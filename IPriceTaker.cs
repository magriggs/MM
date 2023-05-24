using System;
namespace MM
{
    interface IPriceTaker
    {
        void Run(object o);
        void Log(Trade t);
    }
}
