using System;

namespace MM
{
    public class RandomPriceTaker : AbstractPriceTaker
    {
        MatchingEngine _book;
        private Random _rnd;
        private readonly int RANDOM_SIZE = 1;

        public RandomPriceTaker(MatchingEngine book, string name = "RPT") : base(name)
        {
            _book = book;
            _rnd = new Random();
        }
        public override void Run(object o)
        {
            Random rnd = new Random();
            PricePair quote = _book.GetBestBidOffer();
            Log($"BBO,{quote}");
            Order order = null;
            if (quote != null)
            {
                if (order != null)
                {
                    if (order.remsize > 0)
                    {
                        Log($"Cancelling {order.type} {order.size} @ {order.price}");
                        _book.Cancel(order);
                    }
                }

                int randomval = rnd.Next(10);
                if (randomval < 5 && quote.Bid.HasValue & quote.BidSize.HasValue)
                {
                    //  sell
                    int size = Math.Max(RANDOM_SIZE, _rnd.Next((int)quote.BidSize.GetValueOrDefault()));
                    order = new Order(TradeType.sell, quote.Bid.Value, size);
                    if (_book.SubmitOrder(order) == false)
                        _book.Cancel(order);
                    Log(order.ToString()); 
                }
                else if (randomval >= 5 && quote.Ask.HasValue && quote.AskSize.HasValue)
                {
                    //  buy
                    int size = Math.Max(RANDOM_SIZE, _rnd.Next((int)quote.AskSize.GetValueOrDefault()));
                    order = new Order(TradeType.buy, quote.Ask.GetValueOrDefault(), size);
                    if (_book.SubmitOrder(order) == false)
                        _book.Cancel(order);

                    Log(order.ToString());
                }
            }
            else
            {
                Log("No trade, Quote is null");
            }
        }

        public override void Log(string s)
        {
            logger.log($"Random,{s}");
        }
    }
}
