using Plugin.LocalNotification;

namespace DietMVP.Services;

public static class NotificationService
{
    // Benzersiz ID aralığı için sabitler
    private const int WaterBase = 800000;

    public static void Init()
    {
        try { _ = LocalNotificationCenter.Current.RequestNotificationPermission(); } catch { }

        // Tek parametreli imza
        LocalNotificationCenter.Current.NotificationActionTapped += (e) =>
        {
            // İsterseniz e.Request.NotificationId, e.IsDismissed vb. kullanın
        };
    }

    public static void CancelAllPending()
    {
        try { LocalNotificationCenter.Current.CancelAll(); } catch { }
    }

    // --- Öğün bildirimleri: -30dk, başlangıç, bitiş
    public static async Task ScheduleMealsForTodayAsync(List<DietMVP.Services.MealCardVm> meals, Guid patientId)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        foreach (var m in meals)
        {
            var start = today.ToDateTime(m.Start);
            var end = today.ToDateTime(m.End);

            var pre = start.AddMinutes(-30);
            if (pre > DateTime.Now)
                await ShowOnceAsync(CombineToInt(patientId, m.MealId, 1),
                    "Öğün Yaklaşıyor", $"{m.Title} için 30 dk kaldı ({m.Start:hh\\:mm})", pre);

            if (start > DateTime.Now)
                await ShowOnceAsync(CombineToInt(patientId, m.MealId, 2),
                    "Öğün Zamanı", $"{m.Title} başladı • {m.Start:hh\\:mm}-{m.End:hh\\:mm}", start);

            if (end > DateTime.Now)
                await ShowOnceAsync(CombineToInt(patientId, m.MealId, 3),
                    "Öğün Süresi Doldu", $"{m.Title} için süre doldu ({m.End:hh\\:mm})", end);
        }
    }

    // --- Su hatırlatma: 09:00–21:00 arası her X dakikada bir (max 16 plan)
    public static async Task ScheduleWaterAsync(TimeOnly dayStart, TimeOnly dayEnd, TimeSpan interval)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var first = today.ToDateTime(dayStart);
        var last = today.ToDateTime(dayEnd);
        if (last <= DateTime.Now) return;

        var t = first;
        while (t < DateTime.Now) t = t.Add(interval);

        int i = 0;
        while (t <= last && i < 16)
        {
            await ShowOnceAsync(WaterBase + i,
                "Su Zamanı 💧", "Bir bardak su içmeyi unutma.", t);
            t = t.Add(interval);
            i++;
        }
    }

    // Gece 00:00 civarı yeniden planlamak için
    public static void StartMidnightRescheduler(Func<Task> rescheduleAction)
    {
        Device.StartTimer(TimeSpan.FromMinutes(1), () =>
        {
            var now = DateTime.Now;
            if (now.Hour == 0 && now.Minute <= 1)
            {
                MainThread.BeginInvokeOnMainThread(async () => await rescheduleAction());
                return false; // bir kez çalıştır
            }
            return true;
        });
    }

    // ---- helpers ----
    private static async Task ShowOnceAsync(int id, string title, string body, DateTime when)
    {
        try { LocalNotificationCenter.Current.Cancel(id); } catch { }
        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = id,
            Title = title,
            Description = body,
            ReturningData = "payload",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = when,
                RepeatType = NotificationRepeat.No
            }
        });
    }

    private static int CombineToInt(Guid a, Guid b, int suffix)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + a.GetHashCode();
            hash = hash * 31 + b.GetHashCode();
            hash = hash * 31 + suffix;
            return hash < 0 ? -hash : hash;
        }
    }
}
