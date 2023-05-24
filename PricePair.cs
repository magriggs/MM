namespace MM
{
    public class PositionPair
    {
        public TradeType Type { get; set; }
        public double Amount { get; set; }
        public double AveragePrice { get; set; }
    }

    public enum MarketMakerPriceType
    {
        spread = 0,
        liquidation
    }

    public class PricePair
    {
        public PricePair()
        {

        }
        public PricePair(double bid, double bidsize, double ask, double asksize)
        {
            this.Bid = bid;
            this.Ask = ask;
            this.BidSize = bidsize;
            this.AskSize = asksize;
        }

        public double? Bid;
        public double? Ask;
        public double? BidSize;
        public double? AskSize;

        public override string ToString()
        {
            return $"b:{Bid.ToString()},bs:{BidSize.ToString()},a:{Ask.ToString()},as:{AskSize.ToString()}";
        }

        public MarketMakerPriceType BidPriceType { get; set; }
        public MarketMakerPriceType AskPriceType { get; set; }
    }
}
