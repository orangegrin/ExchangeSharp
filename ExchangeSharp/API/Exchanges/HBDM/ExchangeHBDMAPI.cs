using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public sealed partial class ExchangeHBDMAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.hbdm.com";
        public string BaseUrlV1 { get; set; } = "https://api.hbdm.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://www.hbdm.com/ws";
        public string PrivateUrlV1 { get; set; } = "https://api.hbdm.com/api/v1";

        public bool IsMargin { get; set; }
        public string SubType { get; set; }

        private long webSocketId = 0;
		private decimal basicUnit = 100;		/// <summary>
        /// 当前的仓位<MarketSymbol,ExchangeOrderResult>
        /// </summary>
        private Dictionary<string, ExchangeOrderResult> currentPostionDic = null;
        public ExchangeHBDMAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixMilliseconds;
            MarketSymbolSeparator = "_";//string.Empty;
            MarketSymbolIsUppercase = false;
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;
            currentPostionDic = new Dictionary<string, ExchangeOrderResult>();
        }
        /// <summary>
        /// marketSymbol ws 2 web 
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <returns></returns>
        private string[] GetSymbolAndContractCode(string marketSymbol)
        {
            string[] strAry = new string[2];
            string[] splitAry = marketSymbol.Split(MarketSymbolSeparator.ToCharArray(), StringSplitOptions.None);
            strAry[0] = splitAry[0];
            if (splitAry[1].Equals("CW"))
            {
                strAry[1] = "this_week";
            }
            else if (splitAry[1].Equals("NW"))
            {
                strAry[1] = "next_week";
            }
            else if (splitAry[1].Equals("CQ"))
            {
                strAry[1] = "quarter";
            }
            return strAry;
        }
        public override string ExchangeMarketSymbolToGlobalMarketSymbol(string marketSymbol)
        {
            if (marketSymbol.Length < 6)
            {
                throw new ArgumentException("Invalid market symbol " + marketSymbol);
            }
            else if (marketSymbol.Length == 6)
            {
                return ExchangeMarketSymbolToGlobalMarketSymbolWithSeparator(marketSymbol.Substring(0, 3) + GlobalMarketSymbolSeparator + marketSymbol.Substring(3, 3), GlobalMarketSymbolSeparator);
            }
            return ExchangeMarketSymbolToGlobalMarketSymbolWithSeparator(marketSymbol.Substring(3) + GlobalMarketSymbolSeparator + marketSymbol.Substring(0, 3), GlobalMarketSymbolSeparator);
        }

        public override string PeriodSecondsToString(int seconds)
        {
            return CryptoUtility.SecondsToPeriodStringLong(seconds);
        }

        public override decimal AmountComplianceCheck(decimal amount)
        {
            return Math.Floor(amount * basicUnit) / basicUnit;
        }

        #region Websocket API
        protected override IWebSocket OnGetOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            return ConnectWebSocket(string.Empty, async (_socket, msg) =>
            {
                /*
{{
  "id": "id1",
  "status": "ok",
  "subbed": "market.btcusdt.depth.step0",
  "ts": 1526749164133
}}


{{
  "ch": "market.btcusdt.depth.step0",
  "ts": 1526749254037,
  "tick": {
    "bids": [
      [
        8268.3,
        0.101
      ],
      [
        8268.29,
        0.8248
      ],
      
    ],
    "asks": [
      [
        8275.07,
        0.1961
      ],
	  
      [
        8337.1,
        0.5803
      ]
    ],
    "ts": 1526749254016,
    "version": 7664175145
  }
}}
                 */
                var str = msg.ToStringFromUTF8Gzip();
                JToken token = JToken.Parse(str);

                if (token["status"] != null)
                {
                    return;
                }
                else if (token["ping"] != null)
                {
                    await _socket.SendMessageAsync(str.Replace("ping", "pong"));
                    return;
                }
                var ch = token["ch"].ToStringInvariant();
                var sArray = ch.Split('.');
                var marketSymbol = sArray[1].ToStringInvariant();
                ExchangeOrderBook book = ExchangeAPIExtensions.ParseOrderBookFromJTokenArrays(token["tick"], maxCount: maxCount, basicUnit: basicUnit);
                book.MarketSymbol = marketSymbol;
                callback(book);
            }, async (_socket) =>
            {
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
                    marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                }
                foreach (string symbol in marketSymbols)
                {
                    long id = System.Threading.Interlocked.Increment(ref webSocketId);
                    //var normalizedSymbol = NormalizeMarketSymbol(symbol);
                    string channel = $"market.{symbol}.depth.step11";
                    await _socket.SendMessageAsync(new { sub = channel, id = "id" + id.ToStringInvariant() });
                }
            });
        }
        #endregion

        #region Rest API
        /// <summary>
        /// 如果操作有仓位，并且新操作和仓位反向，那么如果新仓位>当前仓位，平仓在开新仓位-老仓位。
        /// 否则  平掉对应老仓位数量的新仓位
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <param name="side"></param>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <param name="orderType"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            if (order.OrderType == OrderType.Limit)
            {
                Logger.Error(new Exception("尚未实现限价单"));
                return new ExchangeOrderResult();
            }
            order.Amount = order.Amount / 100;//两边平台100倍
            string marketSymbol = order.MarketSymbol;
            Side side = order.IsBuy==true?Side.Buy:Side.Sell;
            decimal price = order.Price;
            decimal amount = order.Amount;
            OrderType orderType = order.OrderType;
            //如果没有仓位，或者平仓直接开仓
            ExchangeOrderResult currentPostion;

            decimal openNum = 0;
            decimal closeNum = 0;
            bool hadPosition = currentPostionDic.TryGetValue(marketSymbol, out currentPostion);
            if (hadPosition==false || currentPostion.Amount == 0)
            {
                //直接开仓
                openNum = amount;
            }
            else
            {   
                if(currentPostion.IsBuy)
                {
                    if(side == Side.Buy)
                    {
                        //直接开仓
                        openNum = amount;
                    }
                    else if (side == Side.Sell)
                    {
                        //如果当前仓位>=开仓位。平仓
                        if(Math.Abs(currentPostion.Amount)>= amount)
                        {
                            closeNum = amount;
                        }
                        else
                        {
                            closeNum = Math.Abs(currentPostion.Amount);
                            openNum = amount - Math.Abs(currentPostion.Amount);
                        }
                    }
                }
                else if (currentPostion.IsBuy==false)
                {
                    if (side == Side.Buy)
                    {
                        //如果当前仓位>=开仓位。平仓
                        if (Math.Abs(currentPostion.Amount) >= amount)
                        {
                            closeNum = amount;
                        }
                        else
                        {
                            closeNum = Math.Abs(currentPostion.Amount);
                            openNum = amount - Math.Abs(currentPostion.Amount);
                        }
                    }
                    else if (side == Side.Sell)
                    {
                        //直接开仓
                        openNum = amount;
                    }
                }
            }
            ExchangeOrderResult returnResult = null;
            if (closeNum>0)//平仓
            {
                ExchangeOrderRequest closeOrder = order;
                closeOrder.IsBuy = true;
                ExchangeOrderResult downReturnResult = await m_OnPlaceOrderAsync(closeOrder,false);
            }
            if (openNum > 0)//开仓
            {
                ExchangeOrderRequest closeOrder = order;
                closeOrder.IsBuy = true;
                returnResult = await m_OnPlaceOrderAsync(closeOrder, true);
            }
            else//如果刚刚好平仓，
            {
                returnResult = new ExchangeOrderResult();
                returnResult.Amount = 0;
            }
            if(hadPosition)
            {
                currentPostionDic[marketSymbol] = returnResult;
            }
            else
            {
                currentPostionDic.Add(marketSymbol, returnResult);
            }
            return returnResult;


        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["orderID"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "DELETE");
        }

        private async Task<ExchangeOrderResult> m_OnPlaceOrderAsync(ExchangeOrderRequest order,bool isOpen)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            AddOrderToPayload(order, isOpen, payload);
            JToken token = await MakeJsonRequestAsync<JToken>("/api/v1/contract_order", BaseUrl, payload, "POST");
            return ParseOrder(token, order);
        }

        //protected override async Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] orders)
        //{
        //    List<ExchangeOrderResult> results = new List<ExchangeOrderResult>();
        //    Dictionary<string, object> payload = await GetNoncePayloadAsync();
        //    List<Dictionary<string, object>> orderRequests = new List<Dictionary<string, object>>();
        //    foreach (ExchangeOrderRequest order in orders)
        //    {
        //        Dictionary<string, object> subPayload = new Dictionary<string, object>();
        //        AddOrderToPayload(order, subPayload);
        //        orderRequests.Add(subPayload);
        //    }
        //    payload["orders"] = orderRequests;
        //    JToken token = await MakeJsonRequestAsync<JToken>("/order/bulk", BaseUrl, payload, "POST");
        //    foreach (JToken orderResultToken in token)
        //    {
        //        results.Add(ParseOrder(orderResultToken));
        //    }
        //    return results.ToArray();
        //}

        /// <summary>
        ///symbol  string  true    "BTC","ETH"...
        ///contract_type string  true    合约类型("this_week":当周 "next_week":下周 "quarter":季度)
        ///contract_code string  true    BTC180914
        ///client_order_id long    false   客户自己填写和维护，这次一定要大于上一次
        ///price decimal true    价格
        ///volume long    true    委托数量(张)
        ///direction string  true    "buy":买 "sell":卖
        ///offset string  true    "open":开 "close":平
        ///lever_rate int true    杠杆倍数[“开仓”若有10倍多单，就不能再下20倍多单]
        ///order_price_type string  true    订单报价类型 "limit":限价 "opponent":对手价
        /// </summary>
        /// <param name="order"></param>
        /// <param name="payload"></param>
        private void AddOrderToPayload(ExchangeOrderRequest order,bool isOpen,Dictionary<string, object> payload)
        {
            string[] strAry = GetSymbolAndContractCode(order.MarketSymbol);
            payload["symbol"] = strAry[0];
            payload["contract_type"] = strAry[1]; //order.OrderType.ToStringInvariant();
            //payload["contract_code"] = order.OrderType.ToStringInvariant();
            payload["client_order_id"] = "";
            payload["price"] = order.Price;
            payload["volume"] = order.Amount;
            payload["direction"] = order.IsBuy ? "buy" : "sell"; ;
            payload["offset"] = isOpen? "open": "close";
            payload["lever_rate"] = 25;
            payload["order_price_type"] = order.OrderType == OrderType.Limit? "limit": "opponent";
            if (order.ExtraParameters.TryGetValue("execInst", out var execInst))
            {
                payload["execInst"] = execInst;
            }
        }

        private ExchangeOrderResult ParseOrder(JToken token,ExchangeOrderRequest orderRequest)
        {
            /*
{[
  {
status	true	string	请求处理结果	"ok" , "error"
<list>(属性名称: errors)				
index	true	int	订单索引	
err_code	true	int	错误码	
err_msg	true	string	错误信息	
</list>				
<list>(属性名称: success)				
index	true	int	订单索引	
order_id	true	long	订单ID	
client_order_id	true	long	用户下单时填写的客户端订单ID，没填则不返回	
</list>				
ts
  }
]}
            */
            ExchangeOrderResult result = new ExchangeOrderResult();
            if (token["ts"]["statue"].ToString().Equals("ok"))
            {
                result = new ExchangeOrderResult
                {
                    Amount = orderRequest.Amount,
                    AmountFilled = 0,
                    Price = orderRequest.Price,
                    IsBuy = orderRequest.IsBuy,
                    OrderDate = token["ts"].ConvertInvariant<DateTime>(),
                    OrderId = token["order_id"].ToStringInvariant(),
                    MarketSymbol = orderRequest.MarketSymbol,
                };
                result.Result = ExchangeAPIOrderResult.Pending;
            }
            else
            {
                result.Result = ExchangeAPIOrderResult.Error;
            }

            //// http://www.onixs.biz/fix-dictionary/5.0.SP2/tagNum_39.html
            //switch (token["order_type"].ToStringInvariant())
            //{
            //    case "New":
            //        result.Result = ExchangeAPIOrderResult.Pending;
            //        break;
            //    case "PartiallyFilled":
            //        result.Result = ExchangeAPIOrderResult.FilledPartially;
            //        break;
            //    case "Filled":
            //        result.Result = ExchangeAPIOrderResult.Filled;
            //        break;
            //    case "Canceled":
            //        result.Result = ExchangeAPIOrderResult.Canceled;
            //        break;

            //    default:
            //        result.Result = ExchangeAPIOrderResult.Error;
            //        break;
            //}

            return result;
        }
        #endregion

    }
    public partial class ExchangeName { public const string HBDM = "HBDM"; }
}
