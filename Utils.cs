using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MM
{
    internal class Utils
    {
        public static double CalculateRealisedPnl(List<Fill> fills, double lastPrice, out double unrealisedPnl, out double unrealisedUnits, out double unrealisedAveragePrice)
        {
            double realpnl = 0d;

            var longUnits = 0d;
            var shortUnits = 0d;
            var longQ = new Queue<Fill>();
            var shortQ = new Queue<Fill>();
            TradeType unrealisedDirection = TradeType.none;
            unrealisedPnl = 0d;

            unrealisedUnits = 0;

            foreach (var f in fills)
            {
                if (f.OriginalOrder.type == TradeType.buy)
                {
                    longQ.Enqueue(f);
                    longUnits += f.Amount;
                }
                else if (f.OriginalOrder.type == TradeType.sell)
                {
                    shortQ.Enqueue(f);
                    shortUnits += f.Amount;
                }
            }

            Queue<Fill> queueWithLess = null;
            Queue<Fill> queueWithMore = null;

            if (longUnits > shortUnits)
            {
                queueWithMore = longQ; queueWithLess = shortQ;
                unrealisedDirection = TradeType.buy; // if anything is unrealised, it will be that we bought too much 
            }
            else
            {
                queueWithMore = shortQ; queueWithLess = longQ;
                unrealisedDirection = TradeType.sell; // if anything is unrealised, it will be that we sold too much
            }

            double currAmount = 0d;
            Fill currMore = null;
            while (queueWithMore.Count > 0)
            {


                if (currAmount == 0d)
                {
                    currMore = queueWithMore.Dequeue();
                    currAmount = currMore.Amount;
                }

                while (currAmount > 0 && queueWithLess.Count > 0)
                {
                    var currLess = queueWithLess.Dequeue();
                    if (currLess.Amount <= currAmount)
                    {
                        currAmount -= currLess.Amount;
                        realpnl += Pnl(currMore, currLess, currLess.Amount);
                    }
                    else
                    {
                        Fill modifiedFill = new Fill(currLess.Price, currLess.Amount - currAmount, currLess.OriginalOrder);
                        realpnl += Pnl(currMore, modifiedFill, currAmount); // add on pnl
                        currAmount = 0d; // this 'more' queue fill is done, its remaining amount is zero
                        queueWithLess.Enqueue(modifiedFill); // put the a new fill back on the queue, but with a reduced quantity
                    }
                }

                if (queueWithLess.Count == 0)
                {
                    if (currAmount > 0)
                    {
                        queueWithMore.Enqueue(new Fill(currMore.Price, currAmount, currMore.OriginalOrder));
                        currAmount = 0d;
                    }
                    break;
                }
            }

            double unrealisedValue = 0d;
            while (queueWithMore.Count > 0)
            {
                currMore = queueWithMore.Dequeue();
                currAmount = currMore.Amount;
                unrealisedUnits += currAmount;
                unrealisedValue += currAmount * currMore.Price;
                currAmount = 0d;
            }


            if (unrealisedUnits > 0)
                unrealisedAveragePrice = (double)(unrealisedValue / (double)unrealisedUnits);
            else
                unrealisedAveragePrice = 0;

            if (unrealisedDirection == TradeType.buy)
                unrealisedPnl += unrealisedUnits * (lastPrice - unrealisedAveragePrice);
            else if (unrealisedDirection == TradeType.sell)
                unrealisedPnl += unrealisedUnits * (unrealisedAveragePrice - lastPrice);

            return realpnl;
        }

        private static double Pnl(Fill f1, Fill f2, double units)
        {
            if (f1.OriginalOrder.type == TradeType.buy && f2.OriginalOrder.type == TradeType.sell)
            {
                return (f2.Price - f1.Price) * units;
            }
            else if (f1.OriginalOrder.type == TradeType.sell && f2.OriginalOrder.type == TradeType.buy)
            {
                return (f1.Price - f2.Price) * units;
            }
            else
            {
                return 0d;
            }
        }
    }
}
