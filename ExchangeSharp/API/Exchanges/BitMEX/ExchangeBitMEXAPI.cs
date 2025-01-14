﻿/*
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
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeBitMEXAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.bitmex.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://www.bitmex.com/realtime";
//         public override string BaseUrl { get; set; } = "https://testnet.bitmex.com/api/v1";
//         public override string BaseUrlWebSocket { get; set; } = "wss://testnet.bitmex.com/realtime";

        private SortedDictionary<long, decimal> dict_long_decimal = new SortedDictionary<long, decimal>();
        private SortedDictionary<decimal, long> dict_decimal_long = new SortedDictionary<decimal, long>();

        public ExchangeBitMEXAPI()
        {
            RequestWindow = TimeSpan.Zero;
            NonceStyle = NonceStyle.ExpiresUnixSeconds;

            // make the nonce go 10 seconds into the future (the offset is subtracted)
            // this will give us an api-expires 60 seconds into the future
            NonceOffset = TimeSpan.FromSeconds(-60.0);

            MarketSymbolSeparator = string.Empty;
            RequestContentType = "application/json";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;

            RateLimit = new RateGate(60, TimeSpan.FromMinutes(1));
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
                var msg = CryptoUtility.GetJsonForPayload(payload);
                var sign = $"{request.Method}{request.RequestUri.AbsolutePath}{request.RequestUri.Query}{nonce}{msg}";
                string signature = CryptoUtility.SHA256Sign(sign, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));

                request.AddHeader("api-expires", nonce.ToStringInvariant());
                request.AddHeader("api-key", PublicApiKey.ToUnsecureString());
                request.AddHeader("api-signature", signature);

                await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
            }
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
                    FundingRate = marketSymbolToken["fundingRate"].ConvertInvariant<decimal>()
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
                 var payloadJSON = GeneratePayloadJSON();
                 await _socket.SendMessageAsync(payloadJSON.Result);
             });

        }

        private async Task<string> GeneratePayloadJSON()
        {
            object expires = await GenerateNonceAsync();
            var message = "GET/realtime" + expires;
            var signature = CryptoUtility.SHA256Sign(message, PrivateApiKey.ToUnsecureString());
            Dictionary<string, object> payload = new Dictionary<string, object>
                {
                    { "op", "authKeyExpires"},
                    { "args", new object[]{ PublicApiKey.ToUnsecureString(),expires, signature } }
                };
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
{"info":"Welcome to the BitMEX Realtime API.","version":"2018-06-29T18:05:14.000Z","timestamp":"2018-07-05T14:22:26.267Z","docs":"https://www.bitmex.com/app/wsAPI","limit":{"remaining":39}}
{"success":true,"subscribe":"orderBookL2:XBTUSD","request":{"op":"subscribe","args":["orderBookL2:XBTUSD"]}}
{"table":"orderBookL2","action":"update","data":[{"symbol":"XBTUSD","id":8799343000,"side":"Buy","size":350544}]}
             */

            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
            }
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                var action = token["action"].ToStringInvariant();
                JArray bids = token["data"][0]["bids"] as JArray;
                JArray asks = token["data"][0]["asks"] as JArray;

                ExchangeOrderBook book = new ExchangeOrderBook();
                book.SequenceId = token["data"][0]["timestamp"].ConvertInvariant<DateTime>().Ticks;
                var price = 0m;
                var size = 0m;
                var marketSymbol = token["data"][0]["symbol"].ToStringInvariant();
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
                await _socket.SendMessageAsync(new { op = "subscribe", args = marketSymbols.Select(s => "orderBook10:" + this.NormalizeMarketSymbol(s)).ToArray() });
            });
        }
        #endregion
        /// <summary>
        /// 
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <param name="periodSeconds"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            /*
             [
{"timestamp":"2017-01-01T00:00:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.29,"low":968.29,"close":968.29,"trades":0,"volume":0,"vwap":null,"lastSize":null,"turnover":0,"homeNotional":0,"foreignNotional":0},
{"timestamp":"2017-01-01T00:01:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.76,"low":968.49,"close":968.7,"trades":17,"volume":12993,"vwap":968.72,"lastSize":2000,"turnover":1341256747,"homeNotional":13.412567469999997,"foreignNotional":12993},
             */
            //limtit max =1000 ,if >1000 need paginate;
            /*
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


            */
            decimal maxDatas = 1000;
            List<MarketCandle> candles = new List<MarketCandle>();
            //GET /markets/{market_name}/candles?resolution={resolution}&limit={limit}&start_time={start_time}&end_time={end_time}
            int daySeconds = 24 * 60 * 60;
            if (startDate != null && endDate != null)
            {

                int duringDays = Convert.ToInt32(Math.Ceiling((endDate.Value - startDate.Value).TotalDays));
                int allSeconds = daySeconds * duringDays;
                decimal allNum = allSeconds / periodSeconds;
                int times = Convert.ToInt32(Math.Ceiling(allNum / maxDatas));
                int perTime = Convert.ToInt32(maxDatas * periodSeconds);
                double startDateSeconds = startDate.Value.UnixTimestampFromDateTimeSeconds();

                for (int i = 0; i < times; i++)
                {
                    if (i % 11 == 0)//避免超限制
                        await Task.Delay(1000);
                    double theStartDateSeconds = startDateSeconds + i * perTime;
                    double theEndDateSeconds = theStartDateSeconds + perTime;


                    string periodString = PeriodSecondsToString(periodSeconds);
                    string url = $"/trade/bucketed?binSize={periodString}&partial=false&symbol={marketSymbol}&reverse=true" + marketSymbol;
                    //string url = $"/markets/{marketSymbol}/?binSize={periodString}&partial=false&symbol={marketSymbol}&reverse=true" + marketSymbol;
                    url += "&startTime=" + Math.Floor(theStartDateSeconds).UnixTimeStampLocalToDateTimeSeconds().ToString("yyyy-MM-dd-HH");
                    url += "&endTime=" + Math.Floor(theEndDateSeconds).UnixTimeStampLocalToDateTimeSeconds().ToString("yyyy-MM-dd-HH");
                    url += "&count=" + (maxDatas.ToStringInvariant());
                    var obj = await MakeJsonRequestAsync<JToken>(url);
                    foreach (var t in obj)
                    {
                        candles.Add(this.ParseCandle(t, marketSymbol, periodSeconds, "open", "high", "low", "close", "startTime", TimestampType.Iso8601, "volume"));
                    }
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



            return candles;
        }
// 
//         public override async Task<ExchangeOrderBook> GetOrderBookAsync(string marketSymbol, int maxCount = 100)
//         {
//             /*
//              * [
//   {
//     "symbol": "XBTUSD",
//     "id": 8799054750,
//     "side": "Sell",
//     "size": 99391,
//     "price": 9452.5
//   },
//   {
//     "symbol": "XBTUSD",
//     "id": 8799054800,
//     "side": "Sell",
//     "size": 178379,
//     "price": 9452
// 
//   }]*/
//             Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
//             JToken token = await MakeJsonRequestAsync<JToken>($"/orderBook/L2?symbol={marketSymbol}&depth=25");
// 
//             ExchangeOrderBook book = new ExchangeOrderBook();
//             book.SequenceId = token["data"][0]["timestamp"].ConvertInvariant<DateTime>().Ticks;
//             var price = 0m;
//             var size = 0m;
//             book.MarketSymbol = marketSymbol;
//             void applyData(JArray data, bool isBuy)
//             {
//                 foreach (var d in data)
//                 {
//                     price = d[0].ConvertInvariant<decimal>();
//                     size = d[1].ConvertInvariant<decimal>();
//                     var depth = new ExchangeOrderPrice { Price = price, Amount = size };
//                     if (isBuy)
//                     {
//                         book.Bids[depth.Price] = depth;
//                     }
//                     else
//                     {
//                         book.Asks[depth.Price] = depth;
//                     }
// 
//                 }
//             }
// 
//             return book;
//         }
    
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
                var count = item["marginBalance"].ConvertInvariant<decimal>();

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
            string query = "/order?filter={\"ordType\":\""+ orderType.ToString() + "\""+","+"\"open\":" + "true" + "}";
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

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            AddOrderToPayload(order, payload);
            JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "POST");
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
             * [
  {
    "account": 981600,
    "symbol": "XBTM19",
    "currency": "XBt",
    "underlying": "XBT",
    "quoteCurrency": "USD",
    "commission": 0.00075,
    "initMarginReq": 0.01,
    "maintMarginReq": 0.005,
    "riskLimit": 5000000000,
    "leverage": 100,
    "crossMargin": true,
    "deleveragePercentile": null,
    "rebalancedPnl": 0,
    "prevRealisedPnl": 0,
    "prevUnrealisedPnl": 0,
    "prevClosePrice": 12130.48,
    "openingTimestamp": "2019-06-28T10:00:00.000Z",
    "openingQty": 0,
    "openingCost": 0,
    "openingComm": 0,
    "openOrderBuyQty": 0,
    "openOrderBuyCost": 0,
    "openOrderBuyPremium": 0,
    "openOrderSellQty": 0,
    "openOrderSellCost": 0,
    "openOrderSellPremium": 0,
    "execBuyQty": 21,
    "execBuyCost": 178836,
    "execSellQty": 0,
    "execSellCost": 0,
    "execQty": 21,
    "execCost": -178836,
    "execComm": 134,
    "currentTimestamp": "2019-06-28T10:50:21.081Z",
    "currentQty": 21,
    "currentCost": -178836,
    "currentComm": 134,
    "realisedCost": 0,
    "unrealisedCost": -178836,
    "grossOpenCost": 0,
    "grossOpenPremium": 0,
    "grossExecCost": 178836,
    "isOpen": true,
    "markPrice": 11743.77,
    "markValue": -178815,
    "riskValue": 178815,
    "homeNotional": 0.00178815,
    "foreignNotional": -21,
    "posState": "",
    "posCost": -178836,
    "posCost2": -178836,
    "posCross": 0,
    "posInit": 1789,
    "posComm": 136,
    "posLoss": 0,
    "posMargin": 1925,
    "posMaint": 1031,
    "posAllowance": 0,
    "taxableMargin": 0,
    "initMargin": 0,
    "maintMargin": 1946,
    "sessionMargin": 0,
    "targetExcessMargin": 0,
    "varMargin": 0,
    "realisedGrossPnl": 0,
    "realisedTax": 0,
    "realisedPnl": -134,
    "unrealisedGrossPnl": 21,
    "longBankrupt": 0,
    "shortBankrupt": 0,
    "taxBase": 0,
    "indicativeTaxRate": null,
    "indicativeTax": 0,
    "unrealisedTax": 0,
    "unrealisedPnl": 21,
    "unrealisedPnlPcnt": 0.0001,
    "unrealisedRoePcnt": 0.0117,
    "simpleQty": null,
    "simpleCost": null,
    "simpleValue": null,
    "simplePnl": null,
    "simplePnlPcnt": null,
    "avgCostPrice": 11742.5,
    "avgEntryPrice": 11742.5,
    "breakEvenPrice": 11752.5,
    "marginCallPrice": 2086,
    "liquidationPrice": 2086,
    "bankruptPrice": 2084,
    "timestamp": "2019-06-28T10:50:21.081Z",
    "lastPrice": 11743.77,
    "lastValue": -178815
  }
]*/
            ExchangeMarginPositionResult poitionR = null;
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/position", BaseUrl, payload);
            foreach (JToken position in token)
            {
                if (position["symbol"].ToStringInvariant().Equals(marketSymbol))
                {
                    poitionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = position["currentQty"].ConvertInvariant<decimal>(),
                        LiquidationPrice = position["liquidationPrice"].ConvertInvariant<decimal>(),
                        BasePrice = position["avgCostPrice"].ConvertInvariant<decimal>(),
                    };
                }
            }
            return poitionR;
        }
        private void AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            payload["symbol"] = order.MarketSymbol;
            payload["ordType"] = order.OrderType.ToStringInvariant();
            payload["side"] = order.IsBuy ? "Buy" : "Sell";
            payload["orderQty"] = order.Amount;
            if (order.Price != 0)
                payload["price"] = order.Price;
            if (order.StopPrice != 0)
                payload["stopPx"] = order.StopPrice;
            //payload["displayQty"] = 0;//隐藏订单
            if (order.ExtraParameters.TryGetValue("execInst", out var execInst))
            {
                payload["execInst"] = execInst;
            }
            if (order.ExtraParameters.TryGetValue("orderID", out var orderID))
            {
                payload["orderID"] = orderID;
            }
           
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            /*
{[
  {
    "orderID": "b7b8518a-c0d8-028d-bb6e-d843f8f723a3",
    "clOrdID": "",
    "clOrdLinkID": "",
    "account": 93592,
    "symbol": "XBTUSD",
    "side": "Buy",
    "simpleOrderQty": null,
    "orderQty": 1,
    "price": 5500,
    "displayQty": null,
    "stopPx": null,
    "pegOffsetValue": null,
    "pegPriceType": "",
    "currency": "USD",
    "settlCurrency": "XBt",
    "ordType": "Limit",
    "timeInForce": "GoodTillCancel",
    "execInst": "ParticipateDoNotInitiate",
    "contingencyType": "",
    "exDestination": "XBME",
    "ordStatus": "Canceled",
    "triggered": "",
    "workingIndicator": false,
    "ordRejReason": "",
    "simpleLeavesQty": 0,
    "leavesQty": 0,
    "simpleCumQty": 0,
    "cumQty": 0,
    "avgPx": null,
    "multiLegReportingType": "SingleSecurity",
    "text": "Canceled: Canceled via API.\nSubmission from testnet.bitmex.com",
    "transactTime": "2018-07-08T09:20:39.428Z",
    "timestamp": "2018-07-08T11:35:05.334Z"
  }
]}
            */

            //             var marketSymbol = token["symbol"].ToStringInvariant();
            //             string m_symbol = marketSymbol+"_" + token["orderID"].ToStringInvariant();
            //             JToken old;
            //             
            //             if (!orderPairs.TryGetValue(m_symbol, out old))
            //             {
            //                 orderPairs[m_symbol] = token;
            //             }
            //             else
            //             {
            //                 foreach (var item in token)
            //                 {
            //                     if (item is JProperty)
            //                     {
            //                         var jp = (JProperty)item;
            //                         old[jp.Name] = jp.Value;
            //                     }
            //                 }
            //                 token = old;
            //             }

            // 
            ExchangeOrderResult fullOrder;
            lock(fullOrders)
            { 
                bool had = fullOrders.TryGetValue(token["orderID"].ToStringInvariant(), out fullOrder);


                ExchangeOrderResult result = new ExchangeOrderResult
                {
                    Amount = token["orderQty"].ConvertInvariant<decimal>(),
                    AmountFilled = token["cumQty"].ConvertInvariant<decimal>(),
                    Price = token["price"].ConvertInvariant<decimal>(),
                    IsBuy = token["side"].ToStringInvariant().EqualsWithOption("Buy"),
                    OrderDate = token["transactTime"].ConvertInvariant<DateTime>(),
                    OrderId = token["orderID"].ToStringInvariant(),
                    MarketSymbol = token["symbol"].ToStringInvariant(),
                    AveragePrice = token["avgPx"].ConvertInvariant<decimal>(),
                    StopPrice = token["stopPx"].ConvertInvariant<decimal>(),
                };
                if (had)
                {
                    result.IsBuy = fullOrder.IsBuy;
                }
                else
                {
                    fullOrder = result;
                }

                if (!token["side"].ToStringInvariant().EqualsWithOption(string.Empty))
                {
                    result.IsBuy = token["side"].ToStringInvariant().EqualsWithOption("Buy");
                    fullOrder.IsBuy = result.IsBuy;
                }


                // http://www.onixs.biz/fix-dictionary/5.0.SP2/tagNum_39.html
                if (result.Result != ExchangeAPIOrderResult.Filled)//改为成交后不修改成其他状态
                {

                    switch (token["ordStatus"].ToStringInvariant())
                    {
                        case "New":
                            result.Result = ExchangeAPIOrderResult.Pending;
                            Logger.Info("1ExchangeAPIOrderResult.Pending:" + token.ToString());
                            if (token["triggered"].ToStringInvariant().Equals("StopOrderTriggered"))
                            {
                                result.Result = ExchangeAPIOrderResult.TriggerPending;
                            }


                            break;
                        case "PartiallyFilled":
                            result.Result = ExchangeAPIOrderResult.FilledPartially;
                            Logger.Info("2ExchangeAPIOrderResult.FilledPartially:" + token.ToString());
                            break;
                        case "Filled":
                            result.Result = ExchangeAPIOrderResult.Filled;
                            Logger.Info("3ExchangeAPIOrderResult.Filled:" + token.ToString());
                            break;
                        case "Canceled":
                            result.Result = ExchangeAPIOrderResult.Canceled;
                            Logger.Info("4ExchangeAPIOrderResult.Canceled:" + token.ToString());
                            break;
                        default:
                            result.Result = ExchangeAPIOrderResult.Error;
                            Logger.Info("5ExchangeAPIOrderResult.Error:" + token.ToString());
                            break;
                    }

                    if (token["triggered"]!=null)
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
                if(result==null)
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
        /*
         * 
         if (ex.ToString().Contains("overloaded") || ex.ToString().Contains("403 Forbidden") || ex.ToString().Contains("Bad Gateway") )
                    {
                        Logger.Error(Utils.Str2Json( "req", req.ToStringInvariant(), "ex", ex));
                        await Task.Delay(2000);
                    }
                    else if (ex.ToString().Contains("RateLimitError"))
                    {
                        Logger.Error(Utils.Str2Json("req", req.ToStringInvariant(), "ex", ex));
                        await Task.Delay(5000);
                    }
                    else
                    {
                        Logger.Error(Utils.Str2Json("ReverseOpenMarketOrder抛错" , ex.ToString()));
                        throw ex;
                    }
        */
        public override bool ErrorTradingSyatemIsBusy(Exception ex)
        {
            return ex.ToString().Contains("overloaded") || ex.ToString().Contains("403 Forbidden") || ex.ToString().Contains("Bad Gateway");
        }

        public override bool ErrorNeedNotCareError(Exception ex)
        {
            return ex.ToString().Contains("Invalid orderID") || ex.ToString().Contains("Not Found") ;
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

    public partial class ExchangeName { public const string BitMEX = "BitMEX"; }
    public partial class ExchangeFee
    {
        public const decimal BitMEX_EOS = -0.0005m;
        public const decimal BitMEX_ETHM19 = -0.0005m;
    }
}
