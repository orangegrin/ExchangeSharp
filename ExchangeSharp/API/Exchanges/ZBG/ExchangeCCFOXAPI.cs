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
    public sealed partial class ExchangeCCFOXAPI : ExchangeAPI
    {//https://apitest.ccfox.com/api/v1/futureQuot/querySnapshot?contractId=1000002
     //正式用
     //         public override string BaseUrl { get; set; } = "http://zbgapi.ccfox.internal";//"https://apitest.ccfox.com";
     //         public override string BaseUrlWebSocket { get; set; } = "ws://futurezbgws.ccfox.internal/socket.io/?EIO=3&transport=websocket";//"wss://futurewstest.ccfox.com/socket.io/?EIO=3&transport=websocket"

        //测试用
        //         public override string BaseUrl { get; set; } = "https://apitest.ccfox.com";
        //         public override string BaseUrlWebSocket { get; set; } = "wss://futurewstest.ccfox.com/socket.io/?EIO=3&transport=websocket";

        //新测试用
        public override string BaseUrl { get; set; } = "https://api-test.ccfox.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://futurews-test.ccfox.com/socket.io/?EIO=3&transport=websocket";
        //https://api.ccfox.com  正式
        //wss://futurews.ccfox.com 正式

        //         public override string BaseUrl { get; set; } = "https://testnet.bitmex.com/api/v1";
        //         public override string BaseUrlWebSocket { get; set; } = "wss://testnet.bitmex.com/realtime";

        private SortedDictionary<long, decimal> dict_long_decimal = new SortedDictionary<long, decimal>();
        private SortedDictionary<decimal, long> dict_decimal_long = new SortedDictionary<decimal, long>();
        private string OrderIdStart;
        private int OrderNum;
        public ExchangeCCFOXAPI()
        {
            RequestWindow = TimeSpan.Zero;
            NonceStyle = NonceStyle.ExpiresUnixSeconds;

            // make the nonce go 10 seconds into the future (the offset is subtracted)
            // this will give us an api-expires 60 seconds into the future
            NonceOffset = TimeSpan.FromSeconds(10.0);

            MarketSymbolSeparator = string.Empty;
            RequestContentType = "application/json";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookAlways;

            RateLimit = new RateGate(9000, TimeSpan.FromMinutes(10));
            OrderIdStart = (long)CryptoUtility.UtcNow.UnixTimestampFromDateTimeMilliseconds() + "_";
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
        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {


                string expires = (await GenerateNonceAsync()).ToString();
                var privateKey = PrivateApiKey.ToUnsecureString();
                var publicApiKey = PublicApiKey.ToUnsecureString();

               // publicApiKey = "c4e516d4-8f42-4c48-bbbe-8d74ad62d45c";
               // privateKey = "d4f74ddd-6875-48b9-827c-49473b80f24d";//"d4f74ddd-6875-48b9-827c-49473b80f24d";






                // convert nonce to long, trim off milliseconds
                var nonce = payload["nonce"].ConvertInvariant<long>();
                nonce = nonce ;
                payload.Remove("nonce");
                var msg = CryptoUtility.GetJsonForPayload(payload);
                var path = "";
                //var sign = $"{nonce}{request.Method}{request.RequestUri.AbsolutePath}{request.RequestUri.Query}{msg}";
                if (request.Method.Equals("GET")|| request.Method.Equals("DELETE"))
                {
                    /*
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
                    */

                    if ( !string.IsNullOrEmpty(request.RequestUri.Query))
                    {
                        var datas = request.RequestUri.Query.Split(new string[] { "=" }, StringSplitOptions.None);
                        string templ = "";
                        if (datas.Length==2)
                        {
                            templ = WebUtility.UrlDecode(datas[1]);
                            //request.RequestUri.Query = datas[0] + "=" + templ.HttpUrlEncode().Replace(" ", "");
                        }
                        path = request.RequestUri.AbsolutePath + datas[0] + "=" + templ.HttpUrlEncode().Replace(" ","");
                    }
                    else
                    {
                        msg = CryptoUtility.GetJsonForPayload(payload);
                        path = request.RequestUri.AbsolutePath;
                    }
                }
                else
                {
                    msg = CryptoUtility.GetJsonForPayload(payload);
                    path = request.RequestUri.AbsolutePath;
                }

                //var path = @"/api/v1/order?filter={""orderId"":""11548326910655928""}"; // request.RequestUri.AbsolutePath.UrlEncode(); 
                var sign = $"{request.Method}{path}{nonce}{msg}";

                string signature = CryptoUtility.SHA256Sign(sign, privateKey.ToBytesUTF8());

                Logger.Debug(PrivateApiKey.ToUnsecureString() + "    " + PublicApiKey.ToUnsecureString());
                // Logger.Debug(PublicApiKey.ToUnsecureString());
                request.AddHeader("apiKey", publicApiKey);
                request.AddHeader("signature", signature);
                //request.AddHeader("apiExpires", "15995600000");
                request.AddHeader("apiExpires", nonce.ToStringInvariant());
                //Logger.Debug("signature" + signature);
                if (!string.IsNullOrEmpty(SubAccount))
                {
                    request.AddHeader("FTX-SUBACCOUNT", SubAccount);
                }

                await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
            }
        }

        protected override Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload, string method)
        {
            if (method=="GET")
            {
                var datas = url.Query.Split(new string[] { "=" }, StringSplitOptions.None);
                string templ = "";
                if (datas.Length == 2)
                {
                    templ = WebUtility.UrlDecode(datas[1]);
                    url.Query = datas[0] + "=" + templ.HttpUrlEncode().Replace(" ", "");
                }
            }
            else
            {

            }
            return base.ProcessRequestUrl(url, payload, method);
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
            /*
             * 
             "[
  "match",
  {
    "messageType": 3002,
    "accountId": 19113,
    "lastId": 1,
    "currencyId": 999999,
    "totalBalance": "55000000",
    "available": "54999999.9605375",
    "frozenForTrade": "0.0394625",
    "initMargin": "0",
    "frozenInitMargin": "0.0385",
    "closeProfitLoss": "0",
    "posiMode": 0
  }
]"


             
             */

            Timer pingTimer = null;
            string lastMarketSymbol = "";
            //return base.OnGetOrderDetailsWebSocket(callback);
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                //Logger.Debug(str);
                if (str.Contains("connected") || str.Contains("subscribe") || str.Contains("realtime") || str.Contains("Pong") || str.Contains("pingInterval") || str.StartsWith("40") || str.StartsWith("3") 
                || str.Contains("auth") )
                {


                    if (str.Contains("pingInterval"))
                    {// subscription successful
                        if (pingTimer == null)
                        {
                            pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync("2"),
                                state: null, dueTime: 0, period: 20000); // send a ping every 15 seconds
                        }
                        return Task.CompletedTask;
                    }
                    else if (str.Contains("auth"))
                    {
                        string vv = @"42[""subscribe"",{ ""header"":{ ""type"":1003},""body"":{ ""topics"":[{""topic"":""match""}]}}]";
                        //Logger.Debug("  3send: " + vv.ToString());
                        _socket.SendMessageAsync(vv);
                    }
                    else if (str.StartsWith("3"))
                    {
                        callback(new ExchangeOrderResult() { MarketSymbol = "pong" });
                    }
                    return Task.CompletedTask;
                }
                if (str.StartsWith("42"))
                {
                    //"42["match",{"messageType":3002,"accountId":19113,"lastId":1,"currencyId":999999,"totalBalance":"54999999.77646","available":"54999995.1984","frozenForTrade":"4.57806","initMargin":"0","frozenInitMargin":"4.4664","closeProfitLoss":"0","posiMode":0}]"
                    //"42["match",{"messageType":3004,"accountId":19113,"contractId":999999,"orderId":"11597373409560555","clientOrderId":"56b23b710c104cd8b3a76d10de8ff9d7","price":"11155","quantity":"1","leftQuantity":"1","side":1,"placeTimestamp":1597375898866716,"matchAmt":"0","orderType":1,"positionEffect":1,"marginType":1,"initMarginRate":"0.01","fcOrderId":"","feeRate":"0.00025","contractUnit":"0.01","matchQty":"0","orderStatus":2,"avgPrice":"0","stopPrice":"0","orderSubType":0,"stopCondition":0}]"
                    //"42["match",{"messageType":3012,"lastId":5,"applId":2,"accountId":19113,"posis":[{"contractId":999999,"marginType":1,"initMarginRate":"0.01","maintainMarginRate":"0.005","initMargin":"0","extraMargin":"0","openAmt":"0","posiQty":"0","frozenOpenQty":"4","frozenCloseQty":"0","posiStatus":0,"closeProfitLoss":"0","contractUnit":"0.01","posiSide":0}]}]"


                    str = str.Replace("42[", "[");
                    if (str.Contains("\"messageType\":3004")|| str.Contains("\"messageType\":3012") || str.Contains("\"messageType\":3002"))
                    {
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
                        JToken orderData = token[1];
                        ExchangeOrderResult order = null;
                        if (str.Contains("\"messageType\":3004"))
                        {
                            Logger.Debug("orderData" + orderData.ToString());
                            if (orderData["error"] != null)
                            {
                                Logger.Info(token["error"].ToStringInvariant());
                                return Task.CompletedTask;
                            }


                            order = ParseOrder(orderData);
                            callback(order);
                        }
                        else if (str.Contains("\"messageType\":3012"))//仓位信息
                        {
                            Logger.Debug("orderData" + orderData.ToString());
                            if (orderData["error"] != null)
                            {
                                Logger.Info(token["error"].ToStringInvariant());
                                return Task.CompletedTask;
                            }
                            int ct = 0;
                            foreach (JToken item in orderData["posis"])
                            {
                                decimal amount = Convert.ToDecimal(item["posiQty"].ToString());
                                decimal allPrice = Convert.ToDecimal(item["openAmt"].ToString());
                                var od = new ExchangeOrderResult();
                                od.MarketSymbol = item["contractId"].ToString();
                                od.Amount = Convert.ToDecimal(item["posiQty"].ToString());
                                od.AmountFilled = Convert.ToDecimal(item["closeProfitLoss"].ToString());//实际亏损
                                od.Message = "Position";
                                od.Price = Math.Abs(allPrice) *100;//1张 = 0.01rhi;总金额
                                callback(od);
                                ct++;
                                lastMarketSymbol = od.MarketSymbol;
                            }
                            if (ct==0)//如果仓位为0 返回 的是空数组
                            {
                                var od = new ExchangeOrderResult();
                                od.MarketSymbol = lastMarketSymbol;
                                od.Amount = 0;
                                od.Price = 0;
                                od.Message = "Position";
                                callback(od);
                            }
                        }
                        else if (str.Contains("\"messageType\":3002"))//用户资产信息
                        {
                            Logger.Debug("orderData" + orderData.ToString());
                            if (orderData["error"] != null)
                            {
                                Logger.Info(token["error"].ToStringInvariant());
                                return Task.CompletedTask;
                            }

                            string currencyId = orderData["currencyId"].ToString();
                            decimal totalBalance = Convert.ToDecimal(orderData["totalBalance"].ToString());
                            decimal available = Convert.ToDecimal(orderData["available"].ToString());
                            decimal closeProfitLoss = Convert.ToDecimal(orderData["closeProfitLoss"].ToString());
                            var od = new ExchangeOrderResult();
                            od.MarketSymbol = currencyId;//币种
                            od.Amount = totalBalance;//总资产
                            od.AmountFilled = available;//可用资金
                            od.Message = "Account";
                            //od.Price = Math.Abs(allPrice) * 100;//1张 = 0.01rhi;总金额
                            callback(od);
                        }
                    }

                }


                return Task.CompletedTask;
            }, async (_socket) =>
            {
                //连接中断也不应该删除历史信息
                //fullOrders.Clear();
                var payloadJSON = await GeneratePayloadJSON();


                Logger.Debug(payloadJSON.ToString());

                await _socket.SendMessageAsync(payloadJSON);
                //await _socket.SendMessageAsync(new { op = "subscribe", channel = "orders" });
            }, async (_socket) =>
            {
                pingTimer.Dispose();
                pingTimer = null;

            });
        }

        private async Task<string> GeneratePayloadJSON()
        {
            //verb + path + expires + data
            string verb = "GET";
            string path = "GET/realtime";
            string data = "";

            string myexpires = (await GenerateNonceAsync()).ToString();
            var privateKey = PrivateApiKey.ToUnsecureString();
            var publicApiKey = PublicApiKey.ToUnsecureString();
            /*
            [
    "auth",
    {
        "header": {
            "type": 1001
        },
        "body": {
            "apiKey": "AccessKey",
            "expires": "expires",
            "signature": "signature"
        }
    }
]*/

            //publicApiKey = "c4e516d4-8f42-4c48-bbbe-8d74ad62d45c";
            //privateKey = "d4f74ddd-6875-48b9-827c-49473b80f24d";


            var sign = verb + path + myexpires + data;
            string str = "";
            string mysignature = CryptoUtility.SHA256Sign(sign, privateKey.ToBytesUTF8());
            Dictionary<string, object> payload;
            if (!string.IsNullOrEmpty(SubAccount))
            {
                payload = new Dictionary<string, object>
                {
                    { "args", new { key = PublicApiKey.ToUnsecureString() ,sign = mysignature ,time = myexpires,subaccount=SubAccount } },
                     { "op", "login"}
                };
            }
            else
            {
                payload = new Dictionary<string, object>
                {
                    { "auth",new {header=new {type=1001 },body=new{
                        apiKey =publicApiKey ,
                        expires = myexpires,
                        signature = mysignature
                    } } } ,

                };
                str = @"42[""auth"",{""header"":{""type"":1001},""body"":{""apiKey"":""publicApiKey"",""expires"":""myexpires"",""signature"":""mysignature""}}]";
                str = str.Replace("publicApiKey", publicApiKey).Replace("myexpires", myexpires).Replace("mysignature", mysignature);


            }

            return str;
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
            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                //Logger.Debug(str);
                if (str.Contains("connected") || str.Contains("subscribe") || str.Contains("realtime") || str.Contains("Pong") || str.Contains("pingInterval") || str.StartsWith("40") || str.StartsWith("3"))
                {
                    if (str.Contains("pingInterval"))
                    {// subscription successful
                        if (pingTimer == null)
                        {
                            pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync("2"),
                                state: null, dueTime: 0, period: 20000); // send a ping every 15 seconds
                        }
                        return Task.CompletedTask;
                    }
                    return Task.CompletedTask;
                }
                if (str.StartsWith("42"))
                {
                    str = str.Replace("42[", "[");
                    if (str.Contains("future_snapshot_depth"))
                    {
                        
                    }
                   
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
                JArray bids = null;
                JArray asks = null;
                //Logger.Debug("token[1]"+ token[1]);
                if (token[1]["bids"] != null)
                {
                    bids = token[1]["bids"] as JArray;
                }
                if (token[1]["asks"] != null)
                {
                    asks = token[1]["asks"] as JArray;
                }
                ExchangeOrderBook book = new ExchangeOrderBook();
                book.SequenceId = token[1]["time"].ConvertInvariant<long>();
                var price = 0m;
                var size = 0m;
                var marketSymbol = marketSymbols[0];
                book.MarketSymbol = marketSymbol;
                void applyData(JArray data, bool isBuy)
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
                lock(book)
                {
                    if (token[1]["bids"] != null)
                    {
                        applyData(bids, true);
                    }
                    if (token[1]["asks"] != null)
                    {
                        applyData(asks, false);
                    }
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
                //42["subscribe",{"header":{"type":1003},"body":{"topics":[{"topic":"future_depth","params":{"symbols":[{"symbol":1000000}]}},{"topic":"future_snapshot_indicator","params":{"symbols":[{"symbol":1000000}]}},{"topic":"future_snapshot_depth","params":{"symbols":[{"symbol":1000000}]}},{"topic":"future_tick","params":{"symbols":[{"symbol":1000000}]}}]}}]

                var str = new { header = new { type = 1003 }, body = new { topics = new[] { new { topic = "future_snapshot_depth", @params = new { symbols = new[] { new { symbol = Convert.ToInt32(marketSymbols[0]) } } } } } } } ;

                var v = JsonConvert.SerializeObject(str);
                Logger.Debug(v);
                v = @"[""subscribe""," + v.ToString() + "]";
                //str = str.Replace(@"""symbol""", marketSymbols[0]);

                Logger.Debug("  1send: " + v.ToString());
                string vv = "42" + v.ToString();
                Logger.Debug("  2send: "+vv.ToString());
                vv = @"42[""subscribe"",{ ""header"":{ ""type"":1003},""body"":{ ""topics"":[{""topic"":""future_snapshot_depth"",""params"":{""symbols"":[{""symbol"":TheSymbol}]}}]}}]";
                vv = vv.Replace("TheSymbol", marketSymbols[0]);
                Logger.Debug("  3send: " + vv.ToString());
                //@"42[""subscribe"",{ ""header"":{ ""type"":1003},""body"":{ ""topics"":[{ ""topic"":""future_depth"",""params"":{ ""symbols"":[{""symbol"":1000000}]}},{""topic"":""future_snapshot_indicator"",""params"":{""symbols"":[{""symbol"":1000000}]}},{""topic"":""future_snapshot_depth"",""params"":{""symbols"":[{""symbol"":1000000}]}},{""topic"":""future_tick"",""params"":{""symbols"":[{""symbol"":1000000}]}}]}}]";
                await _socket.SendMessageAsync(vv);
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
            JToken token = await MakeJsonRequestAsync<JToken>($"/wallet/balances", BaseUrl, payload);
            decimal totalAmount = 0;
            Logger.Debug(token.ToString());
            if (string.IsNullOrEmpty(symbol))//获取全部
            {
                foreach (var item in token)
                {
                    var coin = item["coin"].ToStringInvariant();
                    var count = item["usdValue"].ConvertInvariant<decimal>();
                    totalAmount += count;
                }
            }
            else
            {
                foreach (var item in token)
                {
                    var coin = item["coin"].ToStringInvariant();
                    var count = item["total"].ConvertInvariant<decimal>();
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
            if (marketSymbol == null)
            {
                throw new APIException("marketSymbol can not be null!!!!");
            }
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            //string query = "/order";
            string s = $"filter={{\"contractId\": {marketSymbol}}}";
            //s= $"filter={{\"contractId\": \"{marketSymbol}\"}}";
            //s = "";
            string query = "/api/v1/future/queryActiveOrder?"+s ;
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrderOpenOrder(order));
                // Logger.Debug(token.ToString());
            }
            Logger.Debug("OnGetOpenOrderDetailsAsync " + orders.Count);
            return orders;
        }
        /// <summary>
        /// 获取当前 止盈订单
        /// </summary>
        /// <param name="marketSymbol"></param>
        /// <returns></returns>
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenProfitOrderDetailsAsync(string marketSymbol = null, OrderType orderType = OrderType.MarketIfTouched)
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
            else if (orderType == OrderType.LimitIfTouched || orderType == OrderType.MarketIfTouched)
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
                token = await MakeJsonRequestAsync<JToken>("/api/v1/future/order/all", BaseUrl, payload, "DELETE");
            }
            else
            {
                if (marketSymbol == null)
                {
                    Logger.Error("marketSymbol can not be null!!");
                    throw new Exception("marketSymbol can not be null!!");
                }
                //payload["filter"] = new { contractId = 999999, originalOrderId = orderId 
                var url = "/api/v1/future/order" + $"?filter=" + JsonConvert.SerializeObject(new { contractId = Convert.ToUInt32(marketSymbol), originalOrderId = orderId });
                //var url = "/api/v1/future/order" + $"?filter=" + @"{""orderId"":""11548740843018185"",""contractId"":10000}";
                Logger.Debug("url:"+url);
                //var url = "/api/v1/future/order" + $"?filter=" + JsonConvert.SerializeObject(new { orderId = "11548326910655928" });
                token = await MakeJsonRequestAsync<JToken>(url, BaseUrl, payload, "DELETE");
            }
            try
            {
                //Logger.Debug("");
                //result = ParseOd(token);
                if (!(token["code"].ToString().Equals("0")))
                {
                    throw new Exception("CancelOrderEx:" + token.ToString());
                }
            }
            catch (Exception ex)
            {
                throw new Exception("CancelOrderEx:" + token.ToString());
            }
        }
        public override bool ErrorNeedNotCareError(Exception ex)
        {
            return ex.ToString().Contains("502 Bad Gateway");
        }

        public override bool ErrorCancelOrderIdNotFound(Exception ex)
        {
            return ex.ToString().Contains("order no exist");
        }

        public override bool ErrorPlanceOrderPrice(Exception ex)
        {
            return ex.ToString().Contains("invaid price")|| ex.ToString().Contains("passive order should not have counterparty")|| ex.ToString().Contains("counter party order no exist");
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {

            /*
            order.Amount = order.Amount;
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
                currentPostionAmount = currentPostion.Amount;
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
                returnResult.Amount = returnResult.Amount;
                returnResult.IsBuy = order.IsBuy;
            }
            else//仓位和加仓方向相反
            {
                returnResult.Amount = Math.Abs(currentPostionAmount - order.Amount);
                returnResult.Amount = returnResult.Amount;
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
                downReturnResult.Amount = amount ;
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
                m_returnResult.Amount = amount;
                returnResult = m_returnResult;
            }
            Logger.Debug("    OnPlaceOrderAsync 当前仓位：" + returnResult.ToExcleString());
            return returnResult;
            */

            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            JToken token;
            //sdfdsf错误错误
            AddOrderToPayload(order, payload);
            token = await MakeJsonRequestAsync<JToken>("/api/v1/future/order", BaseUrl, payload, "POST");

            /*{{
  "code": 101040740,
  "msg": "invaid price",
  "data": null
}}
*/
            if (token["code"].ToString()!="0")
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

                AddOrderToPayloadOrders(order, subPayload);
                orderRequests.Add(subPayload);
            }
            payload["orders"] = orderRequests;

            JToken token = await MakeJsonRequestAsync<JToken>("/api/v1/future/orders", BaseUrl, payload, protocol);
            Logger.Debug("mOnPlaceOrdersAsync:"+token);
            if (token["reject"] != null)
            {
                bool error = false;
                foreach (JToken orderResultToken in token["reject"])
                {
                    error = true;
                }
                if (error)
                {
                    throw new Exception("order is filed:   " + token["reject"].ToString());
                }
                
            }
            if (token["succ"]!=null)
            {
       
                int i = 0;
                foreach (JToken orderResultToken in token["succ"])
                {
                    JArray a = JArray.Parse(orderResultToken.ToString());
                    ExchangeOrderResult result = ParseOd(orders[i], a);
                    results.Add(result);
                    i++;
                }
            }
            return results.ToArray();
        }

        private static ExchangeOrderResult ParseOd(ExchangeOrderRequest order, JArray a)
        {
            return new ExchangeOrderResult()
            {
                OrderId = a[1].ToString(),
                MarketSymbol = order.MarketSymbol,
                Amount = order.Amount,
                IsBuy = order.IsBuy,
                OrderDate = DateTime.UtcNow,
                Price = order.Price,
            };
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
            JToken token = await MakeJsonRequestAsync<JToken>($"/positions", BaseUrl, payload, "GET");
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
                    poitionR.BasePrice = poitionR.Amount == 0 ? 0 : cost / poitionR.Amount;
                }
            }
            return poitionR;
        }
        private void AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            payload["contractId"] = Convert.ToInt32(order.MarketSymbol);
            payload["orderType"] = GetOrderType(order.OrderType);
            payload["side"] = order.IsBuy ? 1 : -1;
            payload["quantity"] = order.Amount;
            if (order.Price != 0)
            {
                payload["price"] = order.Price;
            }
            else
            {
                payload["postOnly"] = false;//市价单
                //payload["price"] = null;
            }
            if (order.ExtraParameters.TryGetValue("positionEffect", out object positionEffect))
            {
                payload["positionEffect"] = positionEffect;
            }
            else
            {
                Logger.Error("ExtraParameters: \"positionEffect\" must set value");
                throw new Exception("ExtraParameters: \"positionEffect\" must set value");
            }


            payload["marginType"] = 1;
            payload["marginRate"] = "0";

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


        private void AddOrderToPayloadOrders(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            payload["contractId"] = Convert.ToInt32(order.MarketSymbol);
            payload["orderType"] = GetOrderType(order.OrderType);
            payload["side"] = order.IsBuy ? 1 : -1;
            payload["orderQty"] = order.Amount;
            if (order.Price != 0)
            {
                payload["orderPrice"] = order.Price;
            }
            else
            {
                payload["postOnly"] = false;//市价单
                payload["orderPrice"] = null;
            }
            if (order.ExtraParameters.TryGetValue("positionEffect", out object positionEffect))
            {
                payload["positionEffect"] = positionEffect;
            }
            else
            {
                Logger.Error("ExtraParameters: \"positionEffect\" must set value");
                throw new Exception("ExtraParameters: \"positionEffect\" must set value");
            }

            payload["marginType"] = 1;
            payload["initRate"] = "0";
            //payload["orderSubType"] = 0;//0（默认值），1（被动委托），2（最近价触发条件委托），3（指数触发条件委托），4（标记价触发条件委托）

            if (order.StopPrice != 0)
                payload["stopPx"] = order.StopPrice;
            
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
        private ExchangeOrderResult ParseOrderOpenOrder(JToken token)
        {
            /*
             
             "applId": 2,
  "contractId": 1000022,
  "accountId": 19114,
  "clOrderId": "fc8d9fcfeee34aed8ad18f11b4fcba78",
  "side": 1,
  "orderPrice": "384.4",
  "orderQty": "51",
  "orderId": "11599551907005433",
  "orderTime": 1599562657692422,
  "orderStatus": 2,
  "matchQty": "0",
  "matchAmt": "0",
  "cancelQty": "0",
  "matchTime": 0,
  "orderType": 1,
  "timeInForce": 0,
  "feeRate": "0.00025",
  "markPrice": null,
  "avgPrice": "0",
  "positionEffect": 1,
  "marginType": 1,
  "initMarginRate": "0.01",
  "fcOrderId": "",
  "contractUnit": "0.1",
  "stopPrice": "0",
  "orderSubType": 0,
  "stopCondition": 0,
  "minimalQuantity": null,
  "deltaPrice": "0",
  "frozenPrice": "384.4"
             */
            ExchangeOrderResult result = new ExchangeOrderResult();

            result.Amount = token["orderQty"].ConvertInvariant<decimal>();
            result.Price = token["orderPrice"].ConvertInvariant<decimal>();
            result.IsBuy = token["side"].ToStringInvariant().EqualsWithOption("1");
            result.OrderDate = (Convert.ToDouble(token["orderTime"].ToString()) / 1000d).UnixTimeStampToDateTimeMilliseconds();
            result.OrderId = token["orderId"].ToStringInvariant();
            result.MarketSymbol = token["contractId"].ToStringInvariant();
            result.AveragePrice = token["orderPrice"].ConvertInvariant<decimal>();
            //StopPrice = token["stopPx"].ConvertInvariant<decimal>(),

            //result.AmountFilled = result.Amount - token["leftQuantity"].ConvertInvariant<decimal>();
            return result;
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
                result.OrderDate = (Convert.ToDouble(token["placeTimestamp"].ToString()) / 1000d).UnixTimeStampToDateTimeMilliseconds();
                result.OrderId = token["orderId"].ToStringInvariant();
                result.MarketSymbol = token["contractId"].ToStringInvariant();
                result.AveragePrice = token["avgPrice"].ConvertInvariant<decimal>();
                //StopPrice = token["stopPx"].ConvertInvariant<decimal>(),
                
                result.AmountFilled = result.Amount - token["leftQuantity"].ConvertInvariant<decimal>();

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
                    if (orderStatus == "0" || orderStatus == "1" || orderStatus == "2" )
                    {
                        statu = "new";
                    }else if (orderStatus == "3" || orderStatus == "4" || orderStatus == "5" || orderStatus == "7")
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
                string url = $"/api/v1/futureQuot/queryCandlestick?symbol={marketSymbol}&range={periodString}&point=300";
                var obj = await MakeJsonRequestAsync<JToken>(url);
                foreach (JArray t in obj["lines"])
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
                candles.Reverse();
            }
            return candles;
        }

        public override string PeriodSecondsToString(int seconds)
        {
            return (seconds * 1000) .ToString();
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
            JToken token = await MakeJsonRequestAsync<JToken>($"/api/v1/futureQuot/querySnapshot?contractId={marketSymbol}", BaseUrl);
            ExchangeOrderBook book = new ExchangeOrderBook();

            JArray bids = null;
            JArray asks = null;
            if (token["bids"] != null)
            {
                bids = token["bids"] as JArray;
            }
            if (token["asks"] != null)
            {
                asks = token["asks"] as JArray;
            }
            book.SequenceId = token["te"].ConvertInvariant<long>();
            var price = 0m;
            var size = 0m;
            book.MarketSymbol = marketSymbol;
            void applyData(JArray data, bool isBuy)
            {
                int count = 0;

                foreach (var d in data)
                {
                    count++;
                    if (count>maxCount)
                        break;
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

            return book;
        }

    }

    public partial class ExchangeName { public const string CCFOX = "CCFOX"; }
    public partial class ExchangeFee
    {
    }
}
