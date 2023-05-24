using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MM
{
    public class SimulationParameters
    {
        public double MM_ORDER_SIZE = 100;
        public int MAX_WAIT_PERIOD_MS = 2;
        public int MAX_ITERATIONS = 200;
        public int MAX_PERFECT_TAKERS = 0;
        public int MAX_NOISY_TAKERS = 0;
        public int MAX_RANDOM_TAKERS = 0;
        public int NUM_LIQUIDITY_PROVIDERS = 0; // additional liquidity providers/market makers alongside the designated market maker
        public double FIXED_SPREAD = 2;
        public double BIAS_SPREAD = 2;
        public int MAX_PRICE_TAKERS { get { return MAX_PERFECT_TAKERS + MAX_NOISY_TAKERS + MAX_RANDOM_TAKERS; } }

        public double MARKET_VOLATILITY = 0.15; // 15% volatility

        public double SIGNAL_NOISE_MAGNITUDE = 5;
        public bool MULTITHREAD_MARKET_MAKER = true;

        public MarketMakerSignalType MARKET_MAKER_SIGNAL_TYPE = MarketMakerSignalType.noisy;

        public SimulationParameters()
        {

        }

        public SimulationParameters(SimulationParameters other)
        {
            this.MM_ORDER_SIZE = other.MM_ORDER_SIZE;
            this.MAX_WAIT_PERIOD_MS = other.MAX_WAIT_PERIOD_MS;
            this.MAX_ITERATIONS = other.MAX_ITERATIONS;
            this.MAX_PERFECT_TAKERS = other.MAX_PRICE_TAKERS;
            this.MAX_NOISY_TAKERS = other.MAX_NOISY_TAKERS;
            this.MAX_RANDOM_TAKERS = other.MAX_RANDOM_TAKERS;
            this.NUM_LIQUIDITY_PROVIDERS = other.NUM_LIQUIDITY_PROVIDERS;
            this.FIXED_SPREAD = other.FIXED_SPREAD; 
            this.BIAS_SPREAD = other.BIAS_SPREAD;
            this.MARKET_VOLATILITY = other.MARKET_VOLATILITY;
            this.SIGNAL_NOISE_MAGNITUDE = other.SIGNAL_NOISE_MAGNITUDE;
            this.MULTITHREAD_MARKET_MAKER = other.MULTITHREAD_MARKET_MAKER;
            this.MARKET_MAKER_SIGNAL_TYPE = other.MARKET_MAKER_SIGNAL_TYPE;
        }

        public override string ToString()
        {
            var result = $"MAX_WAIT_PERIOD_MS:{MAX_WAIT_PERIOD_MS}" + Environment.NewLine;
            result += $"MAX_ITERATIONS:{MAX_ITERATIONS}" + Environment.NewLine;
            result += $"MAX_PERFECT_TAKERS:{MAX_PERFECT_TAKERS}" + Environment.NewLine;
            result += $"MAX_NOISY_TAKERS:{MAX_NOISY_TAKERS}" + Environment.NewLine;
            result += $"MAX_RANDOM_TAKERS:{MAX_RANDOM_TAKERS}" + Environment.NewLine;
            result += $"FIXED_SPREAD:{FIXED_SPREAD}" + Environment.NewLine;
            result += $"BIAS_SPREAD:{BIAS_SPREAD}" + Environment.NewLine;
            result += $"MAX_PRICE_TAKERS:{MAX_PRICE_TAKERS}" + Environment.NewLine;
            result += $"SIGNAL_NOISE_MAGNITUDE:{SIGNAL_NOISE_MAGNITUDE}" + Environment.NewLine;
            result += $"MULTITHREAD_MARKET_MAKER:{MULTITHREAD_MARKET_MAKER}" + Environment.NewLine;
            result += $"NUM_LIQUIDITY_PROVIDERS:{NUM_LIQUIDITY_PROVIDERS}" + Environment.NewLine;
            return result;
        }
    }

    public class Simulation
    {
        private Thread priceMakerThread = null;
        private Thread signalGeneratorThread = null;

        internal ILocalLogger debugLogger;
        private SimulationParameters simParams;
        private ILocalLogger resultsLogger;
        private readonly long fileNumber;
        private Thread designatedMarketMakerThread;

        public double profit { get; private set; }

        public Simulation(SimulationParameters simParams)
        {
            this.simParams = simParams;
        }

        public Simulation(SimulationParameters simParams, ILocalLogger debugLogger, ILocalLogger resultsLogger, long fileNumber) : this(simParams)
        {
            this.debugLogger = debugLogger;
            this.resultsLogger = resultsLogger;
            this.fileNumber = fileNumber;
        }

        public double RunSimulation()
        {
            SignalGenerator sigGen = new SignalGenerator(100, simParams.MAX_WAIT_PERIOD_MS, simParams.MARKET_VOLATILITY);
            sigGen.logger = debugLogger;

            MatchingEngine engine = new MatchingEngine(debugLogger);
            engine.Start();

            var designatedMMSimParams = new SimulationParameters(simParams) { MM_ORDER_SIZE = 10 };
            var designatedMarketMaker = new MarketMaker(sigGen, debugLogger, designatedMMSimParams, engine, "MM0");
            designatedMarketMaker.Start();
            if (simParams.MULTITHREAD_MARKET_MAKER == true)
            {
                designatedMarketMakerThread = new Thread(new ParameterizedThreadStart(designatedMarketMaker.Run));
                designatedMarketMakerThread.Start();
            }

            List<MarketMaker> liquidityProviders = new List<MarketMaker>();
            List<Thread> liquidityProviderThreads = new List<Thread>();

            for(int i = 0; i < simParams.NUM_LIQUIDITY_PROVIDERS; i++)
            {
                var lpSimParams = new SimulationParameters(simParams) { FIXED_SPREAD = 2.5 };
                var LP = new MarketMaker(sigGen, debugLogger, lpSimParams , engine, "MM"+(i+1))
                {
                    logger = debugLogger,
                    Book = engine
                };
                liquidityProviders.Add(LP);
                LP.Start();
                
                if (simParams.MULTITHREAD_MARKET_MAKER == true)
                {
                    var th = new Thread(new ParameterizedThreadStart(LP.Run));
                    th.IsBackground = true;
                    th.Start();
                    liquidityProviderThreads.Add(th);
                }
            }

            var priceTakers = new List<AbstractPriceTaker>();

            for (int i = 0; i < simParams.MAX_PERFECT_TAKERS; i++)
                priceTakers.Add(new PerfectInformedPriceTaker(engine, "PIPT"+i) { logger = debugLogger });

            for (int i = 0; i < simParams.MAX_NOISY_TAKERS; i++)
                priceTakers.Add(new NoisyInformedPriceTaker(engine, "NIPT"+i) { logger = debugLogger });
            
            for (int i = 0; i < simParams.MAX_RANDOM_TAKERS; i++)
                priceTakers.Add(new RandomPriceTaker(engine, "RPT"+i){ logger = debugLogger });

            signalGeneratorThread = new Thread(new ParameterizedThreadStart(sigGen.Run)) { IsBackground = true };
            signalGeneratorThread.Start();

            Random rndSelectPriceTaker = new Random();
            Random rndSleep = new Random();

            // wait for designated market maker to start publishing prices
            while(true)
            {
                var bbo = engine.GetBestBidOffer();
                if (bbo.BidSize > 0 || bbo.Ask > 0)
                    break;
            }

            for (int iteration = 0; iteration < simParams.MAX_ITERATIONS; iteration++)
            {
                if (simParams.MULTITHREAD_MARKET_MAKER == false)
                    designatedMarketMaker.RunOnce();

                RunLiquidityProviders(liquidityProviders, rndSelectPriceTaker);

                RunPriceTakers(sigGen, priceTakers, rndSelectPriceTaker);

                Thread.Sleep(rndSleep.Next(simParams.MAX_WAIT_PERIOD_MS) + 1); // always sleep at least 1ms
            }

            debugLogger.log("Simulation complete");
            debugLogger.Flush();

            foreach(var lp in liquidityProviders)
                lp.Stop();

            engine.Stop();
            sigGen.Stop();
            designatedMarketMaker.Stop();

            signalGeneratorThread?.Join();
            priceMakerThread?.Join();
            designatedMarketMakerThread?.Join();
            
            double lastPrice = sigGen.Signal;

            // switch to capturing the output
            lock (resultsLogger)
            {
                debugLogger = resultsLogger;

                debugLogger.logline($"************* RESULTS File Number: {fileNumber} *******************");
                debugLogger.logline("");

                designatedMarketMaker.CalculatePosition(out PositionPair longPosition, out PositionPair shortPosition);
                LogMarketMakerPnl(designatedMarketMaker, lastPrice, longPosition, shortPosition);

                foreach(var lp in liquidityProviders)
                {
                    debugLogger.logline($"===== {lp.Name} results =====");
                    lp.CalculatePosition(out longPosition, out shortPosition);
                    LogMarketMakerPnl(lp, lastPrice, longPosition, shortPosition);
                }

                var ptResults = GetPriceTakerResults(priceTakers, lastPrice, profit, out double totalTakerPnl);

                foreach (var s in ptResults)
                    debugLogger.logline($"{s}");

                debugLogger.logline("*****************************************");
                debugLogger.Flush();

                if (Math.Abs(profit) - Math.Abs(totalTakerPnl) > 0.1)
                    debugLogger.log($"*** ERROR,Mismatch Pnl,MM:{profit},Takers:{totalTakerPnl}");

            }
            return profit;
        }

        private void LogMarketMakerPnl(MarketMaker maker, double lastPrice, PositionPair longPosition, PositionPair shortPosition)
        {
            double netStockUnitPosition = longPosition.Amount - shortPosition.Amount;
            debugLogger.logline($"{maker.Name},Position,Buy: {longPosition.Amount} @ {Math.Round(longPosition.AveragePrice, 4)},Sell: {shortPosition.Amount} @ {Math.Round(shortPosition.AveragePrice, 4)}");
            debugLogger.logline($"{maker.Name},Last Px,{Math.Round(lastPrice, 2)}");
            double buyPay = Math.Round(longPosition.AveragePrice * longPosition.Amount, 2); //  what we paid for buying
            profit -= buyPay;

            double saleReceive = Math.Round(shortPosition.AveragePrice * shortPosition.Amount, 2);    //  what we received for selling
            profit += saleReceive;

            profit = Math.Round(profit, 2);

            debugLogger.log($"{maker.Name},Trading profit,${saleReceive} - ${buyPay} = ${profit}");

            if (netStockUnitPosition < 0)
            {
                // we sold more units than we bought, need to buy back at last price
                double amountToPayForBuyingBack = netStockUnitPosition * lastPrice;
                debugLogger.logline($"{maker.Name},Paid ${amountToPayForBuyingBack} = {netStockUnitPosition}*${lastPrice} on shortfall");
                profit -= Math.Abs(amountToPayForBuyingBack);
            }
            else if (netStockUnitPosition > 0)
            {
                //  we need to sell our excess - so we receive money
                double amountToReceiveForSellingBack = netStockUnitPosition * lastPrice;
                debugLogger.logline($"{maker.Name},Received (${amountToReceiveForSellingBack} = {netStockUnitPosition})*{lastPrice} on surplus");
                profit += Math.Abs(amountToReceiveForSellingBack);
            }
            else
            {
                debugLogger.log("{maker.Name},Flat position, nothing to buy or sell at lastPrice");
            }

            debugLogger.logline($"Profit,{maker.Name},${Math.Round(profit, 2)}");
        }

        private void RunPriceTakers(SignalGenerator sigGen, List<AbstractPriceTaker> priceTakers, Random rndSelectPriceTaker)
        {
            HashSet<int> permutation = new HashSet<int>();
            while (permutation.Count < simParams.MAX_PRICE_TAKERS)
            {
                permutation.Add(rndSelectPriceTaker.Next(simParams.MAX_PRICE_TAKERS));
            }

            foreach (var pos in permutation)
            {
                AbstractPriceTaker priceTaker = priceTakers.ElementAtOrDefault(pos);

                if (priceTaker is PerfectInformedPriceTaker)
                    priceTaker.Run(sigGen.GetPerfectSignal());
                else if (priceTaker is NoisyInformedPriceTaker)
                    priceTaker.Run(sigGen.GetNoisySignal(simParams.SIGNAL_NOISE_MAGNITUDE));
                else if (priceTaker is RandomPriceTaker)
                    priceTaker.Run(null);
            }

            permutation.Clear();
        }

        private void RunLiquidityProviders(List<MarketMaker> marketMakers, Random rndSelectPriceTaker)
        {
            HashSet<int> permutation = new HashSet<int>();
            if (simParams.MULTITHREAD_MARKET_MAKER == false)
            {
                while (permutation.Count < simParams.MAX_PRICE_TAKERS)
                {
                    permutation.Add(rndSelectPriceTaker.Next(simParams.MAX_PRICE_TAKERS));
                }
                foreach (var index in permutation)
                {
                    marketMakers[index].RunOnce();
                }

                permutation.Clear();
            }
        }

        private List<String> GetPriceTakerResults(List<AbstractPriceTaker> priceTakers, double lastPrice, double makerPnl, out double totalTakerPnl)
        {
            var results = new List<string>();
            totalTakerPnl = 0d;
            foreach (var pt in priceTakers)
            {
                double realisedPnl = pt.CalculatePnl(lastPrice, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);

                double totalPnl = unrealisedPnl + realisedPnl;
                totalTakerPnl += totalPnl;
                pt.CalculatePnlValues(out double buyValue, out double sellValue, out double buyUnits, out double sellUnits);

                results.Add($"{pt.Name},PnL,Total:${Math.Round(totalPnl, 2)},Realised:${Math.Round(realisedPnl, 2)},Unrealised:${Math.Round(unrealisedPnl, 2)}, Trades: {pt.NumberOfTrades}, NoTrades: {pt.NumberOfNoTrades}, Traded: {Math.Round(100d * ((double)pt.NumberOfTrades / (double)(pt.NumberOfNoTrades + pt.NumberOfTrades)), 2)}%, BuyValue:${Math.Round(buyValue,2)}, SellValue:${Math.Round((double)sellValue,2)}, BuyUnits:{buyUnits}, SellUnits:{sellUnits}, UrAvgPx:{Math.Round(unrealizedAvgPx, 4)}");
            }   

            if (Math.Abs(makerPnl) - Math.Abs(totalTakerPnl) > 0.1)
            {
                debugLogger.log("**** ERROR Pnl Mismatch");
            }

            return results;
        }

    }
}
