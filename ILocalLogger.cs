using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MM
{
    public interface ILocalLogger
    {
        void log(string p);
        void log(string fmt, params object[] args);
        void logline(string p);
        void logline(string fmt, params object[] args);

        void Flush();
    }
}
