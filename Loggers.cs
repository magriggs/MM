using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace MM
{
    public class NullLogger : ILocalLogger
    {

        #region ILocalLogger Members

        public void log(string p)
        {

        }

        public void log(string fmt, params object[] args)
        {

        }

        public void logline(string p)
        {

        }

        public void logline(string fmt, params object[] args)
        {

        }

        public void Flush()
        {

        }

        public void Flush(ILocalLogger logger)
        {
        }

        #endregion
    }

    public class CachingTraceSourceLogger : ILocalLogger
    {
        ConcurrentQueue<string> _logLines = new ConcurrentQueue<string>();
        private ILocalLogger _source;

        public CachingTraceSourceLogger(ILocalLogger source)
        {
            _source = source;
        }

        public void Flush()
        {
            while (_logLines.IsEmpty == false)
            {
                if (_logLines.TryDequeue(out string line))
                    _source.log(line);
            }

            _logLines = new ConcurrentQueue<string>();
        }

        public void log(string p)
        {
            _logLines.Enqueue(p);
        }

        public void log(string fmt, params object[] args)
        {
            _logLines.Enqueue(string.Format(fmt, args));
        }

        public void logline(string p)
        {
            log(p);
        }

        public void logline(string fmt, params object[] args)
        {
            log(fmt, args);
        }
    }

    public class TraceSourceLogger : ILocalLogger
    {
        TraceSource _source = null;
        public TraceSourceLogger(string key = "MM")
        {
            _source = new TraceSource(key);
            _source.Switch = new SourceSwitch("sourceSwitch", "Verbose");
            _source.Listeners.Remove("Default");
        }

        public void AddListener(TraceListener listener)
        {
            _source.Listeners.Add(listener);
        }

        public void Flush()
        {
            _source.Flush();
        }

        public void log(string p)
        {
            _source.TraceInformation(p);
        }

        public void log(string fmt, params object[] args)
        {
            _source.TraceInformation(string.Format(fmt, args));
        }

        public void logline(string p)
        {
            log(p);
        }

        public void logline(string fmt, params object[] args)
        {
            log(fmt, args);
        }
    }

    public class ConsoleLogger : ILocalLogger
    {
        public void log(string fmt, params object[] args)
        {
            Console.Write(fmt, args);
        }

        public void Flush()
        {
        }

        public void log(string p)
        {
            Console.Write(p);
        }

        public void logline(string p)
        {
            Console.WriteLine(p);

        }

        public void logline(string fmt, params object[] args)
        {
            Console.WriteLine(fmt, args);
        }
    }
}