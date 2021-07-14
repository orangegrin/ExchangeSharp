/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeJaexAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://api.jaex.co";
        public override string BaseUrlWebSocket { get; set; } = "wss://wsapi.jaex.co/openapi/quote/ws/v1";
//         public override string BaseUrl { get; set; } = "https://testnet.bitmex.com/api/v1";
//         public override string BaseUrlWebSocket { get; set; } = "wss://testnet.bitmex.com/realtime";

        private SortedDictionary<long, decimal> dict_long_decimal = new SortedDictionary<long, decimal>();
        private SortedDictionary<decimal, long> dict_decimal_long = new SortedDictionary<decimal, long>();
        private string OrderIdStart;
        private int OrderNum;
        
        public ExchangeJaexAPI()
        {
            RequestWindow = TimeSpan.FromSeconds(20);
            NonceStyle = NonceStyle.ExpiresUnixMilliseconds;

            // make the nonce go 10 seconds into the future (the offset is subtracted)
            // this will give us an api-expires 60 seconds into the future
            NonceOffset = TimeSpan.Zero;

            MarketSymbolSeparator = string.Empty;
            RequestContentType = "application/x-www-form-urlencoded";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;

            RateLimit = new RateGate(300, TimeSpan.FromMinutes(5));
            OrderIdStart = (long)CryptoUtility.UtcNow.UnixTimestampFromDateTimeMilliseconds()+"_";
            OrderNum = 0;
        }
        /// <summary>
        /// 返回 客户端id
        /// </summary>
        /// <returns></returns>
        private string GetClinetOrderID()
        {
            lock(OrderIdStart)
            {
                Random r = new Random();
                return OrderIdStart +"_"+r.Next(111111,999999) + (OrderNum++);
            }
        }
        public override string ExchangeMarketSymbolToGlobalMarketSymbol(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        public override string GlobalMarketSymbolToExchangeMarketSymbol(string marketSymbol)
        {
            throw new NotImplementedException();
        }
        public override string NormalizeMarketSymbol(string marketSymbol)
        {
            return marketSymbol;
        }
        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // convert nonce to long, trim off milliseconds
                var nonce = payload["nonce"].ConvertInvariant<long>();
                payload.Remove("nonce");
                payload.Add("timestamp", nonce);
                if (request.Method == "GET")
                {
                    payload.Remove("timestamp");
                }
                var msg = CryptoUtility.GetFormForPayload(payload, false, true, true);
                var sign = "";
                if (request.Method == "POST")
                {
                    sign = request.RequestUri.Query + msg;
                }
                else if (request.Method == "GET")
                {
                    string query = request.RequestUri.Query.Replace("?", "");
                    sign = query;
                }
                else
                {
                    sign = request.RequestUri.Query + msg;
                }
                //TEST
                //msg = "direction=ASK&price=7126.4285&symbol=BTC_USDT&volume=0.12";

               // var sign = request.RequestUri.Query + msg;//$"{nonce}{request.Method}{request.RequestUri.AbsolutePath}{request.RequestUri.Query}{msg}";
                Logger.Debug(" PrivateApiKey:"+PrivateApiKey.ToUnsecureString());
                Logger.Debug(" PublicApiKey:" + PublicApiKey.ToUnsecureString());
                string signature = CryptoUtility.SHA256Sign(sign, PrivateApiKey.ToUnsecureString());//CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));
                //Logger.Debug("sign :" + sign);
                //Logger.Debug("signature :" + signature);
                if (request.Method == "GET")
                {

                }
                else
                {
                    msg += "&signature=" + signature;
                }
               
                //SIGNED（需要签名）的端点需要发送一个参数，signature，在query string 或者 request
                // Logger.Debug(PublicApiKey.ToUnsecureString());
                request.AddHeader("X-BH-APIKEY", PublicApiKey.ToUnsecureString());
                //request.AddHeader("X_SIGNATURE", signature);

                //                 if (!string.IsNullOrEmpty(SubAccount))
                //                 {
                //                     request.AddHeader("FTX-SUBACCOUNT", SubAccount);
                //                 }
                await CryptoUtility.WriteToRequestAsync(request, msg);
                //await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
            }
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // payload is ignored, except for the nonce which is added to the url query - bittrex puts all the "post" parameters in the url query instead of the request body
                if (method == "GET")
                {
                    var query = (url.Query ?? string.Empty).Trim('?', '&');
                    string newQuery = "timestamp=" + payload["nonce"].ToStringInvariant() + (query.Length != 0 ? "&" + query : string.Empty) +
                        (payload.Count > 1 ? "&" + CryptoUtility.GetFormForPayload(payload, false) : string.Empty);
                    string signature = CryptoUtility.SHA256Sign(newQuery, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));
                    newQuery += "&signature=" + signature;
                    url.Query = newQuery;
                    return url.Uri;
                }
            }
            return base.ProcessRequestUrl(url, payload, method);
        }
        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var m = await GetMarketSymbolsMetadataAsync();
            return m.Select(x => x.MarketSymbol);
        }

        protected override async Task<IEnumerable<ExchangeTrade>> OnGetRecentTradesAsync(string marketSymbol)
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JToken token = await MakeJsonRequestAsync<JToken>($"/openapi/quote/v1/trades?symbol={marketSymbol}&limit=1", BaseUrl);
            List<ExchangeTrade> tardes = new List<ExchangeTrade>();
            foreach (var item in token)
            {
                ExchangeTrade trade = new ExchangeTrade()
                {
                    Amount = item["qty"].ConvertInvariant<decimal>(),
                    IsBuy = item["isBuyerMaker"].ConvertInvariant<string>().Equals("true"),
                    Price = item["price"].ConvertInvariant<decimal>()
                };
                tardes.Add(trade);
            }
            return tardes;
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
                var payloadJSON = GeneratePayloadJSON();
                await _socket.SendMessageAsync(payloadJSON.Result);
            });
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
            Timer pingTimer = null;
            //return base.OnGetOrderDetailsWebSocket(callback);

            return ConnectWebSocket(string.Empty, (_socket, msg) =>
             {

                 var str = msg.ToStringFromUTF8();
                 if(str.Contains("pong"))//心跳添加
                 {
                     callback(new ExchangeOrderResult() {MarketSymbol = "pong" });
                 }
                 else
                 {
                     JToken token = JToken.Parse(str);
                     //Logger.Debug(token.ToString());

                     if (token["error"] != null)
                     {
                         Logger.Info(token["error"].ToStringInvariant());
                         return Task.CompletedTask;
                     }
                     //{"success":true,"request":{"op":"authKeyExpires","args":["2xrwtDdMimp5Oi3F6oSmtsew",1552157533,"1665aedbd293e435fafbfaba2e5475f882bae9228bab0f29d9f3b5136d073294"]}}
                     if (token["type"] != null && token["type"].ToStringInvariant() == "subscribed")
                     {  // subscription successful
                         if (pingTimer == null)
                         {
                             pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync(new { op = "ping" }),
                                 state: null, dueTime: 0, period: 15000); // send a ping every 15 seconds
                         }
                         return Task.CompletedTask;
                     }
                     if (token["data"] == null)
                     {
                         return Task.CompletedTask;
                     }
                     //{ "table":"order","action":"insert","data":[{ "orderID":"b48f4eea-5320-cc06-68f3-d80d60896e31","clOrdID":"","clOrdLinkID":"","account":954891,"symbol":"XBTUSD","side":"Buy","simpleOrderQty":null,"orderQty":100,"price":3850,"displayQty":null,"stopPx":null,"pegOffsetValue":null,"pegPriceType":"","currency":"USD","settlCurrency":"XBt","ordType":"Limit","timeInForce":"GoodTillCancel","execInst":"ParticipateDoNotInitiate","contingencyType":"","exDestination":"XBME","ordStatus":"New","triggered":"","workingIndicator":false,"ordRejReason":"","simpleLeavesQty":null,"leavesQty":100,"simpleCumQty":null,"cumQty":0,"avgPx":null,"multiLegReportingType":"SingleSecurity","text":"Submission from www.bitmex.com","transactTime":"2019-03-09T19:24:21.789Z","timestamp":"2019-03-09T19:24:21.789Z"}]}
                     var action = token["channel"].ToStringInvariant();
                     var data = token["data"];
                     //foreach (var t in data)
                     {
                         var marketSymbol = data["market"].ToStringInvariant();
                         var order = ParseOrder(data);
                         callback(order);
                         //callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, t.ParseTrade("size", "price", "side", "timestamp", TimestampType.Iso8601, "trdMatchID")));

                     }
                 }
                 return Task.CompletedTask;
             }, async (_socket) =>
             {
                 //连接中断也不应该删除历史信息
                 //fullOrders.Clear();
                 var payloadJSON = await GeneratePayloadJSON();
                 //Logger.Debug(payloadJSON.ToString());

                 await _socket.SendMessageAsync(payloadJSON);
                 await _socket.SendMessageAsync(new { op = "subscribe", channel = "orders" });
             },async(_socket) =>
             {
                 pingTimer.Dispose();
                 pingTimer = null;
                 
             });

        }

        private async Task<string> GeneratePayloadJSON()
        {
            object expires = await GenerateNonceAsync();
            var privateKey = PrivateApiKey.ToUnsecureString();


            //expires = 1557246346499;
            //privateKey = "Y2QTHI23f23f23jfjas23f23To0RfUwX3H42fvN-";


            var message = expires + "websocket_login";
            var signature = CryptoUtility.SHA256Sign(message, privateKey);
            Dictionary<string, object> payload;
            if (!string.IsNullOrEmpty(SubAccount))
            {
                payload = new Dictionary<string, object>
                {
                    { "args", new { key = PublicApiKey.ToUnsecureString() ,sign = signature ,time = expires,subaccount=SubAccount } },
                     { "op", "login"}
                };
            }
            else
            {
                payload = new Dictionary<string, object>
                {
                    { "args", new { key = PublicApiKey.ToUnsecureString() ,sign = signature ,time = expires } },
                     { "op", "login"}
                };
            }
            
            return CryptoUtility.GetJsonForPayload(payload);
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

        protected override IWebSocket OnGetOrderBookWebSocket(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            /*
{
  "symbol": "BTCUSDT",
  "symbolName": "BTCUSDT",
  "topic": "depth",
  "params": {
    "realtimeInterval": "24h"
  },
  "data": [
    {
      "e": 301,
      "s": "BTCUSDT",
      "t": 1607501473158,
      "v": "844239040_18",
      "b": [
        [
          "17963",
          "18.071772"
        ],
        [
          "17962.96",
          "0.001"
        ]],
        "a": [
        [
          "17963",
          "18.071772"
        ],
        [
          "17962.96",
          "0.001"
        ]],
             */
            Timer pingTimer = null;
            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
            }
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                //                 if (token["table"] == null)
                //                 {
                //                     return Task.CompletedTask;
                //                 }
                //Logger.Debug(token.ToString());
                if (token.ToString().Contains("pong"))
                {
                    Logger.Debug("pong");
                    return Task.CompletedTask;
                }

                //var action = token["type"].ToStringInvariant();
                //if (token["type"] != null && token["type"].ToStringInvariant() == "subscribed")
                {  // subscription successful
                    if (pingTimer == null)
                    {
                        pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync(new { ping = 123 }),
                            state: null, dueTime: 0, period: 15000); // send a ping every 15 seconds
                    }
                }
                JArray bids = null;
                JArray asks = null;
                var data = token["data"][0];
                if (data["b"]!=null)
                {
                    bids = data["b"] as JArray;
                }
                if (data["a"] != null)
                {
                    asks = data["a"] as JArray;
                }
                ExchangeOrderBook book = new ExchangeOrderBook();
                book.SequenceId = data["t"].ConvertInvariant<long>();
                var price = 0m;
                var size = 0m;
                var marketSymbol = token["symbol"].ToStringInvariant();
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
                if (data["b"] != null)
                {
                    applyData(bids, true);
                }
                if (data["a"] != null)
                {
                    applyData(asks, false);
                }
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
                await _socket.SendMessageAsync(new { symbol = this.NormalizeMarketSymbol(marketSymbols[0]), topic = "depth", @event = "sub" , });
            }, async (_socket) =>
            {
                pingTimer.Dispose();
                pingTimer = null;

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
            /*
             {
  "canTrade": true,
  "canWithdraw": true,
  "canDeposit": true,
  "updateTime": 123456789,
  "balances": [
    {
      "asset": "BTC",
      "free": "4723846.89208129",
      "locked": "0.00000000"
    },
    {
      "asset": "LTC",
      "free": "4763368.68006011",
      "locked": "0.00000000"
            .
    }
  ]
}
             */
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/openapi/v1/account", BaseUrl, payload);
            var balances = token["balances"];
            decimal totalAmount = 0;
            Logger.Debug(token.ToString());
            if ( string.IsNullOrEmpty(symbol))//获取全部
            {
                foreach (var item in balances)
                {
                    var coin = item["asset"].ToStringInvariant();
                    var count = item["free"].ConvertInvariant<decimal>()+ item["locked"].ConvertInvariant<decimal>();
                    totalAmount += count;
                }
            }
            else
            {
                foreach (var item in balances)
                {
                    var coin = item["asset"].ToStringInvariant();
                    var count = item["free"].ConvertInvariant<decimal>() + item["locked"].ConvertInvariant<decimal>();
                    if (coin.Equals(symbol))
                    {
                        totalAmount = count;
                    }
                    
                }
            }
            return totalAmount;
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
            if (marketSymbol!=null)
            {
                payload.Add("symbol", NormalizeMarketSymbol(marketSymbol));
            }
            string query = $"/openapi/v1/openOrders?" ;
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
               // Logger.Debug(token.ToString());
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
            string query = "/conditional_orders?";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                query += "market=" + NormalizeMarketSymbol(marketSymbol);
            }
            string orderStr = "";
            if (orderType == OrderType.Stop || orderType == OrderType.StopLimit)
            {
                orderStr = "stop";
            }
            else if(orderType == OrderType.LimitIfTouched || orderType == OrderType.MarketIfTouched)
            {
                orderStr = "take_profit";
            }
            query += "&type=" + orderStr;
            
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
            ExchangeOrderResult result;
            JToken token;
            if (orderId == "all")
            {
                if (marketSymbol != null)
                    payload["symbol"] = marketSymbol;
                token = await MakeJsonRequestAsync<JToken>("/orders", BaseUrl, payload, "DELETE");
                
            }
            else
            {
                if (orderId.Contains("_"))//删除客户端 订单
                {
                    //payload["orderID"] = orderId;
                    token = await MakeJsonRequestAsync<JToken>("/orders/by_client_id/"+ orderId, BaseUrl, payload, "DELETE");
                   
                }
                else
                {
                    payload["orderId"] = orderId;
                    token = await MakeJsonRequestAsync<JToken>("/openapi/v1/order", BaseUrl, payload, "DELETE");
                    
                }
            }
            try
            {
                result = ParseOrder(token);
//                 if (token["success"].Equals("false"))
//                 {
//                     throw new Exception("CancelOrderEx:"+ token.ToString());
//                 }
            }
            catch (Exception ex)
            {

                throw new Exception("CancelOrderEx:" + token.ToString());
            }


        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            //payload.Remove("nonce");
            JToken token;
