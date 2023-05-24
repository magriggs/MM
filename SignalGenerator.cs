using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MM
{
    public class SignalGenerator
    {
        public ILocalLogger logger { get; set; }

        public SignalGenerator(double initialPrice, int maxWaitPeriodMs, double volatility)
        {
            _signal = initialPrice;
            _maxWaitPeriodMs = maxWaitPeriodMs;
            _stdDevVol = Math.Sqrt(volatility);
            sleepRandom = new Random();
        }

        private object _signalLock = new object();
        private double _signal;
        private readonly int _maxWaitPeriodMs;
        private Random rndNormDist = new Random();
        private bool STOP_THREADS;
        private double _stdDevVol;
        private Random sleepRandom;

        public double Signal
        {
            get
            {
                lock (_signalLock) { return _signal; }
            }
            set
            {
                lock (_signalLock) { _signal = value; }
            }
        }

        public double GetPerfectSignal()
        {
            return Signal;
        }

        public double GetNoisySignal(double noiseMagnitude)
        {
            double s;
            do
            {
                s = Signal + noiseMagnitude * Math.Round(rndNormDist.NextGaussian(), 2);
            } while (s <= 0);

            logger.log($"Signal,{s},Perfect Signal:{Signal}");
            return s;
        }

        public void Stop()
        {
            STOP_THREADS = true;
        }

        public void Run(object o)
        {
            while (this.STOP_THREADS == false)
            {
                while (true)
                {
                    var value = Math.Round(rndNormDist.NextGaussian(_signal, _stdDevVol), 1);
                    if (value > 0)
                    {
                        Signal = value;
                        break;
                    }
                }
                
                Thread.Sleep(sleepRandom.Next(_maxWaitPeriodMs)+1);
            }
        }
    }
}
