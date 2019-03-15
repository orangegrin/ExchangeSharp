using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

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
        //1min, 5min, 15min, 30min, 60min,4hour,1day, 1mon
        public override string PeriodSecondsToString(int seconds)
        {
            if(seconds == 3600)
            {
                return "60min";

            }
            else
            {
                return CryptoUtility.SecondsToPeriodStringLong(seconds);
            }
        }

        public override decimal AmountComplianceCheck(decimal amount)
        {
            return Math.Ceiling(amount * basicUnit) / basicUnit;
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

        public override async Task<ExchangeOrderResult> PlaceOrderAsync(ExchangeOrderRequest order)
        {
            // *NOTE* do not wrap in CacheMethodCall
            await new SynchronizationContextRemover();
            //order.MarketSymbol = NormalizeMarketSymbol(order.MarketSymbol);
            return await OnPlaceOrderAsync(order);
        }
        #region Rest API

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // must sort case sensitive
                var dict = new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["Timestamp"] = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(payload["nonce"].ConvertInvariant<long>()).ToString("s"),
                    ["AccessKeyId"] = PublicApiKey.ToUnsecureString(),
                    ["SignatureMethod"] = "HmacSHA256",
                    ["SignatureVersion"] = "2"
                };

                if (method == "GET")
                {
                    foreach (var kv in payload)
                    {
                        dict.Add(kv.Key, kv.Value);
                    }
                }

                string msg = CryptoUtility.GetFormForPayload(dict, false, false, false);
                string toSign = $"{method}\n{url.Host}\n{url.Path}\n{msg}";

                // calculate signature
                var sign = CryptoUtility.SHA256SignBase64(toSign, PrivateApiKey.ToUnsecureBytesUTF8()).UrlEncode();

                // append signature to end of message
                msg += $"&Signature={sign}";

                url.Query = msg;
            }
            return url.Uri;
        }
        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                if (request.Method == "POST")
                {
                    request.AddHeader("content-type", "application/json");
                    payload.Remove("nonce");
                    var msg = CryptoUtility.GetJsonForPayload(payload);
                    await CryptoUtility.WriteToRequestAsync(request, msg);
                }
            }
        }
        #region 
        /*

        /// <summary>
        /// 请求参数签名
        /// </summary>
        /// <param name="method">请求方法</param>
        /// <param name="host">API域名</param>
        /// <param name="resourcePath">资源地址</param>
        /// <param name="parameters">请求参数</param>
        /// <returns></returns>
        private string GetSignatureStr(string method, string host, string resourcePath, string parameters)
        {

            var sign = string.Empty;
            StringBuilder sb = new StringBuilder();
            sb.Append(method.ToString().ToUpper()).Append("\n")
                .Append(host).Append("\n")
                .Append(resourcePath).Append("\n");
            //参数排序
            var paraArray = parameters.Split('&');
            List<string> parametersList = new List<string>();
            foreach (var item in paraArray)
            {
                parametersList.Add(item);
            }
            parametersList.Sort(delegate (string s1, string s2) { return string.CompareOrdinal(s1, s2); });
            foreach (var item in parametersList)
            {
                sb.Append(item).Append("&");
            }
            sign = sb.ToString().TrimEnd('&');
            //计算签名，将以下两个参数传入加密哈希函数
            sign = CalculateSignature256(sign, PublicApiKey.ToString());
            return UrlEncode(sign);
        }
        /// <summary>
        /// Hmacsha256加密
        /// </summary>
        /// <param name="text"></param>
        /// <param name="secretKey"></param>
        /// <returns></returns>
        private static string CalculateSignature256(string text, string secretKey)
        {
            using (var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                return Convert.ToBase64String(hashmessage);
            }
        }
        /// <summary>
        /// 转义字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string UrlEncode(string str)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in str)
            {
                if (HttpUtility.UrlEncode(c.ToString(), Encoding.UTF8).Length > 1)
                {
                    builder.Append(HttpUtility.UrlEncode(c.ToString(), Encoding.UTF8).ToUpper());
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }
        /// <summary>
        /// Uri参数值进行转义
        /// </summary>
        /// <param name="parameters">参数字符串</param>
        /// <returns></returns>
        private string UriEncodeParameterValue(string parameters)
        {
            var sb = new StringBuilder();
            var paraArray = parameters.Split('&');
            var sortDic = new SortedDictionary<string, string>();
            foreach (var item in paraArray)
            {
                var para = item.Split('=');
                sortDic.Add(para.First(), UrlEncode(para.Last()));
            }
            foreach (var item in sortDic)
            {
                sb.Append(item.Key).Append("=").Append(item.Value).Append("&");
            }
            return sb.ToString().TrimEnd('&');
        }
        
        /// <summary>
        /// 获取通用签名参数
        /// </summary>
        /// <returns></returns>
        private string GetCommonParameters()
        {
            return $"AccessKeyId={PublicApiKey.ToUnsecureString()}&SignatureMethod={"HmacSHA256"}&SignatureVersion={2}&Timestamp={DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")}";
        }
        */
        #endregion
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
            ///////////////////////TEST/////////////////
            //currentPostionDic.Add(order.MarketSymbol, new ExchangeOrderResult
            //{
            //    Amount = 100,
            //    AmountFilled = 0,
            //    Price = 3888,
            //    IsBuy = false,
            //    MarketSymbol = order.MarketSymbol,
            //});
            ///////////////////////TEST/////////////////
            //if (order.OrderType == OrderType.Limit)
            //{
            //    Logger.Error(new Exception("尚未实现限价单"));
            //    return new ExchangeOrderResult();
            //}
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
                decimal currentPostionAmount = currentPostion.Amount / 100;
                if (currentPostion.IsBuy)
                {
                    if(side == Side.Buy)
                    {
                        //直接开仓
                        openNum = amount;
                    }
                    else if (side == Side.Sell)
                    {
                        //如果当前仓位>=开仓位。平仓
                        if(Math.Abs(currentPostionAmount) >= amount)
                        {
                            closeNum = amount;
                        }
                        else
                        {
                            closeNum = Math.Abs(currentPostionAmount);
                            openNum = amount - Math.Abs(currentPostionAmount);
                        }
                    }
                }
                else if (currentPostion.IsBuy==false)
                {
                    if (side == Side.Buy)
                    {
                        //如果当前仓位>=开仓位。平仓
                        if (Math.Abs(currentPostionAmount) >= amount)
                        {
                            closeNum = amount;
                        }
                        else
                        {
                            closeNum = Math.Abs(currentPostionAmount);
                            openNum = amount - Math.Abs(currentPostionAmount);
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
                closeOrder.IsBuy = ! currentPostion.IsBuy;
                closeOrder.Amount = closeNum;
                ExchangeOrderResult downReturnResult = await m_OnPlaceOrderAsync(closeOrder, false);
                downReturnResult.Amount = amount * 100;
                //m_OnPlaceOrderAsync(closeOrder, false);
                //TODO 如果失败了平仓(不需貌似await)
            }
            if (openNum > 0)//开仓
            {
                ExchangeOrderRequest closeOrder = order;
                //closeOrder.IsBuy = true;
                order.Amount = openNum;
                returnResult = await m_OnPlaceOrderAsync(closeOrder, true);
                returnResult.Amount = amount * 100;
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

        private async Task<ExchangeOrderResult> m_OnPlaceOrderAsync(ExchangeOrderRequest order, bool isOpen)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string addUrl = "/api/v1/contract_order";
            AddOrderToPayload(order, isOpen, payload);
            //HuobiApi api = new HuobiApi(PublicApiKey.ToUnsecureString(), "7f0a0c5c-24fd0bb9-eb64134f-2e1b6");
            //OrderPlaceRequest req = new OrderPlaceRequest();
            //req.volume = int.Parse( payload["volume"].ToString());
            //req.direction = payload["direction"].ToString();
            //req.price = int.Parse(payload["price"].ToString());
            //req.offset = payload["offset"].ToString();
            //req.lever_rate = int.Parse(payload["lever_rate"].ToString());
            ////req.contract_code = "BTC181214";
            //req.order_price_type = payload["order_price_type"].ToString();
            //req.symbol = payload["symbol"].ToString();
            //req.contract_type = payload["contract_type"].ToString();
            //string result = api.OrderPlace(req);

            JObject token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrl, payload, "POST");
 
            JObject jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
            return ParseOrder(jo, order);
        }

        private async Task<Dictionary<string, string>> OnGetAccountsAsync()
        {
            /*
            {[
  {
    "id": 3274515,
    "type": "spot",
    "subtype": "",
    "state": "working"
  },
  {
    "id": 4267855,
    "type": "margin",
    "subtype": "btcusdt",
    "state": "working"
  },
  {
    "id": 3544747,
    "type": "margin",
    "subtype": "ethusdt",
    "state": "working"
  },
  {
    "id": 3274640,
    "type": "otc",
    "subtype": "",
    "state": "working"
  }
]}
 */
            Dictionary<string, string> accounts = new Dictionary<string, string>();
            var payload = await GetNoncePayloadAsync();
            JToken data = await MakeJsonRequestAsync<JToken>("/account/accounts", PrivateUrlV1, payload);
            foreach (var acc in data)
            {
                string key = acc["type"].ToStringInvariant() + "_" + acc["subtype"].ToStringInvariant();
                accounts.Add(key, acc["id"].ToStringInvariant());
            }
            return accounts;
        }
        private async Task<string> GetAccountID(bool isMargin = false, string subtype = "")
        {
            var accounts = await OnGetAccountsAsync();
            var key = "spot_";
            if (isMargin)
            {
                key = "margin_" + subtype;
            }
            var account_id = accounts[key];
            return account_id;
        }
        private ExchangeOrderResult ParsePlaceOrder(JToken token, ExchangeOrderRequest order)
        {
            /*
              {
                  "status": "ok",
                  "data": "59378"
                }
            */
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = order.Amount,
                Price = order.Price,
                IsBuy = order.IsBuy,
                OrderId = token.ToStringInvariant(),
                MarketSymbol = order.MarketSymbol
            };
            result.AveragePrice = result.Price;
            result.Result = ExchangeAPIOrderResult.Pending;

            return result;
        }
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
            payload["lever_rate"] = 20;
            payload["order_price_type"] = order.OrderType == OrderType.Limit? "limit": "opponent";
            if (order.ExtraParameters.TryGetValue("execInst", out var execInst))
            {
                payload["execInst"] = execInst;
            }
        }
        private ExchangeOrderResult ParseOrder(JObject token,ExchangeOrderRequest orderRequest)
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
            if (token["status"].ToString().Equals("ok"))
            {
                result = new ExchangeOrderResult
                {
                    Amount = orderRequest.Amount,
                    AmountFilled = 0,
                    Price = orderRequest.Price,
                    IsBuy = orderRequest.IsBuy,
                    OrderDate = new DateTime( token["ts"].ConvertInvariant<long>()),
                    OrderId = token["data"]["order_id"].ToStringInvariant(),//token.Data["order_id"].ToStringInvariant(),
                    MarketSymbol = orderRequest.MarketSymbol,
                };
                result.Result = ExchangeAPIOrderResult.Pending;
            }
            else
            {
                //Logger.Warn("m_OnPlaceOrderAsync 失败 OrderId:"+ token["data"]["order_id"].ToStringInvariant());
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

        public override async Task<IEnumerable<MarketCandle>> GetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            //marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await Cache.CacheMethod(MethodCachePolicy, async () => await OnGetCandlesAsync(marketSymbol, periodSeconds, startDate, endDate, limit), nameof(GetCandlesAsync),
                nameof(marketSymbol), marketSymbol, nameof(periodSeconds), periodSeconds, nameof(startDate), startDate, nameof(endDate), endDate, nameof(limit), limit);
        }
        /// <summary>
        /// symbol  true    string 合约名称        如"BTC_CW"表示BTC当周合约，"BTC_NW"表示BTC次周合约，"BTC_CQ"表示BTC季度合约
        /// period  true    string K线类型        1min, 5min, 15min, 30min, 60min,4hour,1day, 1mon
        /// size    true    integer 获取数量    150[1, 2000]
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <param name="periodSeconds"></param>string K线类型        1min, 5min, 15min, 30min, 60min,4hour,1day, 1mon 传入秒 
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {


            /*
# Response
{
  "ch": "market.BTC_CQ.kline.1min",
  "data": [
    {
      "vol": 2446,
      "close": 5000,
      "count": 2446,
      "high": 5000,
      "id": 1529898120,
      "low": 5000,
      "open": 5000,
      "amount": 48.92
     },
    {
      "vol": 0,
      "close": 5000,
      "count": 0,
      "high": 5000,
      "id": 1529898780,
      "low": 5000,
      "open": 5000,
      "amount": 0
     }
   ],
  "status": "ok",
  "ts": 1529908345313
},
             */

            List<MarketCandle> candles = new List<MarketCandle>();
            string size = "150";
            
            if (limit != null)
            {
                // default is 150, max: 2000
                size=(limit.Value.ToStringInvariant());
            }
            string periodString = PeriodSecondsToString(periodSeconds);
            string url = $"/market/history/kline?period={periodString}&size={size}&symbol={marketSymbol}";
            
            JToken allCandles = await MakeJsonRequestAsync<JToken>(url, BaseUrl, null);
            foreach (var token in allCandles)
            {
                candles.Add(this.ParseCandle(token, marketSymbol, periodSeconds, "open", "high", "low", "close", "id", TimestampType.UnixSeconds, null, "vol"));
            }
            return candles;
        }
    }
    public partial class ExchangeName { public const string HBDM = "HBDM"; }
}
