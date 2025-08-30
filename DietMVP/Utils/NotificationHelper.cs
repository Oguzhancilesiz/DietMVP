using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.LocalNotification;

namespace DietMVP.Utils
{
    public static class NotificationHelper
    {
        public static Task ShowNowAsync(int id, string title, string body)
        {
            // Schedule vermeden Show => hemen gösterir (platform izinlerine tabi)
            return LocalNotificationCenter.Current.Show(new NotificationRequest
            {
                NotificationId = id,
                Title = title,
                Description = body
            });
        }

        public static int IdFromGuid(Guid g) => unchecked(g.GetHashCode() & 0x7FFFFFFF);

    }
}
