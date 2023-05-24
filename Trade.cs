namespace MM
{
    public class Trade
    {
        public Trade(TradeType type, double price, double amount)
        {
            this.Type = type;
            this.price = price;
            this.amount = amount;
        }
        public TradeType Type;
        public double price;
        public double amount;

        public override string ToString()
        {
            return $"Trade,{Type}:{amount}@{price}";
        }

    }

}
