using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace ExchangeSharp
{
    public sealed partial class ExchangeFMexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.fmex.com";
        public string BaseUrlV1 { get; set; } = "https://api.hbdm.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.fmex.com/v2/ws";
        public string PrivateUrlV1 { get; set; } = "https://api.hbdm.com/api/v1";

        public bool IsMargin { get; set; }
        public string SubType { get; set; }

        private long webSocketId = 0;
		private decimal basicUnit = 100;		/// <summary>
        /// 当前的仓位<MarketSymbol,ExchangeOrderResult>
        /// </summary>
        public Dictionary<string, ExchangeOrderResult> currentPostionDic = null;
        public ExchangeFMexAPI()
        {
            
            //RequestContentType = "application/x-www-form-urlencoded";
            RequestContentType = "application / json";
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
        private void GetSymbolAndContractCode(string marketSymbol,out string symbol, out string contractCode, out string contractType )
        {
            string[] strAry = new string[2];
            string[] splitAry = marketSymbol.Split(MarketSymbolSeparator.ToCharArray(), StringSplitOptions.None);
            symbol = splitAry[0];
            contractCode = symbol+splitAry[1];
            contractType= symbol +"_"+ splitAry[2];
        }
        /// <summary>
        /// marketSymbol ws 2 web 
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <returns></returns>
        private string[] GetSymbolAndContractType(string marketSymbol)
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
            return Math.Ceiling(amount / basicUnit )* basicUnit;
        }

        private async Task DoWebSocket(IWebSocket socket)
        {
            object obj = @"{""cmd"":""ping"",""args"":[" + Math.Floor(CryptoUtility.UtcNow.UnixTimestampFromDateTimeMilliseconds()) + @"]}";
            bool can = true;
            socket.Connected += (m_socket) =>
            {
                can = false;
                return Task.CompletedTask;
            };
            while (can)
            {
                await socket.SendMessageAsync(obj);
                await Task.Delay(15000);
            }
        }
        #region Websocket API
        protected override IWebSocket OnGetOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            return ConnectWebSocket(string.Empty, async (_socket, msg) =>
            {
                /*
                   {
  "type": "depth.L20.ethbtc",
  "ts": 1523619211000,
  "seq": 120,
  "bids": [0.000100000, 1.000000000, 0.000010000, 1.000000000],
  "asks": [1.000000000, 1.000000000]
}
                 */
                var str = msg.ToStringFromUTF8();
                Logger.Debug(str.ToString());
                JToken token = JToken.Parse(str);
                
                if (token["type"].ToString().Equals("hello"))
                {
                    return;
                }
                else if (token["type"].ToString().Equals("topics"))
                {
                    return;
                }
                else if (token["type"].ToString().Equals("ping"))
                {
                    return;
                }
                var ch = token["type"].ToStringInvariant();
                var sArray = ch.Split('.');
                //Logger.Debug(ch.ToString());
                var marketSymbol = sArray[2].ToStringInvariant();
                ExchangeOrderBook _book = GetBook(token, basicUnit: basicUnit);
                _book.MarketSymbol = marketSymbol;

                ExchangeOrderBook GetBook
                (
                    JToken _token,
                    string asks = "asks",
                    string bids = "bids",
                    string sequence = "ts",
                    decimal basicUnit = 1
                )
                {
                    var book = new ExchangeOrderBook { SequenceId = token[sequence].ConvertInvariant<long>() };
                    JArray askAry = JArray.Parse(token[asks].ToString());
                    JArray bidAry = JArray.Parse(token[bids].ToString());
                    for (int i = 0; i < askAry.Count/2; i+=2)
                    {
                        var depth = new ExchangeOrderPrice { Price = askAry[i].ConvertInvariant<decimal>(), Amount = askAry[i+1].ConvertInvariant<decimal>() * basicUnit };
                        book.Asks[depth.Price] = depth;
                        if (book.Asks.Count == maxCount)
                        {
                            break;
                        }
                    }
                    for (int i = 0; i < bidAry.Count / 2; i+=2)
                    {
                        var depth = new ExchangeOrderPrice { Price = bidAry[i].ConvertInvariant<decimal>(), Amount = bidAry[i + 1].ConvertInvariant<decimal>() * basicUnit };
                        book.Bids[depth.Price] = depth;
                        if (book.Bids.Count == maxCount)
                        {
                            break;
                        }
                    }
                    return book;
                }


                callback(_book);
            }, async (_socket) =>
            {//{"cmd":"sub","args":["$topic", ...],"id":"$client_id"}
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
                    marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                }
                foreach (string symbol in marketSymbols)
                {
                    string channel = $"depth.L20.{symbol}";
                    long id = System.Threading.Interlocked.Increment(ref webSocketId);

                    //GetSymbolAndContractCode(symbol,out string s, out string code, out string type);
                    
                    //var normalizedSymbol = NormalizeMarketSymbol(symbol);
                    
                    string sub = @"{""cmd"":""sub"",""args"":["""+ channel + @"""]}";
                    Logger.Debug(Math.Floor(CryptoUtility.UtcNow.UnixTimestampFromDateTimeMilliseconds()).ToString());
                    await _socket.SendMessageAsync(sub);
                    //{ "cmd":"ping","args":[1540557696867], "id":"sample.client.id"}
                    DoWebSocket(_socket);
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
//             ExchangeOrderResult pos = null;
//             bool _hadPosition = currentPostionDic.TryGetValue(order.MarketSymbol, out pos);
//             if (!_hadPosition)
//             {
//                 pos = new ExchangeOrderResult
//                 {
//                     Amount = 300,
//                     AmountFilled = 0,
//                     //Price = 3997,
//                     IsBuy = true,
//                     MarketSymbol = order.MarketSymbol,
//                     //OrderType = OrderType.Market,
//                 };
//                 currentPostionDic.Add(order.MarketSymbol, pos);
// 
//             }
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
            decimal closeEndNum = -1;
            bool hadPosition = currentPostionDic.TryGetValue(marketSymbol, out currentPostion);
            decimal currentPostionAmount = 0;
            if (hadPosition==false || currentPostion.Amount == 0)
            {
                //直接开仓
                openNum = amount;
                closeEndNum = amount;
            }
            else
            {
                currentPostionAmount = currentPostion.Amount / 100;
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
                        closeEndNum = Math.Abs(currentPostionAmount) - amount;
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
                        closeEndNum = Math.Abs(currentPostionAmount) - amount;
                    }
                    else if (side == Side.Sell)
                    {
                        //直接开仓
                        openNum = amount;
                    }
                }
            }
            //计算最后的仓位和方向
            ExchangeOrderResult returnResult = new ExchangeOrderResult();
            returnResult.MarketSymbol = marketSymbol;
            if (closeNum==0)//仓位和加仓方向相同
            {
                returnResult.Amount = currentPostionAmount + openNum;
                returnResult.Amount = returnResult.Amount * 100;
                returnResult.IsBuy = order.IsBuy;
            }
            else//仓位和加仓方向相反
            {
                returnResult.Amount = Math.Abs(currentPostionAmount - order.Amount);
                returnResult.Amount = returnResult.Amount * 100;
                if (openNum > 0)//如果有新开仓说明最后是新开仓方向，否则是原来的方向
                {
                    returnResult.IsBuy = order.IsBuy;
                }
                else
                {
                    returnResult.IsBuy = !order.IsBuy;
                }
            }
            //为了避免多个crossMarket 同时操作
            if (hadPosition)
            {
                currentPostionDic[marketSymbol] = returnResult;
            }
            else
            {
                if (currentPostionDic.ContainsKey(marketSymbol))
                {
                    currentPostionDic[marketSymbol] = returnResult;
                    Console.WriteLine("按理说应该有仓位");
                }
                else
                    currentPostionDic.Add(marketSymbol, returnResult);
                //currentPostionDic.Add(marketSymbol, returnResult);
            }
            
            if (closeNum>0)//平仓
            {
                ExchangeOrderRequest closeOrder = order;
                closeOrder.IsBuy = ! currentPostion.IsBuy;
                closeOrder.Amount = closeNum;
                ExchangeOrderResult downReturnResult = await m_OnPlaceOrderAsync(closeOrder, false);
                downReturnResult.Amount = amount * 100;
                returnResult = downReturnResult;
                //m_OnPlaceOrderAsync(closeOrder, false);
                //TODO 如果失败了平仓(不需貌似await)
            }
            if (openNum > 0)//开仓
            {
                ExchangeOrderRequest closeOrder = order;
                //closeOrder.IsBuy = true;
                order.Amount = openNum;
                ExchangeOrderResult m_returnResult = await m_OnPlaceOrderAsync(closeOrder, true);
                m_returnResult.Amount = amount * 100;
                returnResult = m_returnResult;
            }
            Logger.Debug("    OnPlaceOrderAsync 当前仓位："+returnResult.ToExcleString());
            return returnResult;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["order_id"] = orderId;
            payload["symbol"] = symbol;
            JToken token = await MakeJsonRequestAsync<JToken>("/api/v1/contract_cancel", BaseUrl, payload, "POST");
        }

        private async Task<ExchangeOrderResult> m_OnPlaceOrderAsync(ExchangeOrderRequest order, bool isOpen)
        {

            bool had = order.ExtraParameters.TryGetValue("orderID", out object orderId);
            if (had)//如果有订单号，先删除再挂订单
            {
                await OnCancelOrderAsync(orderId.ToString(), order.MarketSymbol);
            }
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
            JObject token;
            JObject jo;
            try
            {
                Logger.Debug("m_OnPlaceOrderAsync:"+ order.ToString()+ "  isOpen:"+ isOpen);
                token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrl, payload, "POST");
                jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
                Logger.Debug("m_OnPlaceOrderAsync:"+jo.ToString());
                return ParseOrder(jo, order);
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex);
                Logger.Error(ex.Message, "  payload:", payload, "  addUrl:", addUrl, "  BaseUrl:", BaseUrl, ex.StackTrace);
                throw new Exception(payload.ToString(), ex);
            }
            
        }
        protected override async Task<ExchangeMarginPositionResult> OnGetOpenPositionAsync(string marketSymbol)
        {
            /*
             * [
  {
      "status": "ok",
      "data": [
        {
          "symbol": "BTC",
          "contract_code": "BTC180914",
          "contract_type": "this_week",
          "volume": 1,
          "available": 0,
          "frozen": 0.3,
          "cost_open": 422.78,
          "cost_hold": 422.78,
          "profit_unreal": 0.00007096,
          "profit_rate": 0.07,
          "profit": 0.97,
          "position_margin": 3.4,
          "lever_rate": 10,
          "direction":"buy",
          "last_price":7900.17
         }
        ],
     "ts": 158797866555
    }
]*/
            ExchangeMarginPositionResult poitionR = null;
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/contract_position_info", BaseUrl, payload,"POST");
            foreach (JToken position in token)
            {
                GetSymbolAndContractCode(marketSymbol,out string symbol,out string contractCode, out string contractType);  //[0]symbol [1]contract_type
                //if (position["symbol"].ToStringInvariant().Equals(m_symobl[0])&& position["contract_type"].ToStringInvariant().Equals(m_symobl[1]))
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    poitionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 100*(isBuy?position["volume"].ConvertInvariant<decimal>(): -position["volume"].ConvertInvariant<decimal>()),
                        LiquidationPrice = position["liquidationPrice"].ConvertInvariant<decimal>(),
                        BasePrice = position["cost_open"].ConvertInvariant<decimal>(),
                    };
                }
            }
            return poitionR;
        }
        public override async Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string marketSymbol)
        {
            /*
             *  {
      "status": "ok",
      "data": [
        {
          "symbol": "BTC",
          "contract_code": "BTC180914",
          "contract_type": "this_week",
          "volume": 1,
          "available": 0,
          "frozen": 0.3,
          "cost_open": 422.78,
          "cost_hold": 422.78,
          "profit_unreal": 0.00007096,
          "profit_rate": 0.07,
          "profit": 0.97,
          "position_margin": 3.4,
          "lever_rate": 10,
          "direction":"buy",
          "last_price":7900.17
         }
        ],
     "ts": 158797866555
    }
             */
            
            ExchangeMarginPositionResult poitionR = null;
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/contract_position_info", BaseUrl, payload,"POST");
            int count = 0;
            foreach (JToken position in token)
            {
                
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    count++;
                    bool isBuy = position["direction"].ConvertInvariant<string>()=="buy";
                    decimal position_margin = position["position_margin"].ConvertInvariant<decimal>() ;
                    decimal currentPrice = position["cost_hold"].ConvertInvariant<decimal>();
                    Logger.Debug("GetOpenPositionAsync:" + position.ToString());
                    poitionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 100*position["volume"].ConvertInvariant<decimal>()* (isBuy ?1:-1),
                        LiquidationPrice = position["liquidationPrice"].ConvertInvariant<decimal>(),
                        BasePrice = position["cost_open"].ConvertInvariant<decimal>(),
                    };
                    decimal openUse = poitionR.BasePrice /Math.Abs(poitionR.Amount);//单位btc
                    if (isBuy)
                        poitionR.LiquidationPrice =Math.Ceiling(1/((1/poitionR.BasePrice)+ (position_margin/ Math.Abs(poitionR.Amount))));
                    else
                        poitionR.LiquidationPrice =Math.Floor(1 / ((1 / poitionR.BasePrice) - (position_margin / Math.Abs(poitionR.Amount))));
                    poitionR.LiquidationPrice = await GetLiquidationPriceAsync(symbol);
                    Logger.Debug("Buy："+  Math.Ceiling(1 / ((1 / poitionR.BasePrice) + (position_margin / Math.Abs(poitionR.Amount))))+"  Sell:" + Math.Floor(1 / ((1 / poitionR.BasePrice) - (position_margin / Math.Abs(poitionR.Amount)))));
                    Logger.Debug("GetOpenPositionAsync " + count + poitionR.ToString());
                    if (count>=2)
                    {
                        Logger.Debug("双向开仓错误停止程序 ");
                        Environment.Exit(0);
                    }
                }
            }
            return poitionR;
        }
        public override async Task<ExchangeOrderResult> GetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            ExchangeOrderResult poitionR = null;
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            var payload = await GetNoncePayloadAsync();
            
            payload["order_id"] = orderId;
            payload["client_order_id"] = "";
            payload["symbol"] = symbol;
            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/contract_order_info", BaseUrl, payload, "POST");
            foreach (JToken position in token)
            {
                //if (position["symbol"].ToStringInvariant().Equals(m_symobl[0]) && position["contract_type"].ToStringInvariant().Equals(m_symobl[1]))
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    poitionR = new ExchangeOrderResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 100 * position["volume"].ConvertInvariant<decimal>() ,
                        AmountFilled = 100 * position["trade_volume"].ConvertInvariant<decimal>(),
                        IsBuy = isBuy,
                        Price = position["price"].ConvertInvariant<decimal>(),
                        AveragePrice = position["trade_avg_price"].ConvertInvariant<decimal>(),
                    };
                }
            }
            return poitionR;
        }
        private async Task< decimal> GetLiquidationPriceAsync(string inSymbol)
        {
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/contract_account_info", BaseUrl, payload, "POST");
            foreach (JToken position in token)
            {
                string symbol = position["symbol"].ConvertInvariant<string>();

                decimal margin_balance = position["margin_balance"].ConvertInvariant<decimal>();
                decimal liquidation_price = position["liquidation_price"].ConvertInvariant<decimal>();
                Logger.Debug(position.ToString());
                if (symbol.Equals(inSymbol))
                {
                    return liquidation_price;
                }
            }
            return 0;
        }
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List< ExchangeOrderResult> poitionRList =  new List<ExchangeOrderResult>();
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            var payload = await GetNoncePayloadAsync();

            payload["symbol"] = symbol;


            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/contract_openorders", BaseUrl, payload, "POST");
            foreach (JToken position in token["orders"])
            {
                //if (position["symbol"].ToStringInvariant().Equals(m_symobl[0]) && position["contract_type"].ToStringInvariant().Equals(m_symobl[1]))
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    ExchangeOrderResult poitionR = new ExchangeOrderResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 100 * position["volume"].ConvertInvariant<decimal>(),
                        AmountFilled = 100 * position["trade_volume"].ConvertInvariant<decimal>(),
                        IsBuy = isBuy,
                        Price = position["price"].ConvertInvariant<decimal>(),
                        AveragePrice = position["trade_avg_price"].ConvertInvariant<decimal>(),
                        Result = GetOderType(position["order_type"].ConvertInvariant<int>()),  //订单类型，1:报单 、 2:撤单 、 3:强平、4:交割
                        OrderId = position["order_id_str"].ConvertInvariant<string>(),
                    };
                    poitionRList.Add(poitionR);
                }
            }
            return poitionRList;
        }
        //订单类型，1:报单 、 2:撤单 、 3:强平、4:交割
        private ExchangeAPIOrderResult GetOderType(int type)
        {
            if (type == 1)
            {
                return ExchangeAPIOrderResult.Pending;
            }
            else if (type == 2)
            {
                return ExchangeAPIOrderResult.Canceled;
            }
            else if (type == 3)
            {
                return ExchangeAPIOrderResult.Filled;
            }
            else if (type == 4)
            {
                return ExchangeAPIOrderResult.Filled;
            }
            return ExchangeAPIOrderResult.Unknown;
        }
        public override async Task GetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            var payload = await GetNoncePayloadAsync();
            payload["symbol"] = symbol;
            payload["trade_type"] = 0; //order.OrderType.ToStringInvariant();
            payload["create_date"] = 7;
            payload["contract_code	"] = contractCode;

            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/contract_matchresults", BaseUrl, payload, "POST");
            Logger.Debug(token.ToString());
        }
        public override async Task<decimal> GetWalletSummaryAsync(string marketSymbol)
        {
            string symbol = "";
            if (!string.IsNullOrEmpty(marketSymbol))
            {
                GetSymbolAndContractCode(marketSymbol, out string s, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
               
                symbol = s;
            }
           
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/contract_account_inf", BaseUrl, payload, "POST ");
            foreach (var item in token)
            {
                var transactType = item["transactType"].ToStringInvariant();
                var count = item["amount"].ConvertInvariant<decimal>();

                if (transactType.Equals("Total"))
                {
                    return count;
                }
            }
            return 0;
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
            GetSymbolAndContractCode(order.MarketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            //payload["symbol"] = symbol;
            //payload["contract_type"] = strAry[1]; //order.OrderType.ToStringInvariant();
            payload["contract_code"] = contractCode;

            payload["client_order_id"] = "";
            //if (order.Price!=0)
            {
                payload["price"] = order.Price;
            }
            payload["volume"] = (int)order.Amount;
            payload["direction"] = order.IsBuy ? "buy" : "sell"; ;
            payload["offset"] = isOpen? "open": "close";
            payload["lever_rate"] = 5;
            payload["order_price_type"] = order.OrderType == OrderType.Limit? "limit": "optimal_20";
            foreach (var item in payload)
            {
                Logger.Debug(item.Key + ":" + item.Value);
            }
        }
        private ExchangeOrderResult ParseOrder(JObject token,ExchangeOrderRequest orderRequest)
        {
            /*
{
  "status": "ok",
  "data": {
    "order_id": 637039332274479104,
    "order_id_str": "637039332274479104"
  },
  "ts": 1571923612438
}" }
            */
            ExchangeOrderResult result = new ExchangeOrderResult();
            if (token["status"].ToString().Equals("ok"))
            {
                result = new ExchangeOrderResult();
                result.Amount = orderRequest.Amount;
                result.AmountFilled = 0;
                result.Price = orderRequest.Price;
                result.IsBuy = orderRequest.IsBuy;
                result.OrderDate = new DateTime(token["ts"].ConvertInvariant<long>());
                result.OrderId = token["data"]["order_id"].ToStringInvariant();//token.Data["order_id"].ToStringInvariant(),
                result.MarketSymbol = orderRequest.MarketSymbol;
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
    public partial class ExchangeName { public const string FMex = "FMex"; }
}
