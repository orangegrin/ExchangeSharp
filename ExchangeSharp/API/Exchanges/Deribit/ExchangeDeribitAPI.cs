/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeDeribitAPI : ExchangeAPI
    {
        //         public override string BaseUrl { get; set; } = "https://www.deribit.com/api/v2";
        //         public override string BaseUrlWebSocket { get; set; } = "wss://www.deribit.com/ws/api/v1/";
        public override string BaseUrl { get; set; } = "https://test.deribit.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://www.deribit.com/ws/api/v2";

        private SortedDictionary<long, decimal> dict_long_decimal = new SortedDictionary<long, decimal>();
        private SortedDictionary<decimal, long> dict_decimal_long = new SortedDictionary<decimal, long>();
        private IWebSocket mWS;
        public ExchangeDeribitAPI()
        {
            RequestWindow = TimeSpan.Zero;
            NonceStyle = NonceStyle.UnixMilliseconds;

            // make the nonce go 10 seconds into the future (the offset is subtracted)
            // this will give us an api-expires 60 seconds into the future
//             NonceOffset = TimeSpan.FromSeconds(-60.0);
//             NonceStyle = NonceStyle.ExpiresUnixSeconds;
            MarketSymbolSeparator = string.Empty;
            RequestContentType = "application/json";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;

            RateLimit = new RateGate(300, TimeSpan.FromMinutes(5));
        }

        public override string ExchangeMarketSymbolToGlobalMarketSymbol(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        public override string GlobalMarketSymbolToExchangeMarketSymbol(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // convert nonce to long, trim off milliseconds
                var nonce = payload["nonce"].ConvertInvariant<long>();
                payload.Remove("nonce");
                //payload.Add("access_token", refresh_token);
                string ts = ((int)CryptoUtility.UtcNow.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
                string body = "";

                foreach (var kv in payload)
                {
                    body += $"&{kv.Key.UrlEncode()}={kv.Value.ToStringInvariant().UrlEncode()}";
                }

                //Timestamp + "\n" + Nonce + "\n" + RequestData;
                //RequestData = UPPERCASE(HTTP_METHOD()) + "\n" + URI() + "\n" + RequestBody + "\n";
                //var sign = $"_={"1452237485895"}&_ackey={"2YZn85siaUf5A"}&_acsec={"BTMSIAJ8IYQTAV4MLN88UAHLIUNYZ3HN"}&_action={request.RequestUri.AbsolutePath}&{"instrument=BTC-15JAN16&price=500&quantity=1"}";
                var sign = $"_={nonce}&_ackey={PublicApiKey.ToUnsecureString()}&_acsec={PrivateApiKey.ToUnsecureString()}&_action={request.RequestUri.AbsolutePath}{body}";
                string signature = CryptoUtility.SHA256SignBase64(sign);
                signature = $"{PublicApiKey.ToUnsecureString()}.{nonce}.{signature}";
                request.AddHeader("X-Deribit-Sig", signature);

                //                 ConnectWebSocket(string.Empty, (_socket, msg) =>
                //                 {
                //                     var str = msg.ToStringFromUTF8();
                //                     JToken token = JToken.Parse(str);
                //                     Logger.Debug("token:" + token.ToString());
                //                     return Task.CompletedTask;
                //                 }, async (_socket) =>
                //                 {
                //                     string str = await GeneratePayloadJSON();
                //                     await _socket.SendMessageAsync(str);
                // 
                //                 });

                
                await Task.Delay(10000);

                //                 if (string.IsNullOrEmpty(access_token))
                //                 {
                //                     await AuthAsync_2();
                //                 }







                try
                {
                    //await CryptoUtility.WritePayloadJsonToRequestAsync(request, body);
                    //await ProcessRequestAsync(request, payload);
                    //await CryptoUtility.WriteToRequestAsync(request, "");
                    //await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex.ToString());
                    throw ex; 
                }
            }
        }
        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // must sort case sensitive
                var dict = new SortedDictionary<string, object>(StringComparer.Ordinal);

                if (method == "GET")
                {
                    foreach (var kv in payload)
                    {
                        dict.Add(kv.Key, kv.Value);
                    }
                }

                string msg = CryptoUtility.GetFormForPayload(dict, false, false, false);
                url.Query = msg;
            }
            return url.Uri;
        }
        private string access_token = string.Empty;
        public async Task AuthAsync(IWebSocket ws)
        {//client_id=fo7WAPRm4P&client_secret=W0H6FJW4IRPZ1MOQ8FP6KMC5RZDUUKXS&grant_type=client_credentials" \
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["client_id"] = PublicApiKey.ToUnsecureString();
            payload["client_secret"] = PrivateApiKey.ToUnsecureString();
            payload["grant_type"] = "client_credentials";

//             JToken reslut = await MakeJsonRequestAsync<JToken>($"/public/auth", BaseUrl, payload);
//             access_token = reslut["access_token"].ToStringInvariant();
            await ws.SendMessageAsync(new { jsonrpc = "2.0", id = 110, method = "public/auth",
                @params = new { grant_type = "client_credentials", client_id = PublicApiKey.ToUnsecureString(), client_secret=PrivateApiKey.ToUnsecureString() }});
            //Logger.Debug(reslut.ToString());
        }
        public async Task AuthAsync_2()
        {//client_id=fo7WAPRm4P&client_secret=W0H6FJW4IRPZ1MOQ8FP6KMC5RZDUUKXS&grant_type=client_credentials" \


            object ts = await GenerateNonceAsync();
            var StringToSign = ts + "\n" + ts + "\n" + "";
            var message = StringToSign;
            var signature = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString());
            var nonce = ts;
            var data = "";
