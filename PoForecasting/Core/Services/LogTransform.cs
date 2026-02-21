using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services
{
    internal static class LogTransform
    {
        public static decimal ToLog(decimal value, decimal eps)
        {
            // clamp to >= 0 before log
            if (value < 0) value = 0;
            return (decimal)Math.Log((double)(value + eps));
        }

        public static decimal FromLog(decimal logValue, decimal eps)
        {
            var v = (decimal)Math.Exp((double)logValue) - eps;
            return v < 0 ? 0 : v; // keep safe
        }
    }
}
