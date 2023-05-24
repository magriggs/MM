using System;
using System.Dynamic;

namespace MM
{
    public abstract class InformedPriceTaker : AbstractPriceTaker
    {
        private MatchingEngine _market;
        private Order _currentOrder;
        private readonly double MAX_TRADE_SIZE = 10d;
        private readonly double MIN_TRADE_SIZE = 5d;

        protected InformedPriceTaker(MatchingEngine market, string name="IPT")
        {
            _market = market;
            Name = name;
        }

        public override void Run(object o)
        {
            double underlyingPrice = (double)o;
            PricePair quote = _market.GetBestBidOffer();
            if (_currentOrder != null && _currentOrder.Fullyfilled == false)
            {
                logger.log($"{Name},Cancel:{_currentOrder.GetShortOrderId()}");
                _market.Cancel(_currentOrder);
                _currentOrder = null;
            }

            if (quote != null)
            {
                if (underlyingPrice <= quote.Bid)
                {
                    //  underlyingPrice is less than market designatedMarketMaker will buy for, so sell to the MM
                    _currentOrder = new Order(TradeType.sell, quote.Bid.GetValueOrDefault(), Math.Max(MIN_TRADE_SIZE, MAX_TRADE_SIZE), _orderFilledCallback);
                    Log($"{_currentOrder}, signal: {underlyingPrice}, BBO: {quote}");
                    if (_market.SubmitOrder(_currentOrder) == false)
                        _currentOrder = null;
                    else                    
                        NumberOfTrades++;
                }
                else if (underlyingPrice >= quote.Ask)
                {
                    //  underlyingPrice is more than the market designatedMarketMaker will sell for, so buy from the MM
                    _currentOrder = new Order(TradeType.buy, quote.Ask.GetValueOrDefault(), Math.Max(MIN_TRADE_SIZE, MAX_TRADE_SIZE), _orderFilledCallback);
                    Log($"{_currentOrder}, signal: {underlyingPrice}, BBO: {quote}");
                    if (_market.SubmitOrder(_currentOrder) == false)
                        _currentOrder = null;
                    else
                        NumberOfTrades++;
                }
                else
                {
                    Log($"No trade,signal: {underlyingPrice}, BBO: {quote}");
                    NumberOfNoTrades++;
                }
            }
            else
            {
                logger.log($"No trade,signal: {underlyingPrice}, BBO: no quote");
                NumberOfNoTrades++;
            }
        }
    }
}