//             Dictionary<string, object> payload = new Dictionary<string, object>
//             {
//                 { "jsonrpc" , "2.0"},
//                 { "id" , 110},
//                 { "method", "public/auth"},
//                     { "params", new { grant_type = "client_signature", client_id = PublicApiKey.ToUnsecureString(), timestamp=ts,signature = signature,nonce = nonce ,data = data} }
//             };
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["grant_type"] = "client_signature";
            payload["client_id"] = PublicApiKey.ToUnsecureString();
            payload["timestamp"] = ts;
            payload["signature"] = signature;
            //payload["nonce"] = nonce;
            //payload["data"] = data;
            JToken reslut = await GetHttpReponseAsync("https://testapp.deribit.com/api/v2" + $"/public/auth",  payload,"GET");
            access_token = reslut["access_token"].ToStringInvariant();

        }

        /// <summary>
        /// 获取http返回json
        /// </summary>
        private async Task<JToken> GetHttpReponseAsync(string fullUrl , Dictionary<string, object> payload, string method = "GET")
        {
            UriBuilder ub = new UriBuilder(fullUrl);
            if (method.Equals("GET"))
            {
                string msg = CryptoUtility.GetFormForPayload(payload, false, false, false);
                ub.Query = msg;
            }
            
            HttpWebRequest request = HttpWebRequest.Create(ub.Uri) as HttpWebRequest;
//             if (method.Equals("POST"))
//             {
// 
//                 payload.Remove("nonce");
//                 var msg = CryptoUtility.GetJsonForPayload(payload);
//                 await CryptoUtility.WriteToRequestAsync(request, msg);
//             }
            request.Headers["content-type"] = "application/json";
            request.KeepAlive = false;
            request.Method = method;

            HttpWebResponse response = null;
            string responseString = null;
            try
            {
                try
                {

                    response = await request.GetResponseAsync() as HttpWebResponse;
                    if (response == null)
                    {
                        throw new Exception("Unknown response from server");
                    }
                }
                catch (WebException we)
                {
                    response = we.Response as HttpWebResponse;
                    if (response == null)
                    {
                        throw new Exception(we.Message ?? "Unknown response from server");
                    }
                }
                using (Stream responseStream = response.GetResponseStream())
                {
                    responseString = new StreamReader(responseStream).ReadToEnd();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        // 404 maybe return empty responseString
                        if (string.IsNullOrWhiteSpace(responseString))
                        {
                            throw new Exception(string.Format("{0} - {1}",
                                response.StatusCode.ConvertInvariant<int>(), response.StatusCode));
                        }
                        throw new Exception(responseString);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ex.StackTrace);
                throw;
            }
            finally
            {
                response?.Dispose();
            }

            string stringResult = responseString;
            JToken jsonResult = JsonConvert.DeserializeObject<JToken>(stringResult);
            Console.WriteLine(jsonResult);
            return jsonResult;
        }

        private async Task<string> GeneratePayloadJSON(int authId)
        {

                        /*
            
        "params" : {
        "grant_type": "client_signature",
        "client_id": clientId,
        "timestamp": timestamp,
        "signature": signature,
        "nonce": nonce,
        "data": data
         
             */
             
            object ts = await GenerateNonceAsync();
            var StringToSign = ts + "\n" + ts + "\n" + "";
            var message = StringToSign;
            Logger.Debug("PrivateApiKey.ToUnsecureString():" + PrivateApiKey.ToUnsecureString());
            Logger.Debug("PublicApiKey.ToUnsecureString():" + PublicApiKey.ToUnsecureString());
            var signature = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString());
            var nonce = ts;
            var data = "";
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "jsonrpc" , "2.0"},
                { "id" , authId},
                { "method", "public/auth"},
                    { "params", new { grant_type = "client_signature", client_id = PublicApiKey.ToUnsecureString(), timestamp=ts,signature = signature,nonce = nonce ,data = data} }
            };
            string str = CryptoUtility.GetJsonForPayload(payload);
            Logger.Debug(" payload:"+str);
            return str;
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var m = await GetMarketSymbolsMetadataAsync();
            return m.Select(x => x.MarketSymbol);
        }


        protected override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            /*
             {{
  "symbol": ".XRPXBT",
  "rootSymbol": "XRP",
  "state": "Unlisted",
  "typ": "MRCXXX",
  "listing": null,
  "front": null,
  "expiry": null,
  "settle": null,
  "relistInterval": null,
  "inverseLeg": "",
  "sellLeg": "",
  "buyLeg": "",
  "optionStrikePcnt": null,
  "optionStrikeRound": null,
  "optionStrikePrice": null,
  "optionMultiplier": null,
  "positionCurrency": "",
  "underlying": "XRP",
  "quoteCurrency": "XBT",
  "underlyingSymbol": "XRPXBT=",
  "reference": "PLNX",
  "referenceSymbol": "BTC_XRP",
  "calcInterval": null,
  "publishInterval": "2000-01-01T00:01:00Z",
  "publishTime": null,
  "maxOrderQty": null,
  "maxPrice": null,
  "lotSize": null,
  "tickSize": 1E-08,
  "multiplier": null,
  "settlCurrency": "",
  "underlyingToPositionMultiplier": null,
  "underlyingToSettleMultiplier": null,
  "quoteToSettleMultiplier": null,
  "isQuanto": false,
  "isInverse": false,
  "initMargin": null,
  "maintMargin": null,
  "riskLimit": null,
  "riskStep": null,
  "limit": null,
  "capped": false,
  "taxed": false,
  "deleverage": false,
  "makerFee": null,
  "takerFee": null,
  "settlementFee": null,
  "insuranceFee": null,
  "fundingBaseSymbol": "",
  "fundingQuoteSymbol": "",
  "fundingPremiumSymbol": "",
  "fundingTimestamp": null,
  "fundingInterval": null,
  "fundingRate": null,
  "indicativeFundingRate": null,
  "rebalanceTimestamp": null,
  "rebalanceInterval": null,
  "openingTimestamp": null,
  "closingTimestamp": null,
  "sessionInterval": null,
  "prevClosePrice": null,
  "limitDownPrice": null,
  "limitUpPrice": null,
  "bankruptLimitDownPrice": null,
  "bankruptLimitUpPrice": null,
  "prevTotalVolume": null,
  "totalVolume": null,
  "volume": null,
  "volume24h": null,
  "prevTotalTurnover": null,
  "totalTurnover": null,
  "turnover": null,
  "turnover24h": null,
  "prevPrice24h": 7.425E-05,
  "vwap": null,
  "highPrice": null,
  "lowPrice": null,
  "lastPrice": 7.364E-05,
  "lastPriceProtected": null,
  "lastTickDirection": "MinusTick",
  "lastChangePcnt": -0.0082,
  "bidPrice": null,
  "midPrice": null,
  "askPrice": null,
  "impactBidPrice": null,
  "impactMidPrice": null,
  "impactAskPrice": null,
  "hasLiquidity": false,
  "openInterest": 0,
  "openValue": 0,
  "fairMethod": "",
  "fairBasisRate": null,
  "fairBasis": null,
  "fairPrice": null,
  "markMethod": "LastPrice",
  "markPrice": 7.364E-05,
  "indicativeTaxRate": null,
  "indicativeSettlePrice": null,
  "optionUnderlyingPrice": null,
  "settledPrice": null,
  "timestamp": "2018-07-05T13:27:15Z"
}}
             */

            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken allSymbols = await MakeJsonRequestAsync<JToken>("/instrument?count=500&reverse=false");
            foreach (JToken marketSymbolToken in allSymbols)
            {
                var market = new ExchangeMarket
                {
                    MarketSymbol = marketSymbolToken["symbol"].ToStringUpperInvariant(),
                    IsActive = marketSymbolToken["state"].ToStringInvariant().EqualsWithOption("Open"),
                    QuoteCurrency = marketSymbolToken["quoteCurrency"].ToStringUpperInvariant(),
                    BaseCurrency = marketSymbolToken["underlying"].ToStringUpperInvariant(),
                };

                try
                {
                    market.PriceStepSize = marketSymbolToken["tickSize"].ConvertInvariant<decimal>();
                    market.MaxPrice = marketSymbolToken["maxPrice"].ConvertInvariant<decimal>();
                    //market.MinPrice = symbol["minPrice"].ConvertInvariant<decimal>();

                    market.MaxTradeSize = marketSymbolToken["maxOrderQty"].ConvertInvariant<decimal>();
                    //market.MinTradeSize = symbol["minQty"].ConvertInvariant<decimal>();
                    //market.QuantityStepSize = symbol["stepSize"].ConvertInvariant<decimal>();
                }
                catch
                {

                }
                markets.Add(market);
            }
            return markets;
        }
        private Dictionary<string, JToken> tickerPairs = new Dictionary<string, JToken>();
        private Dictionary<string, JToken> orderPairs = new Dictionary<string, JToken>();
        protected override IWebSocket OnGetTickersWebSocket(Action<IReadOnlyCollection<KeyValuePair<string, ExchangeTicker>>> tickers, params string[] marketSymbols)
        {
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                if (token["error"] != null)
                {
                    Logger.Info(token["error"].ToStringInvariant());
                    return Task.CompletedTask;
                }
                else if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;
                foreach (var t in data)
                {
                    var marketSymbol = t["symbol"].ToStringInvariant();
                    JToken fullToken;
                    if (action == "partial")
                    {
                        tickerPairs[marketSymbol] = t;
                        fullToken = t;
                    }
                    else
                    {
                        fullToken = tickerPairs[marketSymbol];
                        foreach (var item in t)
                        {
                            if (item is JProperty)
                            {
                                var jp = (JProperty)item;
                                fullToken[jp.Name] = jp.Value;
                            }
                        }
                    }

                    tickers.Invoke(new List<KeyValuePair<string, ExchangeTicker>>
                    {
                        new KeyValuePair<string, ExchangeTicker>(marketSymbol, this.ParseTicker(fullToken, marketSymbol, "askPrice", "bidPrice", "lastPrice", "volume", null, "timestamp", TimestampType.Iso8601))
                    });
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
                    await _socket.SendMessageAsync(new { op = "subscribe", args = "instrument" });
                }
                else
                {
                    await _socket.SendMessageAsync(new { op = "subscribe", args = marketSymbols.Select(s => "instrument:" + this.NormalizeMarketSymbol(s)).ToArray() });
                }
            });
        }


        protected override IWebSocket OnGetPositionDetailsWebSocket(Action<ExchangeMarginPositionResult> callback)
        {
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {

                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                if (token["error"] != null)
                {
                    Logger.Info(token["error"].ToStringInvariant());
                    return Task.CompletedTask;
                }
                //{"success":true,"request":{"op":"authKeyExpires","args":["2xrwtDdMimp5Oi3F6oSmtsew",1552157533,"1665aedbd293e435fafbfaba2e5475f882bae9228bab0f29d9f3b5136d073294"]}}
                if (token["request"] != null && token["request"]["op"].ToStringInvariant() == "authKeyExpires")
                {
                    //{ "op": "subscribe", "args": ["order"]}
                    _socket.SendMessageAsync(new { op = "subscribe", args = "position" });
                    return Task.CompletedTask;
                }
                if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }
                //
                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;
                foreach (var t in data)
                {
                    var marketSymbol = t["symbol"].ToStringInvariant();
                    var position = ParsePosition(t);
                    callback(position);
                    //callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, t.ParseTrade("size", "price", "side", "timestamp", TimestampType.Iso8601, "trdMatchID")));

                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                var payloadJSON = GeneratePayloadJSON(111);
                await _socket.SendMessageAsync(payloadJSON.Result);
            });
        }
        public override string NormalizeMarketSymbol(string marketSymbol)
        {
            return marketSymbol;
        }

        private ExchangeMarginPositionResult ParsePosition(JToken t)
        {
            var result = new ExchangeMarginPositionResult
            {
                MarketSymbol = t["symbol"].ToStringInvariant(),
                Amount = t["homeNotional"].ConvertInvariant<decimal>(),
                Total = t["currentQty"].ConvertInvariant<decimal>(),
                LiquidationPrice = t["liquidationPrice"].ConvertInvariant<decimal>()
            };
            return result;
        }

        private Dictionary<string, ExchangeOrderResult> fullOrders = new Dictionary<string, ExchangeOrderResult>();
        #region WebSocket APIs
        protected override IWebSocket OnGetOrderDetailsWebSocket(Action<ExchangeOrderResult> callback)
        {
            //return base.OnGetOrderDetailsWebSocket(callback);

            return ConnectWebSocket(string.Empty, (_socket, msg) =>
             {

                 var str = msg.ToStringFromUTF8();
                 if(str.Equals("pong"))//心跳添加
                 {
                     callback(new ExchangeOrderResult() {MarketSymbol = str });
                 }
                 else
                 {
                     JToken token = JToken.Parse(str);

                     if (token["error"] != null)
                     {
                         Logger.Info(token["error"].ToStringInvariant());
                         return Task.CompletedTask;
                     }
                     //{"success":true,"request":{"op":"authKeyExpires","args":["2xrwtDdMimp5Oi3F6oSmtsew",1552157533,"1665aedbd293e435fafbfaba2e5475f882bae9228bab0f29d9f3b5136d073294"]}}
                     if (token["request"] != null && token["request"]["op"].ToStringInvariant() == "authKeyExpires")
                     {
                         //{ "op": "subscribe", "args": ["order"]}
                         _socket.SendMessageAsync(new { op = "subscribe", args = "order" });
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
                 var payloadJSON = GeneratePayloadJSON(111);
                 await _socket.SendMessageAsync(payloadJSON.Result);
             });

        }


        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] marketSymbols)
        {
            /*
{"table":"trade","action":"partial","keys":[],
"types":{"timestamp":"timestamp","symbol":"symbol","side":"symbol","size":"long","price":"float","tickDirection":"symbol","trdMatchID":"guid","grossValue":"long","homeNotional":"float","foreignNotional":"float"},
"foreignKeys":{"symbol":"instrument","side":"side"},
"attributes":{"timestamp":"sorted","symbol":"grouped"},
"filter":{"symbol":"XBTUSD"},
"data":[{"timestamp":"2018-07-06T08:31:53.333Z","symbol":"XBTUSD","side":"Buy","size":10000,"price":6520,"tickDirection":"PlusTick","trdMatchID":"a296312f-c9a4-e066-2f9e-7f4cf2751f0a","grossValue":153370000,"homeNotional":1.5337,"foreignNotional":10000}]}
             */

            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                if (token["error"] != null)
                {
                    Logger.Info(token["error"].ToStringInvariant());
                    return Task.CompletedTask;
                }
                else if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;
                foreach (var t in data)
                {
                    var marketSymbol = t["symbol"].ToStringInvariant();
                    callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, t.ParseTrade("size", "price", "side", "timestamp", TimestampType.Iso8601)));
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
                    await _socket.SendMessageAsync(new { op = "subscribe", args = "trade" });
                }
                else
                {
                    await _socket.SendMessageAsync(new { op = "subscribe", args = marketSymbols.Select(s => "trade:" + this.NormalizeMarketSymbol(s)).ToArray() });
                }
            });
        }

        private async Task DoWebSocket(IWebSocket socket, object obj)
        {
            bool can = true;
            socket.Connected +=  (m_socket) =>
            {
                can = false;
                return Task.CompletedTask;
            };
            while (can)
            {
                await socket.SendMessageAsync(obj);
                await Task.Delay(20);
            }
        }

        protected override IWebSocket OnGetOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            /*
{
  "params" : {
    "data" : {
      "timestamp" : 1554375447971,
      "instrument_name" : "ETH-PERPETUAL",
      "change_id" : 109615,
      "bids" : [
        [
          160,
          40
        ]
      ],
      "asks" : [
        [
          161,
          20
        ]
      ]
    },
    "channel" : "book.ETH-PERPETUAL.100.1.100ms"
  },
  "method" : "subscription",
  "jsonrpc" : "2.0"
}
             */

            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
            }
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);
               // Logger.Debug("token:"+token.ToString());
                if (token["params"] == null)
                {
                    return Task.CompletedTask;
                }
                if (token["params"].ToString().Equals("pong"))
                {
                    return Task.CompletedTask;
                }

                JArray bids = token["params"]["data"]["bids"] as JArray;
                JArray asks = token["params"]["data"]["asks"] as JArray;

                ExchangeOrderBook book = new ExchangeOrderBook();
                book.SequenceId = token["params"]["data"]["timestamp"].ConvertInvariant<long>();
                var price = 0m;
                var size = 0m;
                var marketSymbol = token["params"]["data"]["instrument_name"].ToStringInvariant();
                book.MarketSymbol = marketSymbol;
                void applyData(JArray data,bool isBuy)
                {
                    foreach (var d in data)
                    {
                        price = d[0].ConvertInvariant<decimal>();
                        size = d[1].ConvertInvariant<decimal>();
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
                if (!string.IsNullOrEmpty(book.MarketSymbol))
                {
                    callback(book);
                }
               
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                if (marketSymbols.Length == 0)
                {
                    marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                }
                List<object> objs = new List<object>();
                string group = "none";
                int depth = 10;
                string interval = "100ms";
                foreach (var symbol in marketSymbols)
                {
                    objs.Add($"book.{symbol}.{group}.{depth}.{interval}");
                }

                Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "jsonrpc" , "2.0"},
                    { "id" , 222},
                    { "method", "public/subscribe"},
                        { "params", new { channels = objs } }
                };

                await _socket.SendMessageAsync(payload);
                //DoWebSocket(_socket, payload);
            });
        }
        #endregion

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            /*
             [
{"timestamp":"2017-01-01T00:00:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.29,"low":968.29,"close":968.29,"trades":0,"volume":0,"vwap":null,"lastSize":null,"turnover":0,"homeNotional":0,"foreignNotional":0},
{"timestamp":"2017-01-01T00:01:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.76,"low":968.49,"close":968.7,"trades":17,"volume":12993,"vwap":968.72,"lastSize":2000,"turnover":1341256747,"homeNotional":13.412567469999997,"foreignNotional":12993},
             */

            List<MarketCandle> candles = new List<MarketCandle>();
            string periodString = PeriodSecondsToString(periodSeconds);
            string url = $"/trade/bucketed?binSize={periodString}&partial=false&symbol={marketSymbol}&reverse=true" + marketSymbol;
            if (startDate != null)
            {
                url += "&startTime=" + startDate.Value.ToString("yyyy-MM-dd");
            }
            if (endDate != null)
            {
                url += "&endTime=" + endDate.Value.ToString("yyyy-MM-dd");
            }
            if (limit != null)
            {
                url += "&count=" + (limit.Value.ToStringInvariant());
            }

            var obj = await MakeJsonRequestAsync<JToken>(url);
            foreach (var t in obj)
            {
                candles.Add(this.ParseCandle(t, marketSymbol, periodSeconds, "open", "high", "low", "close", "timestamp", TimestampType.Iso8601, "volume", "turnover", "vwap"));
            }
            candles.Reverse();

            return candles;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            /*
{[
  {
    "account": 93592,
    "currency": "XBt",
    "riskLimit": 1000000000000,
    "prevState": "",
    "state": "",
    "action": "",
    "amount": 141755795,
    "pendingCredit": 0,
    "pendingDebit": 0,
    "confirmedDebit": 0,
    "prevRealisedPnl": 0,
    "prevUnrealisedPnl": 0,
    "grossComm": 0,
    "grossOpenCost": 0,
    "grossOpenPremium": 0,
    "grossExecCost": 0,
    "grossMarkValue": 0,
    "riskValue": 0,
    "taxableMargin": 0,
    "initMargin": 0,
    "maintMargin": 0,
    "sessionMargin": 0,
    "targetExcessMargin": 0,
    "varMargin": 0,
    "realisedPnl": 0,
    "unrealisedPnl": 0,
    "indicativeTax": 0,
    "unrealisedProfit": 0,
    "syntheticMargin": 0,
    "walletBalance": 141755795,
    "marginBalance": 141755795,
    "marginBalancePcnt": 1,
    "marginLeverage": 0,
    "marginUsedPcnt": 0,
    "excessMargin": 141755795,
    "excessMarginPcnt": 1,
    "availableMargin": 141755795,
    "withdrawableMargin": 141755795,
    "timestamp": "2018-07-08T07:40:24.395Z",
    "grossLastValue": 0,
    "commission": null
  }
]}
             */


            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/margin?currency=all", BaseUrl, payload);
            foreach (var item in token)
            {
                var balance = item["marginBalance"].ConvertInvariant<decimal>();
                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }
            return amounts;
        }
        public override async Task<decimal> GetWalletSummaryAsync(string symbol)
        {
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/walletSummary?currency={symbol}", BaseUrl, payload);
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

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/margin?currency=all", BaseUrl, payload);
            foreach (var item in token)
            {
                var balance = item["availableMargin"].ConvertInvariant<decimal>();
                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }
            return amounts;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            //string query = "/order";
            string query = "/order?filter={\"ordType\":\"MarketIfTouched\"}";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                query += "&symbol=" + NormalizeMarketSymbol(marketSymbol);
            }
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }

            return orders;
        }
        /// <summary>
        /// 获取当前 止盈订单
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenProfitOrderDetailsAsync(string marketSymbol = null,OrderType orderType = OrderType.MarketIfTouched)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            //string query = "/order";
            string query = "/order?filter={\"ordType\":\"MarketIfTouched\"}";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                query += "&symbol=" + NormalizeMarketSymbol(marketSymbol);
            }
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }
            return orders;
        }
        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string query = $"/order?filter={{\"orderID\": \"{orderId}\"}}";
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }

            return orders[0];
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            if (orderId == "all")
            {
                if (marketSymbol != null)
                    payload["symbol"] = marketSymbol;
                JToken token = await MakeJsonRequestAsync<JToken>("/order/all", BaseUrl, payload, "DELETE");
            }
            else
            {
                payload["orderID"] = orderId;
                JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "DELETE");
            }
        }
        private IWebSocket placeWS;
        private JToken token = null;
        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {

            
            bool waitBack = false;
            int authId = 1111;
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            AddOrderToPayload(order, payload);
            string path = "private/" + (order.IsBuy ? "buy" : "sell");
            if (placeWS == null)
            {
                waitBack = true;
                ConnectWebSocket(string.Empty, (_socket, backMsg) =>
                {
                    var strs = backMsg.ToStringFromUTF8();
                    JToken tk = JToken.Parse(strs);
                    if (tk["id"].ConvertInvariant<int>() == authId)
                    {
                        waitBack = false;
                    }
                    else
                    {
                        token = tk;
                    }
                    Logger.Debug("tk:" + tk.ToString());
                    return Task.CompletedTask;
                }, async (_socket) =>
                {
                   
                    string str = await GeneratePayloadJSON(authId);
                    await _socket.SendMessageAsync(str);
                    placeWS = _socket;
                }, async (_socket) =>
                {
                    placeWS = null;
                });
            }
            //等待连接，然后发送信息
            Dictionary<string, object> msg = new Dictionary<string, object>
            {
                { "jsonrpc" , "2.0"},
                { "id" , 110},
                { "method", path},
                    { "params", payload }
            };
            while (true)
            {
                if (placeWS != null && waitBack == false)
                {
                    await placeWS.SendMessageAsync(msg);
                    break;
                }
                else
                    Task.Delay(100);
            }

            //JToken token = await MakeJsonRequestAsync<JToken>("/private/"+(order.IsBuy?"buy":"sell"), BaseUrl, payload, "POST");
            while (true)
            {
                if (token!=null)
                {
                    Logger.Debug(token.ToString());
                    break;
                }
                else
                    Task.Delay(100);
            }
            var res = token["result"];
            token = null;
            return ParseOrder(res);
        }

        private async Task<ExchangeOrderResult[]> mOnPlaceOrdersAsync(string protocol = "POST", params ExchangeOrderRequest[] orders)
        {
            List<ExchangeOrderResult> results = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            List<Dictionary<string, object>> orderRequests = new List<Dictionary<string, object>>();
            foreach (ExchangeOrderRequest order in orders)
            {
                Dictionary<string, object> subPayload = new Dictionary<string, object>();
                AddOrderToPayload(order, subPayload);
                orderRequests.Add(subPayload);
            }
            payload["orders"] = orderRequests;

            JToken token = await MakeJsonRequestAsync<JToken>("/order/bulk", BaseUrl, payload, protocol);
            foreach (JToken orderResultToken in token)
            {
                results.Add(ParseOrder(orderResultToken));
            }
            return results.ToArray();
        }

        protected override async Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] orders)
        {
            List<ExchangeOrderResult> results = new List<ExchangeOrderResult>();
            var postOrderRequests = new List<ExchangeOrderRequest>();
            var putOrderRequests = new List<ExchangeOrderRequest>();
            foreach (ExchangeOrderRequest order in orders)
            {
                if (order.ExtraParameters.ContainsKey("orderID"))
                {
                    putOrderRequests.Add(order);
                }
                else
                {
                    postOrderRequests.Add(order);
                }
            }
            if (putOrderRequests.Count > 0)
                results.AddRange(await mOnPlaceOrdersAsync("PUT", putOrderRequests.ToArray()));
            if (postOrderRequests.Count > 0)
                results.AddRange(await mOnPlaceOrdersAsync("POST", postOrderRequests.ToArray()));
            return results.ToArray();
        }
        private IWebSocket positionWS = null;
        JToken positionToken = null;
        public override async Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string marketSymbol)
        {
            /*
             * [
{
  "jsonrpc": "2.0",
  "id": 404,
  "result": {
      "average_price": 0,
      "delta": 0,
      "direction": "buy",
      "estimated_liquidation_price": 0,
      "floating_profit_loss": 0,
      "index_price": 3555.86,
      "initial_margin": 0,
      "instrument_name": "BTC-PERPETUAL",
      "kind": "future",
      "maintenance_margin": 0,
      "mark_price": 3556.62,
      "open_orders_margin": 0.000165889,
      "realized_profit_loss": 0,
      "settlement_price": 3555.44,
      "size": 0,
      "size_currency": 0,
      "total_profit_loss": 0
  }
}

            {"jsonrpc":"2.0","id":404,"result":{"token_type":"bearer","scope":"account:read connection mainaccount trade:read_write wallet:read_write","refresh_token":"1596533860280.1DESk5_4.XSlR4QbMM7XvSu5janAa8BLSCBXv6UK1fmfUZvPGnjlpXvFZ8A5syZafwPsQuDXB3-n0x5hea11nQVYwi9f1JgktYc_Y4RDWzRK3XlRMrO95m7RHuopbZEnmoG_88mC8TVOa2P2n1h-D2erg4K8Wyks9_X6jbk8xV0dU91acIZsZGvKTQYHYnv5rtNZGsgZ_4l4j2VyxF2Xt4Ew6uiPmwnvd03g4-iCzgl_l1SGE5RlN98AuUDdpFm8g-jC5zVEuNYcQ8K23_JE","expires_in":31536000,"access_token":"1596533860280.1PxgP-KX.eZLz0-Ps8Khfq3heb8co8mA88TtOQGeU9phYecDRKPuPzfdfa_cq-6wMnaz-otq0JK5-Qh-5VZMlgyVLf6pVhZQ0dbig2i6ne3ia_Uf2LfT4-3vjFARX3hYLVEEJYBeRI3rx7G7smliZdpMaYv4IL38Y8G2wrPGCkbyyyCuitA3NK5fHqQ7YP3WQHPVNxLPhEw3v12lGOxC2d_xfaEdmd7nr0Fl-roFk09OrVx_CsBOIZXcfxiu-tZN_eNxwjGZ_9u3QRlFIAa76"},"usIn":1564997860280415,"usOut":1564997860280822,"usDiff":407,"testnet":true}
]*/
            ExchangeMarginPositionResult poitionR = null;

            /*
{
"jsonrpc": "2.0",
"id": 111,
"result": {
"total_profit_loss": 0.0,
"size": 0.0,
"settlement_price": 0.34106716,
"realized_profit_loss": 0.0,
"open_orders_margin": 0.0,
"mark_price": 0.0,
"maintenance_margin": 0.0,
"kind": "option",
"instrument_name": "BTC-27SEP19-8500-C",
"initial_margin": 0.0,
"index_price": 11706.78,
"floating_profit_loss_usd": 0.0,
"floating_profit_loss": 0.0,
"direction": "zero",
"delta": 0.0,
"average_price_usd": 0.0,
"average_price": 0.0
},
"usIn": 1564998348800707,
"usOut": 1564998348801187,
"usDiff": 480,
"testnet": true
}*/
            
            bool waitBack = false;
            int authId = 1112;
            //Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string path = "private/get_position";
            if (positionWS == null)
            {
                waitBack = true;
                ConnectWebSocket(string.Empty, (_socket, backMsg) =>
                {
                    var strs = backMsg.ToStringFromUTF8();
                    JToken tk = JToken.Parse(strs);
                    if (tk["id"].ConvertInvariant<int>() == authId)
                    {
                        waitBack = false;
                    }
                    else
                    {
                         //waitBack = false;
                         positionToken = tk;
                    }
                    Logger.Debug("tk:" + tk.ToString());
                    return Task.CompletedTask;
                }, async (_socket) =>
                {

                    string str = await GeneratePayloadJSON(authId);
                    await _socket.SendMessageAsync(str);
                    positionWS = _socket;
                }, async (_socket) =>
                {
                    positionWS = null;
                });
            }
            //等待连接，然后发送信息
            Dictionary<string, object> msg = new Dictionary<string, object>
            {
                { "jsonrpc" , "2.0"},
                { "id" , 110},
                { "method", path},
                    { "params", new { instrument_name = marketSymbol} }
            };
            while (true)
            {
                if (positionWS != null && waitBack == false)
                {
                    await positionWS.SendMessageAsync(msg);
                    break;
                }
                else
                    Task.Delay(100);
            }

            //JToken token = await MakeJsonRequestAsync<JToken>("/private/"+(order.IsBuy?"buy":"sell"), BaseUrl, payload, "POST");
            while (true)
            {
                if (positionToken != null)
                {
                    Logger.Debug(positionToken.ToString());
                    break;
                }
                else
                    Task.Delay(100);
            }

            JToken position = positionToken["result"];
            poitionR = new ExchangeMarginPositionResult()
            {
                MarketSymbol = marketSymbol,
                Amount = position["size"].ConvertInvariant<decimal>(),
                LiquidationPrice = position["estimated_liquidation_price"].ConvertInvariant<decimal>(),
                BasePrice = position["average_price"].ConvertInvariant<decimal>(),
            };
            positionToken = null;
            return poitionR;
        }
        /*                string fullUrl = BaseUrlWebSocket;
                ExchangeSharp.ClientWebSocket wrapper = new ExchangeSharp.ClientWebSocket
                {
                    Uri = new Uri(fullUrl),
                    OnBinaryMessage = (_socket, backMsg) =>
                    {
                        var strs = backMsg.ToStringFromUTF8();
                        JToken tk = JToken.Parse(strs);
                        if (tk["id"].ConvertInvariant<int>() == authId)
                        {
                            waitBack = false;
                        }
                        else
                        {
                            //waitBack = false;
                            token = tk;
                        }
                        Logger.Debug("tk:" + tk.ToString());
                        return Task.CompletedTask;
                    }
                };
                {
                    wrapper.Connected += async (_socket) =>
                    {

                        string str = await GeneratePayloadJSON(authId);
                        await _socket.SendMessageAsync(str);
                        positionWS = _socket;
                    };
                }

                {
                    wrapper.Disconnected += async (_socket) =>
                    {
                        positionWS = null;
                    };
                }
                wrapper.Start();
         */


        private string GetOrderType(OrderType orderType)
        {
            switch (orderType)
            {
                case OrderType.Limit:
                    return "limit";
                    break;
                case OrderType.Market:
                    return "market";
                    break;
                case OrderType.MarketIfTouched:
                    return "stop_market";
                    break;
                case OrderType.Stop:
                    return "stop_limit";
                    break;
                default:
                    break;
            }
            return "";
        }
        private void AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            payload["instrument_name"] = order.MarketSymbol;
            payload["amount"] = order.Amount;
            payload["type"] = GetOrderType(order.OrderType);
            
            if (order.Price != 0)
                payload["price"] = order.Price;
            if (order.StopPrice != 0)
                payload["stop_price"] = order.StopPrice;
