namespace MM
{
    public class PerfectInformedPriceTaker : InformedPriceTaker
    {
        public PerfectInformedPriceTaker(MatchingEngine market, string name="PIPT") : base(market, name)
        {

        }

        public override void Log(string s)
        {
            logger.logline($"Perfect,{s}");
        }
    }
}
