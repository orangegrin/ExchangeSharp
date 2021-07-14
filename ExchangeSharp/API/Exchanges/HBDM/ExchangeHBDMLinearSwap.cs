﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
namespace ExchangeSharp
{
    public sealed partial class ExchangeHBDMLinearSwap: ExchangeAPI
    {//https://api.btcgateway.pro  cn
        //https://api.hbdm.com     all
        public override string BaseUrl { get; set; } = "https://api.hbdm.vn"; //"https://api.btcgateway.pro";
        public string BaseUrlV1 { get; set; } = "https://api.btcgateway.pro/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.btcgateway.pro/linear-swap-ws";
        public string PrivateUrlV1 { get; set; } = "https://api.btcgateway.pro/api/v1";

        public bool IsMargin { get; set; }
        public string SubType { get; set; }

        private long webSocketId = 0;
        private decimal basicUnit = 100;        /// <summary>
                                                /// 当前的仓位<MarketSymbol,ExchangeOrderResult>
                                                /// </summary>
        public Dictionary<string, ExchangeOrderResult> currentPostionDic = null;
        public ExchangeHBDMLinearSwap()
        {

            //RequestContentType = "application/x-www-form-urlencoded";
            RequestContentType = "application / json";
            NonceStyle = NonceStyle.UnixMilliseconds;
            MarketSymbolSeparator = "_";//string.Empty;
            MarketSymbolIsUppercase = true;
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;
            currentPostionDic = new Dictionary<string, ExchangeOrderResult>();
            RequestTimeout = TimeSpan.FromSeconds(20);
        }
        /// <summary>
        /// marketSymbol ws 2 web 
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <returns></returns>
        private void GetSymbolAndContractCode(string marketSymbol, out string symbol, out string contractCode, out string contractType)
        {
            string[] strAry = new string[2];
            string[] splitAry = marketSymbol.Split(MarketSymbolSeparator.ToCharArray(), StringSplitOptions.None);
            symbol = splitAry[0];
            contractCode = symbol + splitAry[1];
            if (splitAry.Length >= 3)
            {
                contractType = symbol + "-" + splitAry[2];
            }
            else
                contractType = contractCode;
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
            if (seconds == 3600)
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
            return Math.Ceiling(amount / basicUnit) * basicUnit;
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
                    GetSymbolAndContractCode(symbol, out string s, out string code, out string type);
                    long id = System.Threading.Interlocked.Increment(ref webSocketId);
                    //var normalizedSymbol = NormalizeMarketSymbol(symbol);
                    string channel = $"market.{type}.depth.step0";
                    await _socket.SendMessageAsync(new { sub = channel, id = "id" + id.ToStringInvariant() });
                }
            });
        }
        #endregion

        protected override async Task OnGetHistoricalTradesAsync(Func<IEnumerable<ExchangeTrade>, bool> callback, string marketSymbol, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            long? lastTradeId = null;

            bool running = true;
            Dictionary<string, object> payload = await GetNoncePayloadAsync();

            payload["contract_code"] = marketSymbol;
            payload["trade_type"] = 0;
            payload["create_date"] = 3;
            // Abucoins uses a page curser based on trade_id to iterate history. Keep paginating until startDate is reached or we run out of data
            //while (running)
            {
                JToken obj = await MakeJsonRequestAsync<JToken>("/linear-swap-api/v1/swap_cross_matchresults", BaseUrl, payload, "POST");
                //obj = await MakeJsonRequestAsync<JToken>("/linear-swap-api/v1/swap_cross_matchresults" + marketSymbol + "/trades" + (lastTradeId == null ? string.Empty : "?before=" + lastTradeId));
                
//                     foreach (JToken token in obj)
//                     {
// //                         ExchangeTrade trade = ParseExchangeTrade(token);
// //                         if (startDate == null || trade.Timestamp >= startDate)
// //                         {
// //                             trades.Add(trade);
// //                         }
// //                         else
// //                         {
// //                             // sinceDateTime has been passed, no more paging
// //                             running = false;
// //                             break;
// //                         }
//                     }
                
              
                trades.Clear();
                await Task.Delay(1000);
            }
        }
        public override async Task<ExchangeOrderResult> PlaceOrderAsync(ExchangeOrderRequest order)
        {
            // *NOTE* do not wrap in CacheMethodCall
            await new SynchronizationContextRemover();
            //order.MarketSymbol = NormalizeMarketSymbol(order.MarketSymbol);
            return await OnPlaceOrderAsync(order);
        }
        /// <summary>
        /// 双仓模式需要传入是平仓还是开仓
        /// </summary>
        /// <param name="order"></param>
        /// <param name="isOpen"></param>
        /// <returns></returns>
        public override async Task<ExchangeOrderResult> PlaceOrderDoubleSideAsync(ExchangeOrderRequest order, bool isOpen)
        {
            order.Amount = order.Amount;//两边平台100倍
            string marketSymbol = order.MarketSymbol;
            Side side = order.IsBuy == true ? Side.Buy : Side.Sell;
            OrderType orderType = order.OrderType;
            var backResult = await m_OnPlaceOrderAsync(order, isOpen);
            backResult.Amount *= 1;

            return backResult;
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
            order.Amount = order.Amount / 1;//两边平台100倍
            string marketSymbol = order.MarketSymbol;
            Side side = order.IsBuy == true ? Side.Buy : Side.Sell;
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
            if (hadPosition == false || currentPostion.Amount == 0)
            {
                //直接开仓
                openNum = amount;
                closeEndNum = amount;
            }
            else
            {
                currentPostionAmount = currentPostion.Amount / 1;
                if (currentPostion.IsBuy)
                {
                    if (side == Side.Buy)
                    {
                        //直接开仓
                        openNum = amount;
                    }
                    else if (side == Side.Sell)
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
                }
                else if (currentPostion.IsBuy == false)
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
            if (closeNum == 0)//仓位和加仓方向相同
            {
                returnResult.Amount = currentPostionAmount + openNum;
                returnResult.Amount = returnResult.Amount * 1;
                returnResult.IsBuy = order.IsBuy;
            }
            else//仓位和加仓方向相反
            {
                returnResult.Amount = Math.Abs(currentPostionAmount - order.Amount);
                returnResult.Amount = returnResult.Amount * 1;
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

            if (closeNum > 0)//平仓
            {
                ExchangeOrderRequest closeOrder = order;
                closeOrder.IsBuy = !currentPostion.IsBuy;
                closeOrder.Amount = closeNum;
                ExchangeOrderResult downReturnResult = await m_OnPlaceOrderAsync(closeOrder, false);
                downReturnResult.Amount = amount * 1;
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
                m_returnResult.Amount = amount * 1;
                returnResult = m_returnResult;
            }
            Logger.Debug("    OnPlaceOrderAsync 当前仓位：" + returnResult.ToExcleString());
            return returnResult;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            Dictionary<string, object> payload = await GetNoncePayloadAsync();

            try
            {
                if (orderId == "all")
                {
                    payload["contract_code"] = contractType;
                    JToken token = await MakeJsonRequestAsync<JToken>("/linear-swap-api/v1/swap_cross_cancelall", BaseUrl, payload, "POST");
                }
                else
                {
                    payload["order_id"] = orderId;
                    payload["symbol"] = symbol;
                    JToken token = await MakeJsonRequestAsync<JToken>("/linear-swap-api/v1/swap_cross_cancel", BaseUrl, payload, "POST");
                }
            }
            catch (Exception ex)
            {

                if (ex.ToString().Contains("No orders to cancel"))
                {
                    Logger.Debug(ex.ToString());
                }
                else
                {
                    throw ex;
                }
            }

           
        }

        private async Task<ExchangeOrderResult> m_OnPlaceOrderAsync(ExchangeOrderRequest order, bool isOpen)
        {

            bool had = order.ExtraParameters.TryGetValue("orderID", out object orderId);
            if (had)//如果有订单号，先删除再挂订单
            {
                await OnCancelOrderAsync(orderId.ToString(), order.MarketSymbol);
            }
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string addUrl = "/linear-swap-api/v1/swap_cross_order";
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
                //Logger.Debug("m_OnPlaceOrderAsync:" + order.ToString() + "  isOpen:" + isOpen);
                token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrl, payload, "POST");
                jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
                //Logger.Debug("m_OnPlaceOrderAsync:" + jo.ToString());
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
            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_position_info", BaseUrl, payload, "POST");
            foreach (JToken position in token)
            {
                GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
                //if (position["symbol"].ToStringInvariant().Equals(m_symobl[0])&& position["contract_type"].ToStringInvariant().Equals(m_symobl[1]))
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    poitionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 1 * (isBuy ? position["volume"].ConvertInvariant<decimal>() : -position["volume"].ConvertInvariant<decimal>()),
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
            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_position_info", BaseUrl, payload, "POST");
            int count = 0;
            foreach (JToken position in token)
            {
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    count++;
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    decimal position_margin = position["position_margin"].ConvertInvariant<decimal>();
                    decimal currentPrice = position["cost_hold"].ConvertInvariant<decimal>();
                    //Logger.Debug("GetOpenPositionAsync:" + position.ToString());
                    poitionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 1 * position["volume"].ConvertInvariant<decimal>() * (isBuy ? 1 : -1),
                        LiquidationPrice = position["liquidationPrice"].ConvertInvariant<decimal>(),
                        BasePrice = position["cost_open"].ConvertInvariant<decimal>(),
                    };
                    decimal openUse = poitionR.BasePrice / Math.Abs(poitionR.Amount);//单位btc
                    if (isBuy)
                        poitionR.LiquidationPrice = Math.Ceiling(1 / ((1 / poitionR.BasePrice) + (position_margin / Math.Abs(poitionR.Amount))));
                    else
                        poitionR.LiquidationPrice = Math.Floor(1 / ((1 / poitionR.BasePrice) - (position_margin / Math.Abs(poitionR.Amount))));
                    poitionR.LiquidationPrice = await GetLiquidationPriceAsync(symbol);
                    Logger.Debug("Buy：" + Math.Ceiling(1 / ((1 / poitionR.BasePrice) + (position_margin / Math.Abs(poitionR.Amount)))) + "  Sell:" + Math.Floor(1 / ((1 / poitionR.BasePrice) - (position_margin / Math.Abs(poitionR.Amount)))));
                    //Logger.Debug("GetOpenPositionAsync " + count + poitionR.ToString());
                    if (count >= 2)
                    {
                        Logger.Debug("双向开仓错误停止程序 ");//可能是限价单导致的
                        throw new Exception("双向开仓错误!!!!!!!!!!!!");
                        //Environment.Exit(0);
                    }
                }
            }
            return poitionR;
        }

        public override async Task<List<ExchangeMarginPositionResult>> GetOpenPositionDoubleSideAsync(string marketSymbol)
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

            List<ExchangeMarginPositionResult> positionList = new List<ExchangeMarginPositionResult> ();
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type

            var payload = await GetNoncePayloadAsync();
            //payload.Add("contract_code", contractType);
            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_position_info", BaseUrl, payload, "POST");
            int count = 0;
            foreach (JToken position in token)
            {

                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    count++;
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    decimal position_margin = position["position_margin"].ConvertInvariant<decimal>();
                    decimal currentPrice = position["cost_hold"].ConvertInvariant<decimal>();
                    //Logger.Debug("GetOpenPositionAsync:" + position.ToString());
                    var positionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 1 * position["volume"].ConvertInvariant<decimal>() * (isBuy ? 1 : -1),
                        LiquidationPrice = position["liquidationPrice"].ConvertInvariant<decimal>(),
                        BasePrice = position["cost_open"].ConvertInvariant<decimal>(),
                    };
                    decimal openUse = positionR.BasePrice / Math.Abs(positionR.Amount);//单位btc
                    if (isBuy)
                        positionR.LiquidationPrice = Math.Ceiling(1 / ((1 / positionR.BasePrice) + (position_margin / Math.Abs(positionR.Amount))));
                    else
                        positionR.LiquidationPrice = Math.Floor(1 / ((1 / positionR.BasePrice) - (position_margin / Math.Abs(positionR.Amount))));
                    positionR.LiquidationPrice = await GetLiquidationPriceAsync(symbol);
                    Logger.Debug("Buy：" + Math.Ceiling(1 / ((1 / positionR.BasePrice) + (position_margin / Math.Abs(positionR.Amount)))) + "  Sell:" + Math.Floor(1 / ((1 / positionR.BasePrice) - (position_margin / Math.Abs(positionR.Amount)))));
                    Logger.Debug("GetOpenPositionAsync " + count + positionR.ToString());
                    positionList.Add(positionR);
                }
            }
            return positionList;
        }
        public override async Task<ExchangeOrderResult> GetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            ExchangeOrderResult poitionR = null;
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            var payload = await GetNoncePayloadAsync();

            payload["order_id"] = orderId;
            payload["client_order_id"] = "";
            payload["contract_code"] = contractType;
            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_order_info", BaseUrl, payload, "POST");
            Logger.Debug(token.ToString());
            foreach (JToken position in token)
            {
                //if (position["symbol"].ToStringInvariant().Equals(m_symobl[0]) && position["contract_type"].ToStringInvariant().Equals(m_symobl[1]))
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    decimal status = position["status"].ConvertInvariant<decimal>();

                    poitionR = new ExchangeOrderResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 1 * position["volume"].ConvertInvariant<decimal>(),
                        AmountFilled = 1 * position["trade_volume"].ConvertInvariant<decimal>(),
                        IsBuy = isBuy,
                        Price = position["price"].ConvertInvariant<decimal>(),
                        AveragePrice = position["trade_avg_price"].ConvertInvariant<decimal>(),
                    };
                    if (status == 4)
                    {
                        poitionR.Result = ExchangeAPIOrderResult.FilledPartially;
                    }
                    else if (status == 5)
                    {
                        poitionR.Result = ExchangeAPIOrderResult.FilledPartiallyAndCancelled;
                    }
                    else if (status == 6)
                    {
                        poitionR.Result = ExchangeAPIOrderResult.Filled;
                    }
                    else if (status == 7)
                    {
                        poitionR.Result = ExchangeAPIOrderResult.Canceled;
                    }
                    else
                    {
                        poitionR.Result = ExchangeAPIOrderResult.Unknown;
                    }
                }
            }
            return poitionR;
        }
        private async Task<decimal> GetLiquidationPriceAsync(string inSymbol)
        {
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_account_info", BaseUrl, payload, "POST");
            payload.Add("contract_code", inSymbol);
            token = token[0]["contract_detail"];
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
            List<ExchangeOrderResult> poitionRList = new List<ExchangeOrderResult>();
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);  //[0]symbol [1]contract_type
            var payload = await GetNoncePayloadAsync();

            payload["symbol"] = symbol;


            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_openorders", BaseUrl, payload, "POST");
            foreach (JToken position in token["orders"])
            {
                //if (position["symbol"].ToStringInvariant().Equals(m_symobl[0]) && position["contract_type"].ToStringInvariant().Equals(m_symobl[1]))
                if (position["contract_code"].ToStringInvariant().Equals(contractCode))
                {
                    bool isBuy = position["direction"].ConvertInvariant<string>() == "buy";
                    ExchangeOrderResult poitionR = new ExchangeOrderResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 1 * position["volume"].ConvertInvariant<decimal>(),
                        AmountFilled = 1 * position["trade_volume"].ConvertInvariant<decimal>(),
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
            payload["contract_code"] = contractCode;
            payload["trade_type"] = 0;
            payload["create_date"] = 7;

            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_matchresults", BaseUrl, payload, "POST");
            Logger.Debug(token.ToString());
        }
        public override async Task<decimal> GetWalletSummaryAsync(string marketSymbol)
        {
            string symbol = marketSymbol;

            var payload = await GetNoncePayloadAsync();
            //payload.Add("contract_code", marketSymbol);
            JToken token = await MakeJsonRequestAsync<JToken>($"/linear-swap-api/v1/swap_cross_account_info", BaseUrl, payload, "POST");
            foreach (JToken position in token)
            {
                string _symbol = position["margin_asset"].ConvertInvariant<string>();

                decimal margin_balance = position["margin_balance"].ConvertInvariant<decimal>();
                decimal liquidation_price = position["liquidation_price"].ConvertInvariant<decimal>();
                //Logger.Debug(position.ToString());
                if (symbol.Equals(_symbol))
                {
                    return margin_balance;
                }
            }
            return 0;
        }
        protected override IWebSocket OnGetOrderDetailsWebSocket(Action<ExchangeOrderResult> callback)
        {
            /*
             {
    "op": "notify",
    "topic": "orders.btc",
    "ts": 1489474082831,
    "symbol": "BTC",
    "contract_type": "this_week",
    "contract_code": "BTC180914",
    "volume": 111,
    "price": 1111,
    "order_price_type": "limit",
    "direction": "buy",
    "offset": "open",
    "status": 6,
    "lever_rate": 10,
    "order_id": 633989207806582784,
    "order_id_str": "633989207806582784",
    "client_order_id": 10683,
    "order_source": "web",
    "order_type": 1,
    "created_at": 1408076414000,
    "trade_volume": 1,
    "trade_turnover": 1200,
    "fee": 0,
    "trade_avg_price": 10,
    "margin_frozen": 10,
    "profit": 2,
    "trade": [{
        "id": "2131234825-6124591349-1",
        "trade_id": 112,
        "trade_volume": 1,
        "trade_price": 123.4555,
        "trade_fee": 0.234,
        "trade_turnover": 34.123,
        "created_at": 1490759594752,
        "role": "maker"
    }]
}
             */
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {

                var str = msg.ToStringFromUTF8Gzip();
                if (str.Contains("ping"))//心跳添加
                {
                    _socket.SendMessageAsync(str.Replace("ping", "pong"));
                    callback(new ExchangeOrderResult() { MarketSymbol = "ping" });
                    return Task.CompletedTask;
                }
                else
                {
                    JToken token = JToken.Parse(str);

                    if (token["err-code"] != null && token["err-code"].ToString() != "0")
                    {
                        Logger.Info("err-code:" + token["err-code"].ToStringInvariant());
                        return Task.CompletedTask;
                    }
                    //{"success":true,"request":{"op":"authKeyExpires","args":["2xrwtDdMimp5Oi3F6oSmtsew",1552157533,"1665aedbd293e435fafbfaba2e5475f882bae9228bab0f29d9f3b5136d073294"]}}
                    if (token["op"] != null && token["op"].ToStringInvariant() == "auth")
                    {
                        //{ "op": "subscribe", "args": ["order"]}
                        _socket.SendMessageAsync(new { op = "sub", topic = "orders.*" });
                        return Task.CompletedTask;
                    }
                    if (token["table"] == null)
                    {
                        return Task.CompletedTask;
                    }
                    //{ "table":"order","action":"insert","data":[{ "orderID":"b48f4eea-5320-cc06-68f3-d80d60896e31","clOrdID":"","clOrdLinkID":"","account":954891,"symbol":"XBTUSD","side":"Buy","simpleOrderQty":null,"orderQty":100,"price":3850,"displayQty":null,"stopPx":null,"pegOffsetValue":null,"pegPriceType":"","currency":"USD","settlCurrency":"XBt","ordType":"Limit","timeInForce":"GoodTillCancel","execInst":"ParticipateDoNotInitiate","contingencyType":"","exDestination":"XBME","ordStatus":"New","triggered":"","workingIndicator":false,"ordRejReason":"","simpleLeavesQty":null,"leavesQty":100,"simpleCumQty":null,"cumQty":0,"avgPx":null,"multiLegReportingType":"SingleSecurity","text":"Submission from www.bitmex.com","transactTime":"2019-03-09T19:24:21.789Z","timestamp":"2019-03-09T19:24:21.789Z"}]}
                    var action = token["action"].ToStringInvariant();
                    JArray data = token["data"] as JArray;
                    foreach (var t in data)
                    {
                        var marketSymbol = t["symbol"].ToStringInvariant();
                        var order = ParseOrder(t);
                        callback(order);
                        //callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, t.ParseTrade("size", "price", "side", "timestamp", TimestampType.Iso8601, "trdMatchID")));
                    }
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                //连接中断也不应该删除历史信息
                //fullOrders.Clear();
                var payloadJSON = GeneratePayloadJSON();
                await _socket.SendMessageAsync(payloadJSON.Result);
            });

        }

        private async Task<string> GeneratePayloadJSON()
        {
            object expires = await GenerateNonceAsync();
            var message = $"GET\n" +
                $"api.hbdm.com\n" +
                $"/notification\n";
            var signature = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString());
            Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "op", "auth"},
                    { "type", "api"},
                    { "AccessKeyId",  PublicApiKey.ToUnsecureString()},
                    { "SignatureMethod", "HmacSHA256"},
                    { "SignatureVersion", "2"},
                    { "Timestamp", expires},
                    { "Signature", signature }
                };
            return CryptoUtility.GetJsonForPayload(payload);
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
        ///swap_type string  true    合约类型("this_week":当周 "next_week":下周 "quarter":季度)
        ///swap_code string  true    BTC180914
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
        private void AddOrderToPayload(ExchangeOrderRequest order, bool isOpen, Dictionary<string, object> payload)
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
            payload["offset"] = isOpen ? "open" : "close";
            payload["lever_rate"] = 50;
            payload["order_price_type"] = order.OrderType == OrderType.Limit ? "limit" : "optimal_20";
