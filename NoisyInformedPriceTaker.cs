using System;

namespace MM
{
    public class NoisyInformedPriceTaker : InformedPriceTaker
    {
        public NoisyInformedPriceTaker(MatchingEngine market, string name="NIPT") : base(market, name)
        {
        }
    }
}
