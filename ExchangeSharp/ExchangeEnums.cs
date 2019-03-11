using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    #region Enum
    public enum Side
    {
        Buy = 0,
        Sell,
    }
    //enum OrderType
    //{
    //    Limit = 0,
    //    Market,
    //}
    public enum OrderResultType
    {
        Unknown = 0,
        Filled,
        FilledPartially,
        Pending,
        Error,
        Canceled,
        PendingCancel,
    }
    #endregion
}
