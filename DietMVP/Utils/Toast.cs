using DietMVP.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP.Utils
{
    public static class Toast
    {
        static WeakReference<ContentPage>? _current;

        public static void Register(ContentPage page)
        {
            _current = new WeakReference<ContentPage>(page);
        }

        public static async Task Show(string text, int ms = 2000)
        {
            if (_current is null || !_current.TryGetTarget(out var page)) return;

            var host = page.FindByName<ToastOverlay>("ToastHost");
            if (host is null) return;

            await host.ShowAsync(text, ms);
        }
    }
}
