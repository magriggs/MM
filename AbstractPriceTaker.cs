using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MM
{
    public abstract class AbstractPriceTaker
    {
        public ILocalLogger logger { get; set; }
        public int NumberOfTrades { get; protected set; }
        public int NumberOfNoTrades { get; protected set; }
        public OrderFilledDlg _orderFilledCallback;
        public List<Fill> Fills { get { return _fills; } }
        public string Name { get; protected set; }

        protected List<Fill> _fills = new List<Fill>();

        private object _lockobject = new object();
        private double longPosition = 0d;
        private double shortPosition = 0d;

        public abstract void Run(object o);
        
        public virtual void Log(string s)
        {
            logger.log($"{Name},{s}");
        }

        public void CalculatePnlValues(out double buyValue, out double sellValue, out double buyUnits, out double sellUnits)
        {
            lock(_lockobject)
            {
                buyValue = 0;
                sellValue = 0;
                buyUnits = 0;
                sellUnits = 0;

                foreach(var f in _fills)
                {
                    if (f.OriginalOrder.type == TradeType.buy)
                    {
                        buyValue += f.Amount * f.Price;
                        buyUnits += f.Amount;
                    }
                    else if (f.OriginalOrder.type == TradeType.sell)
                    {
                        sellValue += f.Amount * f.Price;
                        sellUnits += f.Amount;
                    }
                }
            }
            logger.Flush();
        }

        public double CalculatePnl(double lastPrice, out double unrealisedPnl, out double unrealisedUnits, out double unrealisedAveragePrice)
        {
            lock (_lockobject)
            {
                return Utils.CalculateRealisedPnl(_fills, lastPrice, out unrealisedPnl, out unrealisedUnits, out unrealisedAveragePrice);
            }
        }

        public AbstractPriceTaker(string name="")
        {
            this._orderFilledCallback = OrderFillCallback;
            Name = name;
        }

        void OrderFillCallback(Fill fill)
        {
            lock (_lockobject)
            {
                if (fill.Amount != fill.OriginalOrder.size)
                    logger.log($"{Name},PartialFill,{fill},{fill.OriginalOrder}");

                _fills.Add(fill);
                if (fill.OriginalOrder.type == TradeType.buy)
                    longPosition += fill.Amount;
                else if (fill.OriginalOrder.type == TradeType.sell)
                    shortPosition += fill.Amount;

                logger.log($"{Name},Fill,{fill},{fill.OriginalOrder.type}");
                logger.log($"{Name},Position,long:{longPosition},short:{shortPosition}");
            }
        }
    }
}
