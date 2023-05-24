using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MM
{
    public class Stats
    {
        public static double GetNormDist(double mu, double sigma, double x)
        {
            return (1 / (sigma * Math.Sqrt(2 * Math.PI)) * Math.Exp(-0.5 * Math.Pow(((x - mu) / sigma),2)));
        }
    }
}