//             if (order.ExtraParameters.TryGetValue("orderID", out var orderID))
//             {
//                 Logger.Debug("change price:"+ orderID);
//                 payload["price"] = order.Price;
//                 payload["size"] = order.Amount;
//                 //payload["clientId"] = orderID;
//                 token = await MakeJsonRequestAsync<JToken>("/orders/by_client_id/"+ orderID+"/modify", BaseUrl, payload, "POST");
//             }
//             else
            
                AddOrderToPayload(order, payload);
           
            token = await MakeJsonRequestAsync<JToken>("/openapi/v1/order", BaseUrl, payload, "POST");
            //token = await MakeJsonRequestAsync<JToken>("openapi/v1/order/test", BaseUrl, payload, "POST");

            return ParseOrder(token);
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

        public override async Task<ExchangeMarginPositionResult> GetOpenPositionAsync(string marketSymbol)
        {
            /*
   
{
{{
  "collateralUsed": 49.51748,
  "cost": 247.5874,
  "entryPrice": 117.34,
  "estimatedLiquidationPrice": 1.4127234583184682,
  "future": "ETH-PERP",
  "initialMarginRequirement": 0.2,
  "longOrderSize": 0.0,
  "maintenanceMarginRequirement": 0.03,
  "netSize": 2.11,
  "openSize": 2.11,
  "realizedPnl": -0.17876023,
  "shortOrderSize": 0.0,
  "side": "buy",
  "size": 2.11,
  "unrealizedPnl": 0.0
}}

            {[
  {
    "collateralUsed": 0.0,
    "cost": 0.0,
    "entryPrice": null,
    "estimatedLiquidationPrice": null,
    "future": "ETH-PERP",
    "initialMarginRequirement": 0.1,
    "longOrderSize": 0.0,
    "maintenanceMarginRequirement": 0.03,
    "netSize": 0.0,
    "openSize": 0.0,
    "realizedPnl": -1.01902667,
    "shortOrderSize": 0.0,
    "side": "buy",
    "size": 0.0,
    "unrealizedPnl": 0.0
  },
  {
    "collateralUsed": 89.23365,
    "cost": 892.34453103,
    "entryPrice": 1.0015090135016835,
    "estimatedLiquidationPrice": 0.70704042966209524,
    "future": "USDT-PERP",
    "initialMarginRequirement": 0.1,
    "longOrderSize": 0.0,
    "maintenanceMarginRequirement": 0.03,
    "netSize": 891.0,
    "openSize": 891.0,
    "realizedPnl": -0.74205564,
    "shortOrderSize": 1774.0,
    "side": "buy",
    "size": 891.0,
    "unrealizedPnl": -0.00803103
  }
]}
*/
            ExchangeMarginPositionResult poitionR = null;
            var payload = await GetNoncePayloadAsync();
            //payload["showAvgPrice"] = true;
            JToken token = await MakeJsonRequestAsync<JToken>($"/positions", BaseUrl, payload,"GET");
            foreach (JToken position in token)
            {
                if (position["future"].ToStringInvariant().Equals(marketSymbol))
                {
                    poitionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = position["netSize"].ConvertInvariant<decimal>(),
                        LiquidationPrice = position["estimatedLiquidationPrice"].ConvertInvariant<decimal>(),
                        
                    };
                    var cost = Math.Abs(position["cost"].ConvertInvariant<decimal>());
                    poitionR.BasePrice = poitionR.Amount == 0 ? 0: cost / poitionR.Amount;
                }
            }
            return poitionR;
        }
        private void AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            payload["symbol"] = order.MarketSymbol;
            
            payload["side"] = order.IsBuy ? "BUY" : "SELL";
            payload["type"] = order.OrderType.ToStringLowerInvariant().ToUpper();
            payload["quantity"] = order.Amount;
            if (order.OrderType == OrderType.Limit)
            {
                payload["price"] = order.Price;
                payload["timeInForce"] = "GTC";
                //timeInForce
            }
            if (order.StopPrice != 0)
            {
                payload["type"] = "STOP_LIMIT";
                payload["stopPrice"] = order.StopPrice;
            }


            //payload["displayQty"] = 0;//隐藏订单

            //payload["ioc"] = true;//值全部成交

            if (order.ExtraParameters.TryGetValue("timeInForce", out var execInst))
            {
                payload["timeInForce"] = execInst;
            }



        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            /*
{
     "accountId": "778061389819282689",
  "symbol": "BTCUSDT",
  "symbolName": "BTCUSDT",
  "clientOrderId": "160758701029755",
  "orderId": "778890727812789762",
  "transactTime": "1607587010302",
  "price": "10000",
  "origQty": "0.0005",
  "executedQty": "0",
  "status": "NEW",
  "timeInForce": "GTC",
  "type": "LIMIT",
  "side": "BUY"
  
}
            */

            Logger.Debug("ParseOrder:" + token.ToString());
            ExchangeOrderResult fullOrder;
            lock (fullOrders)
            {
                
                bool had = fullOrders.TryGetValue(token["orderId"].ToStringInvariant(), out fullOrder);
                ExchangeOrderResult result = new ExchangeOrderResult()
                {
                    Amount = token["origQty"].ConvertInvariant<decimal>(),
                    AmountFilled = token["executedQty"].ConvertInvariant<decimal>(),
                    Price = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = token["side"].ToStringInvariant().EqualsWithOption("BUY"),
                    OrderDate = token["transactTime"].ConvertInvariant<long>().ToDateTime(),
                    OrderId = token["orderId"].ToStringInvariant(),
                    MarketSymbol = token["symbol"].ToStringInvariant(),
                    AveragePrice = token["price"].ConvertInvariant<decimal>(),
                    StopPrice = token["stopPrice"].ConvertInvariant<decimal>(),
                };
                if (string.IsNullOrEmpty(result.OrderId))
                {
                    result.OrderId = token["orderId"].ToStringInvariant();
                }
                if (token["avgPrice"] != null)
                {
                    result.AveragePrice = token["avgPrice"].ConvertInvariant<decimal>();
                }
                if (had)
                {
                    result.IsBuy = fullOrder.IsBuy;
                }
                else
                {
                    fullOrder = result;
                }


                // http://www.onixs.biz/fix-dictionary/5.0.SP2/tagNum_39.html
                if (result.Result != ExchangeAPIOrderResult.Filled)//改为成交后不修改成其他状态
                {
                    switch (token["status"].ToStringInvariant())
                    {
                        case "NEW"://0:挂单中
                            result.Result = ExchangeAPIOrderResult.Pending;
                            Logger.Info("1ExchangeAPIOrderResult.Pending:" + token.ToString());
                            //                             if (token["triggered"].ToStringInvariant().Equals("StopOrderTriggered"))
                            //                             {
                            //                                 result.Result = ExchangeAPIOrderResult.TriggerPending;
                            //                             }

                            if (result.AmountFilled == 0)
                            {
                                result.Result = ExchangeAPIOrderResult.Pending;
                            }
                            else if (result.Amount == result.AmountFilled)
                            {
                                result.Result = ExchangeAPIOrderResult.Filled;
                            }
                            else if (result.AmountFilled > 0)
                            {
                                result.Result = ExchangeAPIOrderResult.FilledPartially;
                            }
                            break;
                        case "PARTIALLY_FILLED"://3:部分成交
                            if (result.AmountFilled == 0)
                            {
                                result.Result = ExchangeAPIOrderResult.Pending;
                            }
                            else if (result.Amount == result.AmountFilled)
                            {
                                result.Result = ExchangeAPIOrderResult.Filled;
                            }
                            else if (result.AmountFilled > 0)
                            {
                                result.Result = ExchangeAPIOrderResult.FilledPartially;
                            }
                            Logger.Info("2ExchangeAPIOrderResult" + result.Result + ":" + token.ToString());
                            break;
                        case "FILLED"://1:已完成
                            if (result.AmountFilled == 0)
                            {
                                result.Result = ExchangeAPIOrderResult.Canceled;
                            }
                            else if (result.Amount == result.AmountFilled)
                            {
                                result.Result = ExchangeAPIOrderResult.Filled;
                            }
                            else if (result.AmountFilled > 0)
                            {
                                result.Result = ExchangeAPIOrderResult.FilledPartially;
                            }

                            Logger.Info("2ExchangeAPIOrderResult" + result.Result + ":" + token.ToString());
                            break;
                        case "CANCELED"://2:已撤销
                            if (result.AmountFilled == 0)
                            {
                                result.Result = ExchangeAPIOrderResult.Canceled;
                            }
                            else if (result.Amount == result.AmountFilled)
                            {
                                result.Result = ExchangeAPIOrderResult.Filled;
                            }
                            else if (result.AmountFilled > 0)
                            {
                                result.Result = ExchangeAPIOrderResult.FilledPartially;
                            }
                            Logger.Info("4ExchangeAPIOrderResult:" + result.Result + ":" + token.ToString());
                            break;
                        case "REJECTED"://2:订单被拒绝
                            if (result.AmountFilled == 0)
                            {
                                result.Result = ExchangeAPIOrderResult.Canceled;
                            }
                            else if (result.Amount == result.AmountFilled)
                            {
                                result.Result = ExchangeAPIOrderResult.Filled;
                            }
                            else if (result.AmountFilled > 0)
                            {
                                result.Result = ExchangeAPIOrderResult.FilledPartially;
                            }
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


        public override async Task<IEnumerable<MarketCandle>> GetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            //window length in seconds. options: 15, 60, 300, 900, 3600, 14400, 86400
            /*
             {
  "success": true,
  "result": [
    {
      "close":11055.25,
      "high":11089.0,
      "low":11043.5,
      "open":11059.25,
      "startTime":"2019-06-24T17:15:00+00:00",
      "volume":464193.95725
    }
  ]
}
             */
//             var payload = await GetNoncePayloadAsync();
//             string urld = $"/funding_rates?";
// 
//             urld += $"&start_time={Math.Floor(startDate.Value.UnixTimestampFromDateTimeSeconds())}";
// 
//             urld += $"&end_time={Math.Floor(endDate.Value.UnixTimestampFromDateTimeSeconds())}";
// 
//             urld += $"&future={marketSymbol}";
//             var objj = await MakeJsonRequestAsync<JToken>(urld, BaseUrl,payload);
// 
//             Logger.Debug(objj.ToString());
// 
// 
//             return null;












            decimal maxDatas = 3600;
            List<MarketCandle> candles = new List<MarketCandle>();
            //GET /markets/{market_name}/candles?resolution={resolution}&limit={limit}&start_time={start_time}&end_time={end_time}
            int daySeconds = 24 * 60 * 60;
            if (startDate != null && endDate!=null)
            {

                int duringDays = Convert.ToInt32(Math.Ceiling((endDate.Value - startDate.Value).TotalDays));
                int allSeconds = daySeconds * duringDays;
                decimal allNum = allSeconds / periodSeconds;
                int times = Convert.ToInt32(Math.Ceiling(allNum / maxDatas));
               
                double startDateSeconds = startDate.Value.UnixTimestampFromDateTimeSeconds();
                double endDateSeconds = endDate.Value.UnixTimestampFromDateTimeSeconds();
                int perTime = Convert.ToInt32(Math.Min(maxDatas, Convert.ToInt32( (endDateSeconds - startDateSeconds )/ periodSeconds)) * periodSeconds);
                for (int i = 0; i < times; i++)
                {
                    if (i % 11 == 0)//避免超限制
                        await Task.Delay(1000);
                    double theStartDateSeconds = startDateSeconds+ i * perTime;
                    double theEndDateSeconds = theStartDateSeconds + perTime;


                    string periodString = PeriodSecondsToString(periodSeconds);
                    string url = $"/markets/{marketSymbol}/?binSize={periodString}&partial=false&symbol={marketSymbol}&reverse=true" + marketSymbol;
                    url = $"/markets/{marketSymbol}/candles?resolution={periodSeconds}";
                    url += "&limit=" + (maxDatas.ToStringInvariant());

                    url += $"&start_time={Math.Floor(theStartDateSeconds)}";

                    url += $"&end_time={Math.Floor(theEndDateSeconds)}";
                    var obj = await MakeJsonRequestAsync<JToken>(url);
                   // Logger.Debug(obj.ToString());
                    foreach (var t in obj)
                    {
                        candles.Add(this.ParseCandle(t, marketSymbol, periodSeconds, "open", "high", "low", "close", "startTime", TimestampType.Iso8601, "volume"));
                    }
                    var c = candles;
                }
            }
            else
            {
                string periodString = PeriodSecondsToString(periodSeconds);
                string url = $"/markets/{marketSymbol}/?binSize={periodString}&partial=false&symbol={marketSymbol}&reverse=true" + marketSymbol;
                url = $"/markets/{marketSymbol}/candles?resolution={periodSeconds}";
                if (limit != null)
                {
                    url += "&limit=" + (limit.Value.ToStringInvariant());
                }
                if (startDate != null)
                {
                    url += $"&start_time={Math.Floor(startDate.Value.UnixTimestampFromDateTimeSeconds())}";
                }
                if (endDate != null)
                {
                    url += $"&end_time={Math.Floor(endDate.Value.UnixTimestampFromDateTimeSeconds())}";
                }

                var obj = await MakeJsonRequestAsync<JToken>(url);
                foreach (var t in obj)
                {
                    candles.Add(this.ParseCandle(t, marketSymbol, periodSeconds, "open", "high", "low", "close", "startTime", TimestampType.Iso8601, "volume"));
                }
                candles.Reverse();
            }
            return candles;

        }
        public override bool ErrorPlanceOrderPrice(Exception ex)
        {
            return ex.ToString().Contains("Timestamp for this request is outside of the recvWindow");
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
        /// <summary>
        /// 返回第一档
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        protected override async Task<ExchangeOrderBook> OnGetOrderBookAsync(string marketSymbol, int maxCount = 100)
        {

            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            JToken token = await MakeJsonRequestAsync<JToken>($"/markets/{marketSymbol}", BaseUrl);


           
            ExchangeOrderBook book = new ExchangeOrderBook();

            var bid = Convert.ToDecimal(token["bid"]);
            var ask = Convert.ToDecimal(token["ask"]);
            book.MarketSymbol = marketSymbol;

            book.Bids.Add(bid, new ExchangeOrderPrice() { Price = bid, Amount = 1 });
            book.Asks.Add(ask,new ExchangeOrderPrice() {Price = ask,Amount = 1 });



            return book;
        }
    }

    public partial class ExchangeName { public const string Jaex = "Jaex"; }
    public partial class ExchangeFee
    {
    }
}
