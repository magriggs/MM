using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MM
{
    public enum TradeType { buy, sell, none };
    public enum MarketMakerSignalType { perfect, noisy }
    public enum MarketMakerState { Stopped, Running, Stopping }
    public class MarketMaker
    {
        public ILocalLogger logger { get; set; }

        private int ticksSinceTrade = 0;

        private List<Trade> _listOfTrades = new List<Trade>();
        private Trade _lastTrade = null;
        public PricePair myPrice;

        public string Name { get; }
        public MatchingEngine Book { get; set; }

        private object _tradesLockObject = new object();

        Order _buyOrder = null;
        Order _sellOrder = null;
        private long STOP_THREADS = 0;
        private Random rndSleep;
        
        private readonly SignalGenerator signalGenerator;
        private readonly MarketMakerSignalType signalType;
        private readonly SimulationParameters simParams;

        private bool LIQUIDATION_ENABLED = true;
        private bool ACTIVE_INVENTORY_CONTROL = true;
        private readonly double MAX_MM_INVENTORY_IMBALANCE = 80;
        
        public MarketMakerState MarketMakerState { get; private set; }

        public MarketMaker(SignalGenerator signalGenerator, ILocalLogger logger, SimulationParameters simParams, MatchingEngine engine, string name = "MM") : this(signalGenerator, logger, simParams, engine, simParams.MM_ORDER_SIZE, name)
        {

        }

        public MarketMaker(SignalGenerator signalGenerator, ILocalLogger logger, SimulationParameters simParams, MatchingEngine engine, double mm_order_size, string name = "MM")
        {
            this.Name = name;
            this.Book = engine;
            this.logger = logger;
            this.simParams = simParams;
            this.signalGenerator = signalGenerator;
            this.signalType = simParams.MARKET_MAKER_SIGNAL_TYPE;
            this.rndSleep = new Random();
        }

        public void FillCallback(Fill fillObject)
        {
            Trade t = new Trade(fillObject.OriginalOrder.type, fillObject.Price, fillObject.Amount);
            lock (_tradesLockObject)
            {
                _listOfTrades.Add(t);
                _lastTrade = t;
                ticksSinceTrade = 0;
                CalculatePosition(out PositionPair longPos, out PositionPair shortPos);
                var positionString = GetPositionString(longPos, shortPos);
                logger.log($"{Name},{fillObject.ToString()}, {positionString}");
            }            
        }

        public void Start()
        {
            if (simParams.FIXED_SPREAD <= 0)
            {
                logger.log($"{Name},Cannot start due to invalid FIXED_SPREAD value: {simParams.FIXED_SPREAD}");
                return;
            }

            this.MarketMakerState = MarketMakerState.Running;
        }

        public void Stop()
        {
            Book.Cancel(_buyOrder);
            Book.Cancel(_sellOrder);
            Interlocked.Exchange(ref STOP_THREADS, 1);
        }

        private double GetSignalPrice()
        {
            switch (signalType)
            {
                case MarketMakerSignalType.noisy:
                    return signalGenerator.GetNoisySignal(simParams.SIGNAL_NOISE_MAGNITUDE);
                case MarketMakerSignalType.perfect:
                default:
                    return signalGenerator.GetPerfectSignal();
            }
        }

        public void RunOnce()
        {
            if (IsStarted() == false)
                return;

            myPrice = GetMyPrice(GetSignalPrice());

            string logString = $"{Name}, ";

            Order newBuyOrder = null;
            Order newSellOrder = null;

            if (_buyOrder != null)
            {
                if (myPrice.Bid != _buyOrder.price || _buyOrder.remsize <= 0)
                {
                    logger.log($"{Name},Cancel,{_buyOrder}");
                    if (Book.Cancel(_buyOrder)==false)
                    {
                        logger.log($"{Name},Failed to cancel {_buyOrder}");
                        _buyOrder = null;
                    }
                    if (myPrice.Bid.HasValue && myPrice.BidSize.HasValue)
                    {
                        newBuyOrder = new Order(TradeType.buy, myPrice.Bid.GetValueOrDefault(), myPrice.BidSize.GetValueOrDefault());
                        newBuyOrder.FillCallback += this.FillCallback;

                        logger.log($"{Name},{newBuyOrder}");
                    }
                    else
                        logger.log($"{Name},No Buy Order,myPrice.Bid:{myPrice.Bid},myPrice.BidSize:{myPrice.BidSize}");
                }
            }
            else
            {
                if (myPrice.Bid.HasValue && myPrice.BidSize.HasValue)
                {
                    newBuyOrder = new Order(TradeType.buy, myPrice.Bid.GetValueOrDefault(), myPrice.BidSize.GetValueOrDefault());
                    newBuyOrder.FillCallback += this.FillCallback;

                    logger.log($"{Name},{newBuyOrder}");
                }
                else
                    logger.log($"{Name},No Buy Order,myPrice.Bid:{myPrice.Bid},myPrice.BidSize:{myPrice.BidSize}");
            }

            if (_sellOrder != null)
            {
                if (myPrice.Ask != _sellOrder.price || _sellOrder.remsize <= 0)
                {
                    logger.log($"{Name},Cancel,{_sellOrder}");
                    if(Book.Cancel(_sellOrder) == false)
                    {
                        logger.log($"{Name},Failed to cancel {_sellOrder}");
                        _sellOrder = null;
                    }

                    if (myPrice.Ask.HasValue && myPrice.AskSize.HasValue)
                    {
                        newSellOrder = new Order(TradeType.sell, myPrice.Ask.GetValueOrDefault(), myPrice.AskSize.GetValueOrDefault());
                        newSellOrder.FillCallback += this.FillCallback;

                        logger.log($"{Name},{newSellOrder}");

                    }
                    else
                        logger.log($"{Name},No Sell Order,myPrice.Ask:{myPrice.Ask},myPrice.AskSize:{myPrice.AskSize}");
                }
            }
            else
            {
                if (myPrice.Ask.HasValue && myPrice.AskSize.HasValue)
                {
                    newSellOrder = new Order(TradeType.sell, myPrice.Ask.GetValueOrDefault(), myPrice.AskSize.GetValueOrDefault());
                    newSellOrder.FillCallback += this.FillCallback;

                    logger.log($"{Name},{newSellOrder}");
                }
                else
                    logger.log($"{Name},No Sell Order,myPrice.Ask:{myPrice.Ask},myPrice.AskSize:{myPrice.AskSize}");
            }

            if (newBuyOrder != null)
                logString += $"Bid {newBuyOrder.price} for {newBuyOrder.size} [{newBuyOrder.Guid}]";

            if (newSellOrder != null)
                logString += $" | Offer {newSellOrder.price} for {newSellOrder.size} [{newSellOrder.Guid}]  ";

            logString += $", Signal: {(double)GetSignalPrice()},PerfectSignal: {signalGenerator.GetPerfectSignal()}";

            logger.log(logString);
            logger.Flush();

            if (newBuyOrder != null)
                if(Book.SubmitOrder(newBuyOrder) == true)
                    _buyOrder = newBuyOrder;

            if (newSellOrder != null)
                if (Book.SubmitOrder(newSellOrder) == true)
                    _sellOrder = newSellOrder;

            lock (_tradesLockObject)
                ticksSinceTrade++;

        }

        private bool IsStarted()
        {
            return MarketMakerState == MarketMakerState.Running;
        }

        public void Run(object nullObject)
        {
            while (Interlocked.Read(ref STOP_THREADS) == 0 && IsStarted())
            {
                RunOnce();
                Thread.Sleep(rndSleep.Next(simParams.MAX_WAIT_PERIOD_MS) + 1);
            }
        }

        public void CalculatePositionUnits(out double buyUnits, out double sellUnits)
        {
            sellUnits = 0;
            buyUnits = 0;
            lock (_tradesLockObject)
            {
                foreach (Trade t in _listOfTrades)
                {
                    if (t.Type == TradeType.buy)
                    {
                        buyUnits += t.amount;
                    }
                    else if (t.Type == TradeType.sell)
                    {
                        sellUnits += t.amount;
                    }
                }
            }
        }

        public void CalculatePosition(out PositionPair longPosition, out PositionPair shortPosition)
        {
            longPosition = new PositionPair() { Type = TradeType.buy };
            shortPosition = new PositionPair() { Type = TradeType.sell };

            lock (_tradesLockObject)
            {
                foreach (Trade t in _listOfTrades)
                {
                    if (t.Type == TradeType.sell)
                    {
                        shortPosition.AveragePrice = (shortPosition.AveragePrice * shortPosition.Amount + (t.amount * t.price)) / (shortPosition.Amount + t.amount);
                        shortPosition.Amount += t.amount;
                    }
                    else if (t.Type == TradeType.buy)
                    {
                        longPosition.AveragePrice = (longPosition.AveragePrice * longPosition.Amount + (t.amount * t.price)) / (longPosition.Amount + t.amount);
                        longPosition.Amount += t.amount;
                    }
                }
            }
        }

        private PricePair GetMyPrice(double signal)
        {
            PricePair price = new PricePair
            {
                Bid = signal - simParams.FIXED_SPREAD,
                Ask = signal + simParams.FIXED_SPREAD,
                BidSize = simParams.MM_ORDER_SIZE,
                AskSize = simParams.MM_ORDER_SIZE
            };
            double buyUnits = 0d;
            double sellUnits = 0d;
            PositionPair longPos = null;
            PositionPair shortPos = null;

            lock (_tradesLockObject)
            {
                CalculatePosition(out longPos, out shortPos);
                buyUnits = longPos.Amount;
                sellUnits = shortPos.Amount;
                
                var ticks = 0;
                lock (_tradesLockObject)
                    ticks = ticksSinceTrade;

                if (_lastTrade != null && ticks == 0 && simParams.BIAS_SPREAD != 0)
                    price = AdjustSpread(_lastTrade.Type, price);
            }

            logger.log($"{Name},{GetPositionString(longPos, shortPos)}");

            if (LIQUIDATION_ENABLED == true)
            {
                if (buyUnits > sellUnits && signal >= longPos.AveragePrice) // only sell a higher quantity if the underlying price is same or better than our price
                {
                    price.AskSize = Math.Max((buyUnits - sellUnits), simParams.MM_ORDER_SIZE);
                    price.Ask = signal;
                    logger.log($"{Name},LiquidationOpp,sell: {price.AskSize} @ {price.Ask},Signal:{signal},buyAvgPx:{Math.Round(longPos.AveragePrice, 2)}");
                    price.AskPriceType = MarketMakerPriceType.liquidation;
                }
                else if (sellUnits > buyUnits && signal <= shortPos.AveragePrice) // only buy a higher quantity if the underlying price is same or better than our price
                {
                    price.BidSize = Math.Max((sellUnits - buyUnits), simParams.MM_ORDER_SIZE);
                    price.Bid = signal;
                    logger.log($"{Name},LiquidationOpp,buy: {price.BidSize} @ {price.Bid},Signal:{signal},sellAvgPx:{Math.Round(shortPos.AveragePrice, 2)}");
                    price.BidPriceType = MarketMakerPriceType.liquidation;
                }
            }

            // cope with overlapping quote prices
            // bias up if position is short,
            // bias down if position is long
            while (price.Bid >= price.Ask)
            {
                if (sellUnits < buyUnits)
                {
                    // position is short
                    logger.log($"{Name},Overlapping prices,Ask:{price.Ask}->{price.Ask + simParams.FIXED_SPREAD}");
                    price.Ask += simParams.FIXED_SPREAD;
                }
                if (buyUnits > sellUnits)
                {
                    // position is long
                    logger.log($"{Name},Overlapping prices,Bid:{price.Bid}->{price.Bid - simParams.FIXED_SPREAD}");
                    price.Bid -= simParams.FIXED_SPREAD;
                }
                else
                {
                    price.Bid -= Math.Ceiling(Math.Abs(simParams.FIXED_SPREAD) / 2);
                    price.Ask += Math.Ceiling(Math.Abs(simParams.FIXED_SPREAD) / 2);
                }
            }

            if (ACTIVE_INVENTORY_CONTROL == true)
            {
                if ((buyUnits - sellUnits) >= MAX_MM_INVENTORY_IMBALANCE)
                {
                    double ticks = Math.Max(1, Math.Floor(MAX_MM_INVENTORY_IMBALANCE / simParams.MM_ORDER_SIZE));
                    double newPrice = price.Bid.Value - (double)ticks * simParams.FIXED_SPREAD;
                    logger.log($"{Name},Overbought inventory,Bid:{price.Bid}->{newPrice}");
                    price.Bid -= ticks * simParams.FIXED_SPREAD;
                }
                else if ((sellUnits - buyUnits) >= MAX_MM_INVENTORY_IMBALANCE)
                {
                    // too short, move our selling price further away
                    double ticks = Math.Max(1, Math.Floor(MAX_MM_INVENTORY_IMBALANCE / simParams.MM_ORDER_SIZE));
                    double newPrice = price.Ask.Value + (double)ticks * simParams.FIXED_SPREAD;
                    logger.log($"{Name},Oversold inventory,Ask:{price.Ask}->{newPrice}");
                    price.Ask = newPrice;
                }
            }

            return price;
        }

        private static string GetPositionString(PositionPair longPos, PositionPair shortPos)
        {
            return $"Position,long:{(double)longPos.Amount} @ {Math.Round(longPos.AveragePrice, 4)},short:{(double)shortPos.Amount} @ {Math.Round(shortPos.AveragePrice, 4)}";
        }

        private PricePair AdjustSpread(TradeType lastTrade, PricePair currentPrice)
        {
            double spread = simParams.BIAS_SPREAD;
            PricePair newPrice = currentPrice;
            logger.log($"{Name},Quote was {currentPrice}");
            if (lastTrade == TradeType.buy)
            {
                double newask = currentPrice.Ask.GetValueOrDefault();
                double newbid = currentPrice.Bid.GetValueOrDefault() - spread;
                newPrice = new PricePair(newbid, currentPrice.BidSize.GetValueOrDefault(), newask, currentPrice.AskSize.GetValueOrDefault());

            }
            else if (lastTrade == TradeType.sell)
            {
                double newask = currentPrice.Ask.GetValueOrDefault() + spread;
                double newbid = currentPrice.Bid.GetValueOrDefault();
                newPrice = new PricePair(newbid, currentPrice.BidSize.GetValueOrDefault(), newask, currentPrice.AskSize.GetValueOrDefault());
            }

            logger.logline($"{Name},Biased Quote,{newPrice}");

            return newPrice;
        }
    }
}
