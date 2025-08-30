using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Utils
{
    public static class Clock
    {
        // Önce IANA / Windows TZ dener, olmazsa sabit +03:00'a düşer
        public static DateTimeOffset NowTR()
        {
            try
            {
#if ANDROID || IOS || MACCATALYST || LINUX
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
#else
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
#endif
                return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            }
            catch
            {
                return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3));
            }
        }

        public static DateOnly TodayTR() => DateOnly.FromDateTime(NowTR().Date);
    }
}
