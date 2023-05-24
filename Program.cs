using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using NUnit.Framework.Internal;

namespace MM
{
    class Program
    {
        private static readonly int MAX_SIMULATIONS = 10;
        private static readonly int MAX_CONCURRENT_SIMULATIONS = 128;
        private ConcurrentBag<double> results = new ConcurrentBag<double>();

        static void Main(string[] args)
        {
            TraceSourceLogger tsl = null;
            CachingTraceSourceLogger cachingLogger = null;
            var consoleLogger = new ConsoleLogger();

            try
            {
                List<Thread> threads = new List<Thread>();
                tsl = new TraceSourceLogger("MM");
                var textListener = new TextWriterTraceListener
                {
                    Writer = new StreamWriter("C:\\temp\\MM.log", false),
                    Filter = new EventTypeFilter(SourceLevels.All),
                    TraceOutputOptions = TraceOptions.None

                };
                tsl.AddListener(textListener);

                cachingLogger = new CachingTraceSourceLogger(tsl);
                tsl.log($"Start time: {DateTime.Now}");
                Program p = new Program();
                var simParams = new SimulationParameters() { MM_ORDER_SIZE=100, NUM_LIQUIDITY_PROVIDERS = 1, MARKET_VOLATILITY=0.20, MULTITHREAD_MARKET_MAKER=true, SIGNAL_NOISE_MAGNITUDE=3, FIXED_SPREAD=3, BIAS_SPREAD=0, MAX_ITERATIONS = 500, MARKET_MAKER_SIGNAL_TYPE = MarketMakerSignalType.noisy, MAX_NOISY_TAKERS = 100, MAX_PERFECT_TAKERS = 0, MAX_RANDOM_TAKERS = 0 };
                long count = 0;
                long threadCount = 0;

                for (int i = 0; i < MAX_SIMULATIONS; i++)
                {
                    while (true)
                    {
                        long l = Interlocked.Read(ref threadCount);
                        if (l < MAX_CONCURRENT_SIMULATIONS)
                            break;

                        consoleLogger.logline($"Running {l} simulations, waiting for slot");
                        Thread.Sleep(500);                        
                    }

                    var thread = new Thread(unused =>
                    {
                        var fileNumber = Interlocked.Increment(ref count);
                        var debugLogger = new TraceSourceLogger("MMDebug");
                        TextWriterTraceListener debugListener = new TextWriterTraceListener
                        {
                            Writer = new StreamWriter($"c:\\temp\\MMDebug_{fileNumber}.log"),
                            Filter = new EventTypeFilter(SourceLevels.All),
                        };
                        debugLogger.AddListener(debugListener);
                        consoleLogger.logline($"Running thread {fileNumber}");
                        p.Run(simParams, cachingLogger, debugLogger, fileNumber);
                        Interlocked.Decrement(ref threadCount);
                    });

                    thread.Start();
                    Interlocked.Increment(ref threadCount);
                    threads.Add(thread);
                }
                
                while (threads.Any(x => x.ThreadState != System.Threading.ThreadState.Stopped))
                {
                    consoleLogger.logline($"Waiting on {threads.Where(x => x.ThreadState != System.Threading.ThreadState.Stopped).Count()} threads to complete");
                    Thread.Sleep(1000);
                }

                cachingLogger.Flush();
                cachingLogger.log("SimParams");
                cachingLogger.log(simParams.ToString());
                cachingLogger.log("");
                cachingLogger.Flush();
                p.LogResults(cachingLogger, p.results);
            }
            finally
            {
                tsl.log($"End time: {DateTime.Now}");
                tsl.Flush();
                cachingLogger.Flush(); 
            }
        }

        // this method gets called by Thread.Start
        public void Run(SimulationParameters simParams, ILocalLogger logger, ILocalLogger debugLogger, long fileNumber)
        {
            var fileLogger = logger;
            var sim = new Simulation(simParams, debugLogger, fileLogger, fileNumber);
            var result = sim.RunSimulation();
            results.Add(result);
        }

        public void LogResults(ILocalLogger logger, IReadOnlyCollection<double> results)
        {
            var profit = Math.Round(results.Sum(x => x),2);
            logger.log($"Total Profit,${profit}");

            var val = results.Count(x => x > 0);

            int wins = results.Count(x => x > 0);
            int losses = results.Count(x => x < 0);
            logger.log($"Wins,{wins}");
            logger.log($"Losses,{losses}");
            logger.log($"Win/Loss Ratio {Math.Round((double)wins/(double)(wins+losses),2)*100}%");
            logger.log($"Average Win,{Math.Round(results.Where(x => x > 0).DefaultIfEmpty().Average(),2)}");
            logger.log($"Average Loss,{Math.Round(results.Where(x => x < 0).DefaultIfEmpty().Average(),2)}");
            
            logger.Flush();
        }
    }
}