//             if (order.ExtraParameters.TryGetValue("execInst", out var execInst))
//             {
//                 payload["execInst"] = execInst;
//             }

        }

        private ExchangeOrderResult ParseOrder(JToken resultJs)
        {            /*
             {
  "jsonrpc": "2.0",
  "id": 5275,
  "result": {
    "trades": [
      {
        "trade_seq": 14151,
        "trade_id": "ETH-37435",
        "timestamp": 1550657341322,
        "tick_direction": 2,
        "state": "closed",
        "self_trade": false,
        "price": 143.81,
        "order_type": "market",
        "order_id": "ETH-349249",
        "matching_id": null,
        "liquidity": "T",
        "label": "market0000234",
        "instrument_name": "ETH-PERPETUAL",
        "index_price": 143.73,
        "fee_currency": "ETH",
        "fee": 0.000139,
        "direction": "buy",
        "amount": 40
      }
    ],
    "order": {
      "time_in_force": "good_til_cancelled",
      "reduce_only": false,
      "profit_loss": 0,
      "price": "market_price",
      "post_only": false,
      "order_type": "market",
      "order_state": "filled",
      "order_id": "ETH-349249",
      "max_show": 40,
      "last_update_timestamp": 1550657341322,
      "label": "market0000234",
      "is_liquidation": false,
      "instrument_name": "ETH-PERPETUAL",
      "filled_amount": 40,
      "direction": "buy",
      "creation_timestamp": 1550657341322,
      "commission": 0.000139,
      "average_price": 143.81,
      "api": true,
      "amount": 40
    }
  }
}      
             */
            ExchangeOrderResult fullOrder;
            Logger.Debug("resultJs:" + resultJs);
            JToken token = resultJs["order"];
            bool had = fullOrders.TryGetValue(token["order_id"].ToStringInvariant(), out fullOrder);


            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = token["amount"].ConvertInvariant<decimal>(),
                AmountFilled = token["filled_amount"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                IsBuy = token["direction"].ToStringInvariant().EqualsWithOption("buy"),
               
                OrderId = token["order_id"].ToStringInvariant(),
                MarketSymbol = token["instrument_name"].ToStringInvariant(),
                AveragePrice = token["average_price"].ConvertInvariant<decimal>(),
            };
            long timeStamp = token["last_update_timestamp"].ConvertInvariant<long>();
            DateTime dtStart = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            long lTime = long.Parse(timeStamp + "0000");
            TimeSpan toNow = new TimeSpan(lTime);
            result.OrderDate = dtStart.AddTicks(lTime);


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


            // http://www.onixs.biz/fix-dictionary/5.0.SP2/tagNum_39.html
            switch (token["state"].ToStringInvariant())
            {
                case "open":
                    result.Result = ExchangeAPIOrderResult.Pending;
                    //Logger.Info("1ExchangeAPIOrderResult.Pending:" + token.ToString());
                    break;
                case "PartiallyFilled":
                    result.Result = ExchangeAPIOrderResult.FilledPartially;
                    //Logger.Info("2ExchangeAPIOrderResult.FilledPartially:" + token.ToString());
                    break;
                case "filled":
                    result.Result = ExchangeAPIOrderResult.Filled;
                    //Logger.Info("3ExchangeAPIOrderResult.Filled:" + token.ToString());
                    break;
                case "cancelled":
                    result.Result = ExchangeAPIOrderResult.Canceled;
                    //Logger.Info("4ExchangeAPIOrderResult.Canceled:" + token.ToString());
                    break;
                case "rejected":
                    result.Result = ExchangeAPIOrderResult.Canceled;
                    //Logger.Info("4ExchangeAPIOrderResult.Canceled:" + token.ToString());
                    break;
                default:
                    result.Result = ExchangeAPIOrderResult.Error;
                    //Logger.Info("5ExchangeAPIOrderResult.Error:" + token.ToString());
                    break;
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
            if(result==null)
            {
                Logger.Error("ExchangeAPIOrderResult:" + token.ToString());
                return fullOrder;
            }
            else
            {
                fullOrders[result.OrderId] = fullOrder;
                return fullOrder;
            }

        }


        //private decimal GetInstrumentTickSize(ExchangeMarket market)
        //{
        //    if (market.MarketName == "XBTUSD")
        //    {
        //        return 0.01m;
        //    }
        //    return market.PriceStepSize.Value;
        //}

        //private ExchangeMarket GetMarket(string symbol)
        //{
        //    var m = GetSymbolsMetadata();
        //    return m.Where(x => x.MarketName == symbol).First();
        //}

        //private decimal GetPriceFromID(long id, ExchangeMarket market)
        //{
        //    return ((100000000L * market.Idx) - id) * GetInstrumentTickSize(market);
        //}

        //private long GetIDFromPrice(decimal price, ExchangeMarket market)
        //{
        //    return (long)((100000000L * market.Idx) - (price / GetInstrumentTickSize(market)));
        //}
    }

    public partial class ExchangeName { public const string Deribit = "Deribit"; }
}
