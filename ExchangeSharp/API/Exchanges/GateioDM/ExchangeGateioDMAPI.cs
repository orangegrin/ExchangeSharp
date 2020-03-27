﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using System.Security.Cryptography;

namespace ExchangeSharp
{
    public sealed partial class ExchangeGateioDMAPI : ExchangeAPI
    {
        //         public override string BaseUrl { get; set; } = "https://fx-api.gateio.ws";
        //         public string BaseUrlV1 { get; set; } = "https://fx-api.gateio.ws/api/v4";

        public override string BaseUrl { get; set; } = "https://fx-api-testnet.gateio.ws";
        public string BaseUrlV1 { get; set; } = "https://fx-api-testnet.gateio.ws/api/v4";

        public override string BaseUrlWebSocket { get; set; } = "wss://fx-ws.gateio.ws/v4/ws";
        public string PrivateUrlV1 { get; set; } = "https://api.GateioDM.com/api/v1";


        
        public bool IsMargin { get; set; }
        public string SubType { get; set; }

        private long webSocketId = 0;
		private decimal basicUnit = 100;		/// <summary>
        /// 当前的仓位<MarketSymbol,ExchangeOrderResult>
        /// </summary>
        private Dictionary<string, ExchangeOrderResult> currentPostionDic = null;
        public ExchangeGateioDMAPI()
        {
            RequestContentType = "application/x-www-form-urlencoded";
            NonceStyle = NonceStyle.UnixSeconds;
            MarketSymbolSeparator = "_";//string.Empty;
            MarketSymbolIsUppercase = false;
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookSometimes;
            currentPostionDic = new Dictionary<string, ExchangeOrderResult>();
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

        /// <summary>
        /// marketSymbol ws 2 web 
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <returns></returns>
        private void GetSymbolAndContractCode(string marketSymbol, out string symbol, out string contractCode)
        {
            string[] strAry = new string[2];
            string[] splitAry = marketSymbol.Split(MarketSymbolSeparator.ToCharArray(), StringSplitOptions.None);
            symbol = marketSymbol;
            contractCode =splitAry[0];
        }
        //1min, 5min, 15min, 30min, 60min,4hour,1day, 1mon
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
            IWebSocket web= ConnectWebSocket(string.Empty, async (_socket, msg) =>
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
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);
                //Logger.Debug(token.ToString());
                if (token["status"] != null)
                {
                    return;
                }
                else if (token["ping"] != null)
                {
                    await _socket.SendMessageAsync(str.Replace("ping", "pong"));
                    return;
                }
                var _event = token["event"].ToString();
                ExchangeOrderBook book = new ExchangeOrderBook();
                var price = 0m;
                var size = 0m;

                var result = token["result"];
                if (_event.Equals("all"))
                {
                    
                    var marketSymbol = result["contract"].ToStringInvariant();

                    JArray bids = result["bids"] as JArray;
                    JArray asks = result["asks"] as JArray;

                    
                    book.SequenceId = token["time"].ConvertInvariant<long>();
                    book.MarketSymbol = marketSymbol;
                    void applyData(JArray data, bool isBuy)
                    {
                        foreach (var d in data)
                        {
                            price = d["p"].ConvertInvariant<decimal>();
                            size = d["s"].ConvertInvariant<decimal>();
                            var depth = new ExchangeOrderPrice { Price = price, Amount = size };
                            if (isBuy)
                            {
                                book.Bids[depth.Price] = depth;
                            }
                            else
                            {
                                book.Asks[depth.Price] = depth;
                            }
                        }
                    }
                    applyData(bids, true);
                    applyData(asks, false);
                    book.IsFull = true;
                }
                else
                {
                    string marketSymbol = "";
                    long SequenceId = 0;
                    book.SequenceId = token["time"].ConvertInvariant<long>();

                    foreach (var d in result)
                    {
                        price = d["p"].ConvertInvariant<decimal>();
                        size = d["s"].ConvertInvariant<decimal>();
                        var depth = new ExchangeOrderPrice { Price = price, Amount = Math.Abs(size) };

                        if (size > 0)
                        {
                            book.Bids[depth.Price] = depth;
                        }
                        else
                        {
                            book.Asks[depth.Price] = depth;
                        }
                        marketSymbol = d["c"].ConvertInvariant<string>();
                    }
                    book.MarketSymbol = marketSymbol;
                    book.IsFull = false;
                }
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
                    
                    string channel = $"futures.order_book";
                    //object o = new { time = id.ToStringInvariant(), channel = channel, @event = "subscribe", payload = "[\"" + symbol + @""", ""20"", ""0""]" };
                    string s = @"{""time"" : 123456, ""channel"" : ""futures.order_book"", ""event"": ""subscribe"", ""payload"" : ["""+ symbol + @""", ""5"", ""0""]}";
                    //Logger.Debug(s.ToString());
                    await _socket.SendMessageAsync(s);
                }
            });
            return web;
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

//         protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
//         {
//             if (CanMakeAuthenticatedRequest(payload))
//             {
//                 // must sort case sensitive
//                 var dict = new SortedDictionary<string, object>(StringComparer.Ordinal)
//                 {
//                     ["Timestamp"] = CryptoUtility.UnixTimeStampToDateTimeMilliseconds(payload["nonce"].ConvertInvariant<long>()).ToString("s"),
//                     ["AccessKeyId"] = PublicApiKey.ToUnsecureString(),
//                     ["SignatureMethod"] = "HmacSHA256",
//                     ["SignatureVersion"] = "2"
//                 };
// 
//                 if (method == "GET")
//                 {
//                     foreach (var kv in payload)
//                     {
//                         dict.Add(kv.Key, kv.Value);
//                     }
//                 }
// 
//                 string msg = CryptoUtility.GetFormForPayload(dict, false, false, false);
//                 string toSign = $"{method}\n{url.Host}\n{url.Path}\n{msg}";
// 
//                 // calculate signature
//                 var sign = CryptoUtility.SHA256SignBase64(toSign, PrivateApiKey.ToUnsecureBytesUTF8()).UrlEncode();
// 
//                 // append signature to end of message
//                 msg += $"&Signature={sign}";
// 
//                 url.Query = msg;
//             }
//             return url.Uri;
//         }
        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                var nonce = payload["nonce"].ConvertInvariant<long>();
                payload.Remove("nonce");

                var strPayload = CryptoUtility.GetJsonForPayload(payload,false);
                /*
                var  testPayload = new Dictionary<string, object>();
                testPayload["contract"] = "BTC_USD";
                testPayload["type"] = "limit";
                testPayload["size"] = 100;
                testPayload["price"] = 6800;
                testPayload["time_in_force"] = "gtc";
                var testPayloadJson = CryptoUtility.GetJsonForPayload(testPayload);
                string tStr = CryptoUtility.GetSHA512HashFromString(testPayloadJson);
                */
                //var msg = "leverage=25";//CryptoUtility.GetFormForPayload(payload,false);
                string tStr = CryptoUtility.GetSHA512HashFromString(strPayload);
                //str = "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e";
                long ts = nonce;
                string pre = request.RequestUri.AbsolutePath.Split("?".ToCharArray(), StringSplitOptions.None)[0];
                var sign = $"{request.Method}\n{pre}\n{request.RequestUri.Query.Replace("?","")}\n{tStr}\n{ts}";
                //var sign = $"{request.Method}\n{request.RequestUri.AbsolutePath}\n{msg}\n{tStr}\n{nonce}";
                string signature = CryptoUtility.SHA512Sign(sign, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey)).ToStringLowerInvariant();

                string key = PublicApiKey.ToUnsecureString();
                string ser = PrivateApiKey.ToUnsecureString();
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Accept", "application/json");
                request.AddHeader("KEY", PublicApiKey.ToUnsecureString());
                request.AddHeader("SIGN", signature);
                request.AddHeader("Timestamp", (ts).ToString());

                await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
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

            ExchangeOrderResult returnResult = await m_OnPlaceOrderAsync(order, true);
            returnResult.Amount = order.Amount;
            
            return returnResult;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            /*
             * 
             * [
                      {
                        "name": "BTC_USD",
                        "type": "inverse",
                        "quanto_multiplier": "0",
                        "mark_type": "index",
                        "last_price": "4123",
                        "mark_price": "4121.41",
                        "index_price": "4121.5",
                        "funding_next_apply": 1546588800,
                        "funding_rate": "0.000333",
                        "funding_interval": 28800,
                        "funding_offset": 0,
                        "interest_rate": "0.001",
                        "order_price_round": "0.5",
                        "mark_price_round": "0.01",
                        "leverage_min": "1",
                        "leverage_max": "100",
                        "maintenance_rate": "0.005",
                        "risk_limit_base": "10",
                        "risk_limit_step": "10",
                        "risk_limit_max": "50",
                        "maker_fee_rate": "-0.00025",
                        "taker_fee_rate": "0.00075",
                        "order_price_deviate": "1",
                        "order_size_min": 1,
                        "order_size_max": 1000000,
                        "orders_limit": 50,
                        "orderbook_id": 39635902,
                        "trade_id": 6935987,
                        "trade_size": 1992012909,
                        "position_size": 4323221,
                        "config_change_time": 1547540148
                      }
                    ]
             * 
             * */
            List<ExchangeMarket> feeDics = new List<ExchangeMarket>();
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format(" /futures/contracts");
            JToken token = await MakeJsonRequestAsync<JToken>(addUrl, BaseUrl, payload, "GET");
            foreach(var feeTmp in token)
            {
                ExchangeMarket em = new ExchangeMarket() { 
                    MarketSymbol = feeTmp["name"].ToString(),
                    FundingRate = feeTmp["funding_rate"].ConvertInvariant<decimal>()
            };
                feeDics.Add(em);
            }
            return feeDics;
        }
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format("/futures/{0}/orders", contractCode.ToLower(), contractCode.ToLower());
            addUrl += "?" + string.Format("contract={0}&status=open", marketSymbol);
            JToken token = await MakeJsonRequestAsync<JToken>(addUrl, BaseUrl, payload, "GET");
            List<ExchangeOrderResult>  ordersRet = new List<ExchangeOrderResult>();
            foreach(var orderS in token)
            {
                ExchangeOrderResult tmpo = new ExchangeOrderResult()
                {

                };
                ordersRet.Add(tmpo);
            }
            return ordersRet;
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format("/futures/{0}/orders/{1}", contractCode.ToLower(), orderId);
            JToken token = await MakeJsonRequestAsync<JToken>(addUrl, BaseUrl, payload, "DELETE");
        }

        public async Task OnCancelAllOrderAsync( string marketSymbol)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format("/futures/{0}/orders", contractCode.ToLower());
            addUrl += "?" + string.Format("contract={0}", marketSymbol);
            JToken token = await MakeJsonRequestAsync<JToken>(addUrl, BaseUrl, payload, "DELETE");
        }

        public override async Task<decimal> GetWalletSummaryAsync(string marketSymbol)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format("/futures/{0}/accounts", contractCode.ToLower());
            
            JToken token = await MakeJsonRequestAsync<JToken>(addUrl, BaseUrl, payload, "GET");

            return token["total"].ConvertInvariant<decimal>();
        }
        public async Task OnCancelPriceTrigerOrderAsync(string orderId, string marketSymbol)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format("/futures/{0}/price_orders/{1}", contractCode.ToLower(), orderId);
            JToken token = await MakeJsonRequestAsync<JToken>(addUrl, BaseUrl, payload, "DELETE");
        }

        public async Task OnCancelAllPriceTrigerOrderAsync(string marketSymbol)
        {
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format("/futures/{0}/price_orders}", contractCode.ToLower());
            addUrl +="?"+ string.Format("contract={0}", marketSymbol);
            JToken token = await MakeJsonRequestAsync<JToken>(addUrl, BaseUrl, payload, "DELETE");
        }

        private async Task<ExchangeOrderResult> m_OnPlaceOrderAsync(ExchangeOrderRequest order, bool isOpen)
        {
            //await SetLeverage(order);
            GetSymbolAndContractCode(order.MarketSymbol, out string symbol, out string contractCode);

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string addUrl = string.Format("/futures/{0}/orders", contractCode);
            AddOrderToPayload(order, isOpen, payload);
           

            JObject token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrl, payload, "POST");
 
            JObject jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
            return ParseOrder(jo, order);
        }

        private async Task<ExchangeOrderResult> SetLeverage(ExchangeOrderRequest order)
        {
            GetSymbolAndContractCode(order.MarketSymbol, out string symbol, out string contractCode);

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string addUrl = string.Format("/futures/{0}/positions/{1}/leverage", contractCode.ToLower(), order.MarketSymbol) ;
            //string addUrl = string.Format("/futures/positions/{0}/leverage",  order.MarketSymbol);
            addUrl = addUrl + "?leverage=" + 25;
            //payload["leverage"] = "25";
            JObject token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrlV1, payload, "POST");

            JObject jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
            return ParseOrder(jo, order);
        }

        public override async Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string marketSymbol)
        {
            /*
             * 
             * {
                  "user": 10000,
                  "contract": "BTC_USD",
                  "size": -9440,
                  "leverage": "0",
                  "risk_limit": "100",
                  "leverage_max": "100",
                  "maintenance_rate": "0.005",
                  "value": "2.497143098997",
                  "margin": "4.431548146258",
                  "entry_price": "3779.55",
                  "liq_price": "99999999",
                  "mark_price": "3780.32",
                  "unrealised_pnl": "-0.000507486844",
                  "realised_pnl": "0.045543982432",
                  "history_pnl": "0",
                  "last_close_pnl": "0",
                  "realised_point": "0",
                  "history_point": "0",
                  "adl_ranking": 5,
                  "pending_orders": 16,
                  "close_order": {
                    "id": 232323,
                    "price": "3779",
                    "is_liq": false
                  }
                }
             * 
             */
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            string addUrl = string.Format("/futures/{0}/positions/{1}", contractCode.ToLower(), marketSymbol);
            JObject token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrlV1, payload, "GET");

            JObject jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
            return ParasePosition(jo);
        }

        private async Task<long> m_OnPlacePriceTriggeredOrder(ExchangeOrderRequest order)
        {
            /*
             * 
             * {
                  "initial": {
                    "contract": "BTC_USD",
                    "size": 100,
                    "price": "5.03",
                    "close": false,
                    "tif": "gtc",
                    "text": "web"
                  },
                  "trigger": {
                    "strategy_type": 0,
                    "price_type": 0,
                    "price": "3000",
                    "rule": 1,
                    "expiration": 86400
                  }
                }
             * 
             * */
            GetSymbolAndContractCode(order.MarketSymbol, out string symbol, out string contractCode);

            Dictionary<string, object> payload = await GetNoncePayloadAsync();

            payload["initial"] = new Dictionary<string, object>() {
                { "contract", order.MarketSymbol },
                { "size", order.Amount },
                { "price", order.Price }, //if order.Price == 0 ,market_price
                { "close",order.Amount==0 ? true :false  }
            };

            payload["trigger"] = new Dictionary<string, object>() {
                { "price", order.StopPrice },
                { "rule", order.IsBuy ? 2 :1  }//1:>= trigger_price 2:<=trigger_price
            };


            string addUrl = string.Format("/futures/{0}/price_orders", contractCode.ToLower());

            JObject token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrlV1, payload, "POST");

            JObject jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
            long orderId = jo["id"].ConvertInvariant<long>();
            return orderId;
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
            payload["contract"] = order.MarketSymbol;

            payload["size"] = order.IsBuy ? order.Amount : -order.Amount;
            //payload["iceberg"] = 0;
            payload["price"] = order.OrderType == OrderType.Limit ? order.Price.ToString() : "0";
            //payload["close"] = false;
            //payload["reduce_only"] = false;
            //payload["tif"] = false;
            //payload["text"] = false;
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
            if (token["status"].ToString().Equals("finished"))
            {
                decimal size = token["size"].ConvertInvariant<decimal>();
                result = new ExchangeOrderResult
                {
                    Amount = size,
                    AmountFilled = size - token["left"].ConvertInvariant<decimal>(),
                    Price = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = size >=0?true:false,
                    OrderDate = new DateTime( token["create_time"].ConvertInvariant<long>()),
                    OrderId = token["id"].ToStringInvariant(),//token.Data["order_id"].ToStringInvariant(),
                    MarketSymbol = token["contract"].ToString(),
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
       
        private ExchangeMarginPositionResult ParasePosition(JObject position)
        {
            ExchangeMarginPositionResult postionRet = new ExchangeMarginPositionResult
            {
                MarketSymbol = position["contract"].ToString(),
                Amount = position["size"].ConvertInvariant<decimal>(),
                LiquidationPrice = position["liq_price"].ConvertInvariant<decimal>(),
                BasePrice = position["entry_price"].ConvertInvariant<decimal>(),
            };
            return postionRet;
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
    public partial class ExchangeName { public const string GateioDM = "GateioDM"; }
}
