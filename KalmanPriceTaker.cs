using KalmanFilters;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MM
{
    public class KalmanPriceTaker : AbstractPriceTaker
    {
        private KalmanFilter1D filter;
        private List<double> prices;

        public KalmanPriceTaker(ILocalLogger logger)
        {
            base.logger = logger;
            prices = new List<double>();
        }

        public override void Log(string s)
        {
            logger.log(s);
        }

        public override void Run(object o)
        {
            if (filter == null)
            {
                prices.Add((double)o);
                if (prices.Count < 10)
                    return;
            }

            var measurementSigma = prices.PopulationStandardDeviation() * prices.PopulationStandardDeviation();
            filter = new KalmanFilter1D(200, 2, measurementSigma, 2 * measurementSigma);

            double price = (double)o;
            filter.Update(price);
        }
    }
}