//             foreach (var item in payload)
//             {
//                 Logger.Debug(item.Key + ":" + item.Value);
//             }
        }
        private Dictionary<string, ExchangeOrderResult> fullOrders = new Dictionary<string, ExchangeOrderResult>();
        private ExchangeOrderResult ParseOrder(JObject token, ExchangeOrderRequest orderRequest)
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



        private ExchangeOrderResult ParseOrder(JToken token)
        {            /*
             *{
    "op": "notify",
    "topic": "orders.btc",
    "ts": 1489474082831,
    "symbol": "BTC",
    "contract_type": "this_week",
    "contract_code": "BTC180914",
    "volume": 111,
    "price": 1111,
    "order_price_type": "limit",
    "direction": "buy",
    "offset": "open",
    "status": 6,
    "lever_rate": 10,
    "order_id": 633989207806582784,
    "order_id_str": "633989207806582784",
    "client_order_id": 10683,
    "order_source": "web",
    "order_type": 1,
    "created_at": 1408076414000,
    "trade_volume": 1,
    "trade_turnover": 1200,
    "fee": 0,
    "trade_avg_price": 10,
    "margin_frozen": 10,
    "profit": 2,
    "trade": [{
        "id": "2131234825-6124591349-1",
        "trade_id": 112,
        "trade_volume": 1,
        "trade_price": 123.4555,
        "trade_fee": 0.234,
        "trade_turnover": 34.123,
        "created_at": 1490759594752,
        "role": "maker"
    }]
}
            */

            //Logger.Debug("ParseOrder:" + token.ToString());
            ExchangeOrderResult fullOrder;
            lock (fullOrders)
            {
                bool had = fullOrders.TryGetValue(token["order_id"].ToStringInvariant(), out fullOrder);
                ExchangeOrderResult result = new ExchangeOrderResult()
                {
                    Amount = token["volume"].ConvertInvariant<decimal>() * 1,
                    AmountFilled = token["trade_volume"].ConvertInvariant<decimal>() * 1,
                    Price = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = token["direction"].ToStringInvariant().EqualsWithOption("buy"),
                    OrderDate = token["created_at"].ConvertInvariant<DateTime>(),
                    OrderId = token["order_id"].ToStringInvariant(),
                    MarketSymbol = token["contract_code"].ToStringInvariant(),
                    AveragePrice = token["trade_avg_price"].ConvertInvariant<decimal>(),
                    //StopPrice = token["stopPx"].ConvertInvariant<decimal>(),
                };
                string symbol = token["symbol"].ToStringInvariant();
                result.MarketSymbol = result.MarketSymbol.Replace(symbol, symbol + "_");

                if (had)
                {
                    result.IsBuy = fullOrder.IsBuy;
                }
                else
                {
                    fullOrder = result;
                }

                if (!token["direction"].ToStringInvariant().EqualsWithOption(string.Empty))
                {
                    result.IsBuy = token["direction"].ToStringInvariant().EqualsWithOption("buy");
                    fullOrder.IsBuy = result.IsBuy;
                }

                if (result.Result != ExchangeAPIOrderResult.Filled)//改为成交后不修改成其他状态
                {
                    string status = token["status"].ToStringInvariant();


                    switch (token["status"].ToStringInvariant())
                    {
                        case "3"://已经提交
                            result.Result = ExchangeAPIOrderResult.Pending;
                            Logger.Info("1ExchangeAPIOrderResult.Pending:" + token.ToString());
                            break;
                        case "4"://部分成交
                            if (result.AmountFilled == 0)
                            {
                                result.Result = ExchangeAPIOrderResult.Pending;
                            }
                            else if (result.AmountFilled > 0)
                            {
                                result.Result = ExchangeAPIOrderResult.FilledPartially;
                            }
                            else if (result.Amount == result.AmountFilled)
                            {
                                result.Result = ExchangeAPIOrderResult.Filled;
                            }
                            Logger.Info("2ExchangeAPIOrderResult" + result.Result + ":" + token.ToString());
                            break;
                        case "5"://部分成交后取消订单
                            if (result.Amount == result.AmountFilled)
                                result.Result = ExchangeAPIOrderResult.Filled;
                            else
                                result.Result = ExchangeAPIOrderResult.Canceled;
                            Logger.Info("4ExchangeAPIOrderResult:" + result.Result + ":" + token.ToString());
                            break;
                        case "6"://全部成交
                            if (result.Amount == result.AmountFilled)
                                result.Result = ExchangeAPIOrderResult.Filled;
                            else
                                result.Result = ExchangeAPIOrderResult.Canceled;
                            Logger.Info("4ExchangeAPIOrderResult:" + result.Result + ":" + token.ToString());
                            break;
                        case "7"://取消
                            if (result.Amount == result.AmountFilled)

                                result.Result = ExchangeAPIOrderResult.Filled;
                            else
                                result.Result = ExchangeAPIOrderResult.Canceled;
                            Logger.Info("4ExchangeAPIOrderResult:" + result.Result + ":" + token.ToString());
                            break;
                        default:
                            result.Result = ExchangeAPIOrderResult.Error;
                            Logger.Info("5ExchangeAPIOrderResult.Error:" + token.ToString());
                            break;
                    }
                    if (token["triggered"] != null)
                    {
                        if (token["triggered"].ToStringInvariant().Equals("StopOrderTriggered"))
                        {
                            result.Result = ExchangeAPIOrderResult.TriggerPending;
                        }
                    }
                }

                //if (had)
                //{
                //    if (result.Amount != 0)
                //        fullOrder.Amount = result.Amount;
                //    if (result.Result != ExchangeAPIOrderResult.Error)
                //        fullOrder.Result = result.Result;
                //    if (result.Price != 0)
                //        fullOrder.Price = result.Price;
                //    if (result.OrderDate > fullOrder.OrderDate)
                //        fullOrder.OrderDate = result.OrderDate;
                //    if (result.AmountFilled != 0)
                //        fullOrder.AmountFilled = result.AmountFilled;
                //    if (result.AveragePrice != 0)
                //        fullOrder.AveragePrice = result.AveragePrice;
                //    //                 if (result.IsBuy != fullOrder.IsBuy)
                //    //                     fullOrder.IsBuy = result.IsBuy;
                //}
                //else
                //{
                //    fullOrder = result;
                //}
                //fullOrders[result.OrderId] = result;


                //ExchangeOrderResult fullOrder;
                if (had)
                {
                    if (result.Amount != 0)
                        fullOrder.Amount = result.Amount;
                    if (result.Result != ExchangeAPIOrderResult.Error)
                        fullOrder.Result = result.Result;
                    if (result.Price != 0)
                        fullOrder.Price = result.Price;
                    if (result.OrderDate > fullOrder.OrderDate)
                        fullOrder.OrderDate = result.OrderDate;
                    if (result.AmountFilled != 0)
                        fullOrder.AmountFilled = result.AmountFilled;
                    if (result.AveragePrice != 0)
                        fullOrder.AveragePrice = result.AveragePrice;
                }
                else
                {
                    fullOrder = result;
                }
                if (result == null)
                {
                    Logger.Error("ExchangeAPIOrderResult:" + token.ToString());
                    return fullOrder;
                }
                else
                {
                    fullOrders[result.OrderId] = fullOrder;
                    return fullOrder;//这里返回的是一个引用，如果多线程再次被修改，比如重filed改成pending，后面方法执行之前被修改 ，就会导致后面出问题，应该改成返回一个clone
                }
            }
        }
        #endregion

        public override async Task<IEnumerable<ExchangeTrade>> GetRecentTradesAsync(string marketSymbol)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);
            string url = $"/linear-swap-ex/market/trade?contract_code={contractType}";
            
            JToken allCandles = await MakeJsonRequestAsync<JToken>(url, BaseUrl, null);
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            foreach (var data in allCandles["tick"]["data"])
            {
                ExchangeTrade trade = new ExchangeTrade()
                { 
                    Price = Convert.ToDecimal(data["price"]),
                    Amount = Convert.ToDecimal(data["amount"])

                };
                trades.Add(trade);
            }


            return trades;
        }


        public override async Task<IEnumerable<MarketCandle>> GetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            //marketSymbol = NormalizeMarketSymbol(marketSymbol);
            return await OnGetCandlesAsync(marketSymbol, periodSeconds, startDate, endDate, limit) ;
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
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode, out string contractType);
            List<MarketCandle> candles = new List<MarketCandle>();
            string size = "150";

            if (limit != null)
            {
                // default is 150, max: 2000
                size = (limit.Value.ToStringInvariant());
            }


            string periodString = PeriodSecondsToString(periodSeconds);
            string url = $"/linear-swap-ex/market/history/kline?period={periodString}&contract_code={contractType}";
            if (startDate != null && endDate != null)
            {
                double startDateSeconds = startDate.Value.UnixTimestampFromDateTimeSeconds();
                double endDateSeconds = endDate.Value.UnixTimestampFromDateTimeSeconds();
                url += "&from="+ startDateSeconds;
                url += "&to=" + endDateSeconds;
                size =""+(int)(endDateSeconds-startDateSeconds)/ periodSeconds;
            }
            else
            {
                url += $"&size={ size}";
            }
            
            JToken allCandles = await MakeJsonRequestAsync<JToken>(url, BaseUrl, null);
            //Logger.Debug(allCandles.ToString());
            foreach (var token in allCandles)
            {
                candles.Add(ParseCandle(token, contractType, periodSeconds, "open", "high", "low", "close", "id", TimestampType.UnixSeconds, null, "vol"));
            }
            return candles;
        }

        private MarketCandle ParseCandle( JToken token, string marketSymbol, int periodSeconds, object openKey, object highKey, object lowKey,
            object closeKey, object timestampKey, TimestampType timestampType, object baseVolumeKey, object quoteVolumeKey = null, object weightedAverageKey = null)
        {
            MarketCandle candle = new MarketCandle
            {
                ClosePrice = token[closeKey].ConvertInvariant<decimal>(),
                ExchangeName = ExchangeName.HBDMLinearSwap,
                HighPrice = token[highKey].ConvertInvariant<decimal>(),
                LowPrice = token[lowKey].ConvertInvariant<decimal>(),
                Name = marketSymbol,
                OpenPrice = token[openKey].ConvertInvariant<decimal>(),
                PeriodSeconds = periodSeconds,
                Timestamp = CryptoUtility.ParseTimestamp(token[timestampKey], timestampType)//火币返回中国时间需要-8到utc
            };

            token.ParseVolumes(baseVolumeKey, quoteVolumeKey, candle.ClosePrice, out decimal baseVolume, out decimal convertVolume);
            candle.BaseCurrencyVolume = (double)baseVolume;
            candle.QuoteCurrencyVolume = (double)convertVolume;
            if (weightedAverageKey != null)
            {
                candle.WeightedAverage = token[weightedAverageKey].ConvertInvariant<decimal>();
            }
            candle.PeriodSeconds = token[timestampKey].ConvertInvariant<int>();
            return candle;
        }
        public override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync()
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload.Add("contract_code", "BNB-USDT");
            JToken allCandles = await MakeJsonRequestAsync<JToken>("/linear-swap-api/v1/swap_available_level_rate", BaseUrl, payload, "POST");
            Logger.Debug(allCandles.ToString());
            return null;
        }
        public override bool ErrorNeedNotCareError(Exception ex)
        {
            return ex.ToString().Contains("502 Bad Gateway") || ex.ToString().Contains("502 Bad Gateway") || ex.ToString().Contains("403 Forbidden") || ex.ToString().Contains("Bad Gateway") || ex.ToString().Contains("Bad Gateway")
                || ex.ToString().Contains(@"""err_code"": 1078");//交割中不能交易
        }//"err_code": 1078
    }
    public partial class ExchangeName { public const string HBDMLinearSwap = "HBDMLinearSwap"; }
}
