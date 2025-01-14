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
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeZBGDMAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.zbg.fun";
        public override string BaseUrlWebSocket { get; set; } = "wss://kline.zbg.fun/exchange/v1/futurews";
//         public override string BaseUrl { get; set; } = "https://testnet.bitmex.com/api/v1";
//         public override string BaseUrlWebSocket { get; set; } = "wss://testnet.bitmex.com/realtime";

        private SortedDictionary<long, decimal> dict_long_decimal = new SortedDictionary<long, decimal>();
        private SortedDictionary<decimal, long> dict_decimal_long = new SortedDictionary<decimal, long>();
        private string OrderIdStart;
        private int OrderNum;
        //btc每张的比例
        public int perRate = 100;
        public ExchangeZBGDMAPI()
        {
            RequestWindow = TimeSpan.Zero;
            NonceStyle = NonceStyle.ExpiresUnixMilliseconds;

            // make the nonce go 10 seconds into the future (the offset is subtracted)
            // this will give us an api-expires 60 seconds into the future
            NonceOffset = TimeSpan.FromSeconds(10.0);

            MarketSymbolSeparator = "-";//string.Empty;
            RequestContentType = "application/json";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;//FullBookFirstThenDeltas;

            RateLimit = new RateGate(9000, TimeSpan.FromMinutes(10));
            OrderIdStart = (long)CryptoUtility.UtcNow.UnixTimestampFromDateTimeMilliseconds()+"_";
            OrderNum = 0;
        }
        /// <summary>
        /// 返回 客户端id
        /// </summary>
        /// <returns></returns>
        private string GetClinetOrderID()
        {
            return OrderIdStart + (OrderNum++);
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
        private void GetSymbolAndContractCode(string marketSymbol, out string symbol, out string contractCode)
        {
            string[] strAry = new string[2];
            string[] splitAry = marketSymbol.Split(MarketSymbolSeparator.ToCharArray(), StringSplitOptions.None);
            symbol = splitAry[0];
            contractCode = splitAry[1].ToLower();
        }
        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // convert nonce to long, trim off milliseconds
                var nonce = payload["nonce"].ConvertInvariant<long>();
                payload.Remove("nonce");
                var msg = CryptoUtility.GetJsonForPayload(payload);
                //var sign = $"{nonce}{request.Method}{request.RequestUri.AbsolutePath}{request.RequestUri.Query}{msg}";
                if (request.Method.Equals("GET"))
                {
                    msg = "";
                    if (!string.IsNullOrEmpty(request.RequestUri.Query))
                    {
                        var datas = request.RequestUri.Query.Split(new string[] { "?" }, StringSplitOptions.None);
                        var str = datas[1].Split(new string[] { "&" }, StringSplitOptions.None);
                        var dataDic = new Dictionary<string, object>();
                        foreach (var s in str)
                        {
                            var splits = s.Split(new string[] { "=" }, StringSplitOptions.None);
                            dataDic.Add(splits[0], splits[1]);
                        }
                        dataDic = CryptoUtility.AsciiSortDictionary(dataDic);
                       
                        foreach (var v in dataDic)
                        {
                            msg += v.Key + v.Value;
                        }
                    }
                   
                }

                var sign = $"{PublicApiKey.ToUnsecureString()}{nonce}{msg}{PrivateApiKey.ToUnsecureString()}";

                string signature = CryptoUtility.MD5Sign(sign);

                Logger.Debug(PrivateApiKey.ToUnsecureString() + "    "+PublicApiKey.ToUnsecureString());
                // Logger.Debug(PublicApiKey.ToUnsecureString());
                request.AddHeader("Apiid", PublicApiKey.ToUnsecureString());
                request.AddHeader("Sign", signature);
                request.AddHeader("Timestamp", nonce.ToStringInvariant());
                
                if (!string.IsNullOrEmpty(SubAccount))
                {
                    request.AddHeader("FTX-SUBACCOUNT", SubAccount);
                }
               
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
                Logger.Debug(str);
                if (str.Contains("connected") || str.Contains("subscribe") || str.Contains("realtime") || str.Contains("Pong"))
                {
                    if (str.Contains("connected"))
                    {
                        var t1 = GeneratePayloadJSON();
                        Task.WaitAll(t1);
                        var payloadJSON = t1.Result;
                        Logger.Debug(payloadJSON.ToString());
                        _socket.SendMessageAsync(payloadJSON);
                    }

                    if (str.Contains("subscribe"))
                    {// subscription successful
                        if (pingTimer == null)
                        {
                            pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync("Ping"),
                                state: null, dueTime: 0, period: 15000); // send a ping every 15 seconds
                        }
                        return Task.CompletedTask;
                    }
                    return Task.CompletedTask;
                }
                JArray token;
                try
                {
                    token = JArray.Parse(str);
                }
                catch (System.Exception ex)
                {
                    Logger.Error(ex);
                    return Task.CompletedTask;
                }
                if (token.ToString().Contains("pong"))
                {
                    return Task.CompletedTask;
                }

                Logger.Debug(token.ToString());
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
                 return Task.CompletedTask;
             }, async (_socket) =>
             {
                 //连接中断也不应该删除历史信息
                 //fullOrders.Clear();
                 var payloadJSON = await GeneratePayloadJSON();
                 //Logger.Debug(payloadJSON.ToString());

                 await _socket.SendMessageAsync(payloadJSON);
                 //await _socket.SendMessageAsync(new { op = "subscribe", channel = "orders" });
             },async(_socket) =>
             {
                 pingTimer.Dispose();
                 pingTimer = null;
                 
             });
        }
        
        private async Task<string> GeneratePayloadJSON()
        {
            string expires = (await GenerateNonceAsync()).ToString();
            var privateKey = PrivateApiKey.ToUnsecureString();
            var publicApiKey = PublicApiKey.ToUnsecureString();
            //var privateKey = PrivateApiKey.ToUnsecureString();
            //var publicApiKey = "7yff9SPuP6O7yff9SPuP6P";

            /*
             {
  "action":"auth",
  "apiid":"7ljSc36ADq47ljSc36ADq5",
  "timestamp":"1591873540440",
  "passphrase":"",
  "sign":"f3c6e8f868838a670aa6360ef9ec50e7"
}*/
            //expires = 1557246346499;
            //privateKey = "Y2QTHI23f23f23jfjas23f23To0RfUwX3H42fvN-";


            var message = expires + "websocket_login";
            var sign = $"{publicApiKey}{expires}{privateKey}";

            string signature = CryptoUtility.MD5Sign(sign);
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
                    { "action","auth"  },
                    { "apiid",publicApiKey  },
                    { "timestamp",expires },
                    { "passphrase","" },//CryptoUtility.MD5Sign(expires.ToString()+privateKey) 
                    { "sign",signature  },
                };
            }
            
            return CryptoUtility.GetJsonForPayload(payload);
        }
        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] marketSymbols)
        {
            /*
"["future_snapshot_depth",{"asks":[["7.22","5935"],["7.221","2547"],["7.222","8290"],["7.223","3498"],["7.224","6413"],["7.225","6184"],["7.226","4356"],["7.227","5457"],["7.228","5039"],["7.229","5388"],["7.23","7225"],["7.231","2598"],["7.232","7895"],["7.233","4059"],["7.234","9607"],["7.235","9573"],["7.236","10520"],["7.237","11537"],["7.238","11934"],["7.241","14178"],["7.254","13692"],["7.267","12276"],["7.28","15677"],["7.293","11224"],["7.306","12092"],["7.319","6533"],["7.332","16045"],["7.345","21849"],["7.358","19462"],["7.371","14799"],["7.384","15433"],["7.397","20453"],["7.41","6614"],["7.423","9631"],["7.436","20367"],["7.449","6578"],["7.462","16502"],["7.475","8524"],["7.488","8437"],["7.64","7391"],["7.643","6432"]],"contractId":1000012,"bids":[["7.216","2879"],["7.215","9212"],["7.214","10608"],["7.213","10279"],["7.212","7532"],["7.211","3499"],["7.21","7573"],["7.209","8631"],["7.208","4746"],["7.206","8568"],["7.205","7279"],["7.204","7275"],["7.203","2888"],["7.202","4257"],["7.201","7235"],["7.2","4443"],["7.199","5416"],["7.198","11131"],["7.197","3955"],["7.189","18034"],["7.176","15819"],["7.163","17235"],["7.15","19942"],["7.137","12637"],["7.124","12186"],["7.111","11625"],["7.098","12532"],["7.085","6228"],["7.072","17606"],["7.059","16962"],["7.046","14764"],["7.033","13671"],["7.02","9047"],["7.007","12677"],["6.994","13787"],["6.981","8376"],["6.968","14632"],["6.955","18212"],["6.487","4821"],["6.474","3583"],["6.461","9272"],["6.448","10174"],["6.435","5522"],["6.422","5646"],["6.409","6997"],["6.396","6086"],["6.383","10617"],["6.37","10010"],["6.357","6441"],["6.344","4620"],["6.318","9840"],["6.125","430"],["6.123","16358"],["6.122","4728"],["6.121","4767"],["6.12","16802"],["6.118","8287"],["6.117","19081"],["6.115","10067"],["6.114","11994"],["6.113","5772"],["6.112","7071"],["6.111","14137"],["6.11","12371"],["6.109","7386"],["6.108","6471"],["6.105","3154"],["6.102","8048"],["6.099","11051"],["6.097","6683"],["6.096","5016"],["6.093","2906"],["6.09","2670"],["6.087","4072"],["6.084","13075"],["6.081","10587"],["6.078","7248"],["6.075","7643"],["6.072","10088"],["6.071","4053"],["6.069","4172"],["6.058","10315"],["6.045","9660"],["6.032","5669"],["6.019","8609"],["6.006","7346"],["5.993","9525"],["5.98","8345"],["5.967","2838"],["5.954","11630"],["5.941","13938"],["5.928","5552"],["5.915","15830"],["5.902","12609"],["5.889","27139"],["5.876","26773"],["5.863","7299"],["5.85","9187"],["5.837","15532"],["5.824","15155"]],"tradeDate":20200803,"time":1596447511399481}]"
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
            /*
"["future_snapshot_depth",{"asks":[["7.22","5935"],["7.221","2547"],["7.222","8290"],["7.223","3498"],["7.224","6413"],["7.225","6184"],["7.226","4356"],["7.227","5457"],["7.228","5039"],["7.229","5388"],["7.23","7225"],["7.231","2598"],["7.232","7895"],["7.233","4059"],["7.234","9607"],["7.235","9573"],["7.236","10520"],["7.237","11537"],["7.238","11934"],["7.241","14178"],["7.254","13692"],["7.267","12276"],["7.28","15677"],["7.293","11224"],["7.306","12092"],["7.319","6533"],["7.332","16045"],["7.345","21849"],["7.358","19462"],["7.371","14799"],["7.384","15433"],["7.397","20453"],["7.41","6614"],["7.423","9631"],["7.436","20367"],["7.449","6578"],["7.462","16502"],["7.475","8524"],["7.488","8437"],["7.64","7391"],["7.643","6432"]],"contractId":1000012,"bids":[["7.216","2879"],["7.215","9212"],["7.214","10608"],["7.213","10279"],["7.212","7532"],["7.211","3499"],["7.21","7573"],["7.209","8631"],["7.208","4746"],["7.206","8568"],["7.205","7279"],["7.204","7275"],["7.203","2888"],["7.202","4257"],["7.201","7235"],["7.2","4443"],["7.199","5416"],["7.198","11131"],["7.197","3955"],["7.189","18034"],["7.176","15819"],["7.163","17235"],["7.15","19942"],["7.137","12637"],["7.124","12186"],["7.111","11625"],["7.098","12532"],["7.085","6228"],["7.072","17606"],["7.059","16962"],["7.046","14764"],["7.033","13671"],["7.02","9047"],["7.007","12677"],["6.994","13787"],["6.981","8376"],["6.968","14632"],["6.955","18212"],["6.487","4821"],["6.474","3583"],["6.461","9272"],["6.448","10174"],["6.435","5522"],["6.422","5646"],["6.409","6997"],["6.396","6086"],["6.383","10617"],["6.37","10010"],["6.357","6441"],["6.344","4620"],["6.318","9840"],["6.125","430"],["6.123","16358"],["6.122","4728"],["6.121","4767"],["6.12","16802"],["6.118","8287"],["6.117","19081"],["6.115","10067"],["6.114","11994"],["6.113","5772"],["6.112","7071"],["6.111","14137"],["6.11","12371"],["6.109","7386"],["6.108","6471"],["6.105","3154"],["6.102","8048"],["6.099","11051"],["6.097","6683"],["6.096","5016"],["6.093","2906"],["6.09","2670"],["6.087","4072"],["6.084","13075"],["6.081","10587"],["6.078","7248"],["6.075","7643"],["6.072","10088"],["6.071","4053"],["6.069","4172"],["6.058","10315"],["6.045","9660"],["6.032","5669"],["6.019","8609"],["6.006","7346"],["5.993","9525"],["5.98","8345"],["5.967","2838"],["5.954","11630"],["5.941","13938"],["5.928","5552"],["5.915","15830"],["5.902","12609"],["5.889","27139"],["5.876","26773"],["5.863","7299"],["5.85","9187"],["5.837","15532"],["5.824","15155"]],"tradeDate":20200803,"time":1596447511399481}]"
   */
            
            Timer pingTimer = null;
            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = GetMarketSymbolsAsync().Sync().ToArray();
            }
            GetSymbolAndContractCode(marketSymbols[0], out string symbol, out string contractCode);
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                if (str.Contains("Pong"))
                {
                    Logger.Debug(str);
                }
               
                if (str.Contains("connected") || str.Contains("subscribe") || str.Contains("realtime")|| str.Contains("Pong"))
                {
                    if (str.Contains("subscribe"))
                    {// subscription successful
//                         if (pingTimer == null)
//                         {
//                             pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync("Ping"),
//                                 state: null, dueTime: 0, period: 15000); // send a ping every 15 seconds
//                         }
//                         return Task.CompletedTask;
                    }
                    return Task.CompletedTask;
                }
                if (pingTimer == null)
                {
                    pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync("Ping"),
                        state: null, dueTime: 0, period: 15000); // send a ping every 15 seconds
                }
                
                JToken token;
                if (!str.Contains("future_snapshot_depth"))
                {
                    return Task.CompletedTask;
                }
                try
                {
                    str = str.Replace("[\"future_snapshot_depth\",", "");
                    str = str.Remove(str.Length - 1);
                    token = JToken.Parse(str);
                }
                catch (System.Exception ex)
                {
                    Logger.Error(ex);
                    return Task.CompletedTask;
                }

                
                if (token.ToString().Contains("Pong"))
                {
                    return Task.CompletedTask;
                }
                JArray bids = null;
                JArray asks = null;
                if (token["bids"]!=null)
                {
                    bids = token["bids"] as JArray;
                }
                if (token["asks"] != null)
                {
                    asks = token["asks"] as JArray;
                }
                ExchangeOrderBook book = new ExchangeOrderBook();
                book.SequenceId = token["time"].ConvertInvariant<long>();
                var price = 0m;
                var size = 0m;
                var marketSymbol = marketSymbols[0];
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
                if (token["bids"] != null)
                {
                    applyData(bids, true);
                }
                if (token["asks"] != null)
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
               
                await _socket.SendMessageAsync(new { action = "sub", topic = "future_snapshot_depth-"+ contractCode });
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
             {[
  {
    "coin": "USD",
    "free": -1.24133108,
    "total": -1.24133108,
    "usdValue": -1.24133108
  },
  {
    "coin": "BTC",
    "free": 0.04766072,
    "total": 0.04794836,
    "usdValue": 252.96543570105575
  }
]}
             */
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/exchange/api/v1/future/assets/available", BaseUrl, payload);
            decimal totalAmount = 0;
            Logger.Debug(token.ToString());
            token = token["datas"];
            if ( string.IsNullOrEmpty(symbol))//获取全部
            {
                foreach (var item in token)
                {
                    var coin = item["currencyName"].ToStringInvariant();
                    var count = item["totalBalance"].ConvertInvariant<decimal>();
                    totalAmount += count;
                }
            }
            else
            {
                foreach (var item in token)
                {
                    var coin = item["currencyName"].ToStringInvariant();
                    var count = item["totalBalance"].ConvertInvariant<decimal>();
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
            string query = "/exchange/api/v1/future/assets/available";
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            
            
            foreach (JToken order in token["datas"])
            {
                var ord = ParseOrder(order);
                // Logger.Debug(token.ToString());
                if (marketSymbol != null)
                {
                    GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
                    if (symbol.Equals(ord.MarketSymbol))
                    {
                        ord.MarketSymbol = marketSymbol;
                        orders.Add(ord);
                    }
                }
                else
                {
                    orders.Add(ord);
                }
               
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
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            //payload.Add("symbol", symbol);
            //payload.Add("orderId", orderId);
            string query = $"/exchange/api/v1/future/order?symbol={symbol}&orderId={orderId}";
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            Logger.Debug(token.ToString());
            var or = ParseOrder(token["datas"]);
            return or;
        }
        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            ExchangeOrderResult result;
            JToken token;
            if (orderId == "all")
            {
                token = await MakeJsonRequestAsync<JToken>("/exchange/api/v1/future/cancel-all", BaseUrl, payload, "POST");
            }
            else
            {
                if (marketSymbol == null)
                {
                    Logger.Error("marketSymbol can not be null!!");
                    throw new Exception("marketSymbol can not be null!!");
                }
                payload["symbol"] = marketSymbol;
                payload["orderId"] = orderId;
                token = await MakeJsonRequestAsync<JToken>("/exchange/api/v1/future/cancel", BaseUrl, payload, "POST");
            }
            try
            {//{{
//   "datas": null,
//   "resMsg": {
//     "message": "success !",
//     "method": null,
//     "code": "1"
//   }
// }}
                Logger.Debug("");
                if (!token["resMsg"]["code"].ToString().Equals("1"))
                {
                    throw new Exception("CancelOrderEx:"+ token.ToString());
                }
            }
            catch (Exception ex)
            {
                throw new Exception("CancelOrderEx:" + token.ToString());
            }
        }
        public override bool ErrorCancelOrderIdNotFound(Exception ex)
        {
            return ex.ToString().Contains("");
        }
        public override bool ErrorNeedNotCareError(Exception ex)
        {
            return base.ErrorNeedNotCareError(ex);
        }
        public override bool ErrorBalanceNotEnouthError(Exception ex)
        {
            return ex.ToString().Contains("avail not enough");
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
        private async Task<ExchangeOrderResult> m_OnPlaceOrderAsync(ExchangeOrderRequest order, bool isOpen)
        {

            bool had = order.ExtraParameters.TryGetValue("orderID", out object orderId);
            if (had)//如果有订单号，先删除再挂订单
            {
                await OnCancelOrderAsync(orderId.ToString(), order.MarketSymbol);
            }
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string addUrl = "/exchange/api/v1/future/place";
            AddOrderToPayload(order,  payload, isOpen);
           
            JObject token;
            JObject jo;
            try
            {
                //Logger.Debug("m_OnPlaceOrderAsync:" + order.ToString() + "  isOpen:" + isOpen);
                token = await MakeJsonRequestAsync<JObject>(addUrl, BaseUrl, payload, "POST");
                //                 //{{
                //                 "datas": "11606195267749142",
                //   "resMsg": {
                //                     "message": "success !",
                //     "method": null,
                //     "code": "1"
                //   }
                //             }}
                //jo = JsonConvert.DeserializeObject<JObject>(token.Root.ToString());
                //Logger.Debug("m_OnPlaceOrderAsync:" + jo.ToString());

                if (token["resMsg"]["code"].ToString() != "1")
                {
                    throw new Exception(token.ToString());
                }
                var result = new ExchangeOrderResult()
                {
                    OrderId = token["datas"].ToString(),
                    MarketSymbol = order.MarketSymbol,
                    Amount = order.Amount,
                    Price = order.Price,
                    OrderDate = DateTime.UtcNow,
                    Result = ExchangeAPIOrderResult.Pending
                };
                return result;

            }
            catch (System.Exception ex)
            {
                Logger.Error(ex);
                Logger.Error(ex.Message, "  payload:", payload, "  addUrl:", addUrl, "  BaseUrl:", BaseUrl, ex.StackTrace);
                throw new Exception(payload.ToString(), ex);
            }

        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            JToken token;
            //sdfdsf错误错误
            AddOrderToPayload(order, payload);
            token = await MakeJsonRequestAsync<JToken>("/exchange/api/v1/future/place", BaseUrl, payload, "POST");

            /*{{
  "code": 101040740,
  "msg": "invaid price",
  "data": null
}}
*/
            if (token["code"].ToString() != "0")
            {
                throw new Exception(token.ToString());
            }
            return new ExchangeOrderResult()
            {
                OrderId = token["data"].ToString(),
                MarketSymbol = order.MarketSymbol,
                Amount = order.Amount,
                Price = order.Price,
                OrderDate = DateTime.UtcNow,
                Result = ExchangeAPIOrderResult.Pending
            };
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
        private void AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload,bool? isOpen=null)
        {
            GetSymbolAndContractCode(order.MarketSymbol, out string symbol, out string contractCode);
            payload["symbol"] = symbol;
            payload["orderType"] = GetOrderType( order.OrderType);
            payload["side"] = order.IsBuy ? 1: -1;
            payload["quantity"] = order.Amount;
            if (order.Price != 0)
            {
                payload["price"] = order.Price;
            }
            else
            {
                payload["postOnly"] = false;//市价单
                payload["price"] = null;
            }
            if (isOpen!=null)
            {
                payload["positionEffect"] = isOpen.Value? 1:2;
            }
            else
            {
                Logger.Error("ExtraParameters: \"positionEffect\" must set value");
                throw new Exception("ExtraParameters: \"positionEffect\" must set value");
            }
                
            
            payload["marginType"] = 1;//全仓1，逐仓2
            payload["marginRate"] = "0.02";//全仓时0，逐仓时>=0，保证金率   杠杆倍数 = 1/marginRate 最多100倍

            payload["orderSubType"] = 0;//0（默认值），1（被动委托），2（最近价触发条件委托），3（指数触发条件委托），4（标记价触发条件委托）


            if (order.StopPrice != 0)
                payload["stopPx"] = order.StopPrice;
            //payload["displayQty"] = 0;//隐藏订单
            
            //payload["ioc"] = true;//值全部成交
            
            //if (order.ExtraParameters.TryGetValue("execInst", out var execInst))
            //{
            //    payload["execInst"] = execInst;
            //}
//             if (order.ExtraParameters.TryGetValue("orderID", out var orderID))
//             {
//                 payload["clientId"] = orderID;
//             }else
            
            //payload["clientId"] = GetClinetOrderID();
        }

        private int GetOrderType(OrderType orderType)
        {
            if (orderType == OrderType.Limit)
                return 1;
            else if (orderType == OrderType.Market)
                return 3;
            else
                throw new Exception("Had not the type" + orderType.ToString());
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            /*
/*
                        {
   "messageType": 3004,
   "accountId": 19113,
   "contractId": 999999,
   "orderId": "11597373409560556",
   "clientOrderId": "221db13aafba441b907d0ca3318ca809",
   "price": "11166",
   "quantity": "2",
   "leftQuantity": "2",
   "side": 1,
   "placeTimestamp": 1597375898867321,
   "matchAmt": "0",
   "orderType": 1,
   "positionEffect": 1,
   "marginType": 1,
   "initMarginRate": "0.01",
   "fcOrderId": "",
   "feeRate": "0.00025",
   "contractUnit": "0.01",
   "matchQty": "0",
   "orderStatus": 2,
   "avgPrice": "0",
   "stopPrice": "0",
   "orderSubType": 0,
   "stopCondition": 0
 }

           */

            Logger.Debug("ParseOrder:" + token.ToString());
            ExchangeOrderResult fullOrder;
            lock (fullOrders)
            {
                bool had = fullOrders.TryGetValue(token["orderId"].ToStringInvariant(), out fullOrder);
                ExchangeOrderResult result = new ExchangeOrderResult();

                result.Amount = token["quantity"].ConvertInvariant<decimal>();
                result.Price = token["price"].ConvertInvariant<decimal>();
                result.IsBuy = token["side"].ToStringInvariant().EqualsWithOption("1");
                result.OrderDate = (Convert.ToDouble(token["matchTime"].ToString()) / 1000d).UnixTimeStampToDateTimeMilliseconds();
                result.OrderId = token["orderId"].ToStringInvariant();
                result.MarketSymbol = token["symbol"].ToStringInvariant();
                result.AveragePrice = token["avgPrice"].ConvertInvariant<decimal>();
                //StopPrice = token["stopPx"].ConvertInvariant<decimal>(),

                result.AmountFilled =token["filledQuantity"].ConvertInvariant<decimal>();

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
                    string orderStatus = token["orderStatus"].ToStringInvariant();
                    string statu = "";
                    if (orderStatus == "0" || orderStatus == "1" || orderStatus == "2")
                    {
                        statu = "new";
                    }
                    else if (orderStatus == "3" || orderStatus == "4" || orderStatus == "5" || orderStatus == "7")
                    {
                        statu = "open";
                    }
                    else if (orderStatus == "6" || orderStatus == "2" || orderStatus == "8")
                    {
                        statu = "closed";
                    }

                    switch (statu)
                    {
                        case "new"://部分成交的时候 也是 在 new 状态
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
                        case "open":
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
                        case "closed":
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
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);
            decimal maxDatas = 3600;
            List<MarketCandle> candles = new List<MarketCandle>();
            //GET /markets/{market_name}/candles?resolution={resolution}&limit={limit}&start_time={start_time}&end_time={end_time}
            int daySeconds = 24 * 60 * 60;
            if (startDate != null && endDate != null)
            {

                int duringDays = Convert.ToInt32(Math.Ceiling((endDate.Value - startDate.Value).TotalDays));
                int allSeconds = daySeconds * duringDays;
                decimal allNum = allSeconds / periodSeconds;
                int times = Convert.ToInt32(Math.Ceiling(allNum / maxDatas));

                double startDateSeconds = startDate.Value.UnixTimestampFromDateTimeSeconds();
                double endDateSeconds = endDate.Value.UnixTimestampFromDateTimeSeconds();
                int perTime = Convert.ToInt32(Math.Min(maxDatas, Convert.ToInt32((endDateSeconds - startDateSeconds) / periodSeconds)) * periodSeconds);
                for (int i = 0; i < times; i++)
                {
                    if (i % 11 == 0)//避免超限制
                        await Task.Delay(1000);
                    double theStartDateSeconds = startDateSeconds + i * perTime;
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
                //  /api/v1/futureQuot/queryCandlestick
                string periodString = PeriodSecondsToString(periodSeconds);
                string url = $"/exchange/api/v1/future/market/klines?symbol={symbol}&range={periodString}&point=300";
                var obj = await MakeJsonRequestAsync<JToken>(url);
                foreach (JArray t in obj["datas"])
                {
                    MarketCandle candle = new MarketCandle
                    {
                        ExchangeName = this.Name,
                        OpenPrice = t[1].ConvertInvariant<decimal>(),
                        HighPrice = t[2].ConvertInvariant<decimal>(),
                        LowPrice = t[3].ConvertInvariant<decimal>(),
                        ClosePrice = t[4].ConvertInvariant<decimal>(),
                        BaseCurrencyVolume = t[5].ConvertInvariant<double>(),
                        Name = marketSymbol,
                        PeriodSeconds = periodSeconds,
                        Timestamp = CryptoUtility.ParseTimestamp(t[0], TimestampType.UnixMilliseconds)
                    };
                    candles.Add(candle);
                }
                //candles.Reverse();
            }
            return candles;
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

            List<ExchangeMarginPositionResult> positionList = new List<ExchangeMarginPositionResult>();
            GetSymbolAndContractCode(marketSymbol, out string symbol, out string contractCode);  //[0]symbol [1]contract_type

            var payload = await GetNoncePayloadAsync();
            //payload.Add("contract_code", contractType);
            JToken token = await MakeJsonRequestAsync<JToken>($"/exchange/api/v1/future/positions", BaseUrl, payload, "GET");
            int count = 0;
            foreach (JToken position in token["datas"])
            {

                if (position["contractId"].ToStringInvariant().Equals(contractCode))
                {
                    count++;
                    bool isBuy = position["posiQty"].ConvertInvariant<decimal>() >0;
                    decimal position_margin = position["initMargin"].ConvertInvariant<decimal>();
                    //decimal currentPrice = position["cost_hold"].ConvertInvariant<decimal>();
                    //Logger.Debug("GetOpenPositionAsync:" + position.ToString());
                    var positionR = new ExchangeMarginPositionResult()
                    {
                        MarketSymbol = marketSymbol,
                        Amount = 1 * position["posiQty"].ConvertInvariant<decimal>(),
                        //LiquidationPrice = position["liquidationPrice"].ConvertInvariant<decimal>(),
                        
                    };
                    positionR.BasePrice =Math.Abs( position["openAmt"].ConvertInvariant<decimal>() / positionR.Amount)* perRate;
                    decimal openUse = positionR.BasePrice / Math.Abs(positionR.Amount);//单位btc
//                     if (isBuy)
//                         positionR.LiquidationPrice = Math.Ceiling(1 / ((1 / positionR.BasePrice) + (position_margin / Math.Abs(positionR.Amount))));
//                     else
//                         positionR.LiquidationPrice = Math.Floor(1 / ((1 / positionR.BasePrice) - (position_margin / Math.Abs(positionR.Amount))));
                    //positionR.LiquidationPrice = await GetLiquidationPriceAsync(symbol);
                    Logger.Debug("Buy：" + Math.Ceiling(1 / ((1 / positionR.BasePrice) + (position_margin / Math.Abs(positionR.Amount)))) + "  Sell:" + Math.Floor(1 / ((1 / positionR.BasePrice) - (position_margin / Math.Abs(positionR.Amount)))));
                    Logger.Debug("GetOpenPositionAsync " + count + positionR.ToString());
                    positionList.Add(positionR);
                }
            }
            return positionList;
        }
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

    public partial class ExchangeName { public const string ZBGDM = "ZBGDM"; }
    public partial class ExchangeFee
    {
    }
}
