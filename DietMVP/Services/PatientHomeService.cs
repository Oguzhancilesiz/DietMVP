using DietMVP.Models;
using DietMVP.Utils;
using Microsoft.Maui.Controls;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Services
{
    public class PatientHomeService
    {
        public async Task<TodayVm> LoadTodayAsync(Guid patientId)
        {
            await Supa.InitAsync();

            var today = Clock.TodayTR();

            var progs = await Supa.Client.From<ProgramEntity>()
                .Filter("patient_id", Operator.Equals, patientId.ToString())
                .Filter("start_date", Operator.LessThanOrEqual, today.ToString("yyyy-MM-dd"))
                .Filter("end_date", Operator.GreaterThanOrEqual, today.ToString("yyyy-MM-dd"))
                .Get();

            var prog = progs.Models.FirstOrDefault();
            if (prog == null)
                return new TodayVm { ProgramId = Guid.Empty, ProgramDayId = Guid.Empty, Meals = new(), TargetMl = 0, WaterMl = 0 };

            var dayRes = await Supa.Client.From<ProgramDay>()
                .Filter("program_id", Operator.Equals, prog.Id.ToString())
                .Filter("local_date", Operator.Equals, today.ToString("yyyy-MM-dd"))
                .Get();

            var day = dayRes.Models.FirstOrDefault();
            if (day == null)
                return new TodayVm { ProgramId = prog.Id, ProgramDayId = Guid.Empty, Meals = new(), TargetMl = prog.DailyWaterTargetMl ?? 2000, WaterMl = 0 };

            var mealRes = await Supa.Client.From<Meal>()
                .Filter("program_day_id", Operator.Equals, day.Id.ToString())
                .Order(m => m.StartTime, Ordering.Ascending)
                .Get();

            var vms = new List<MealCardVm>();
            var now = Clock.NowTR().DateTime;

            foreach (var m in mealRes.Models)
            {
                // öğe içerikleri
                var itemsRes = await Supa.Client.From<MealItem>()
                    .Filter("meal_id", Operator.Equals, m.Id.ToString())
                    .Order(i => i.Sort, Ordering.Ascending)
                    .Get();

                // loglar
                var logsRes = await Supa.Client.From<MealLog>()
                    .Filter("patient_id", Operator.Equals, patientId.ToString())
                    .Filter("meal_id", Operator.Equals, m.Id.ToString())
                    .Get();

                var lastLog = logsRes.Models.OrderByDescending(x => x.LoggedAt).FirstOrDefault();

                var isDone = lastLog?.Status?.Equals("Eaten", StringComparison.OrdinalIgnoreCase) == true;
                var isSkipped = lastLog?.Status?.Equals("Skipped", StringComparison.OrdinalIgnoreCase) == true;

                // zamanlar
                var start = today.ToDateTime(m.StartTime);
                var end = today.ToDateTime(m.EndTime);

                // --- relatif metin + rozet & şerit rengi
                string rel;
                string statusText;
                Color badgeColor;
                Color stripeColor;

                if (isDone)
                {
                    var eaten = lastLog!.LoggedAt.ToLocalTime();
                    if (eaten < start)
                    {
                        var t = start - eaten;
                        rel = $"Zamanından önce yendi • {(int)t.TotalHours} sa {t.Minutes} dk önce";
                    }
                    else if (eaten <= end)
                    {
                        rel = "Zamanında yendi 👌";
                    }
                    else
                    {
                        var t = eaten - end;
                        rel = $"{(int)t.TotalHours} sa {t.Minutes} dk geç yendi";
                    }

                    statusText = "Yapıldı";
                    badgeColor = Colors.Green;
                    stripeColor = Color.FromArgb("#10B981");
                }
                else if (isSkipped)
                {
                    rel = "Atlandı";
                    statusText = "Yapılmadı";
                    badgeColor = Colors.Red;
                    stripeColor = Color.FromArgb("#EF4444");
                }
                else
                {
                    if (now < start)
                    {
                        var t = start - now;
                        rel = $"{(int)t.TotalHours} sa {t.Minutes} dk sonra";
                        statusText = "Bekliyor";
                        badgeColor = Color.FromArgb("#F59E0B");
                        stripeColor = Color.FromArgb("#FBBF24");
                    }
                    else if (now <= end)
                    {
                        var t = end - now;
                        rel = $"Şimdi • {t.Minutes} dk kaldı";
                        statusText = "Devam ediyor";
                        badgeColor = Color.FromArgb("#3B82F6");
                        stripeColor = Color.FromArgb("#60A5FA");
                    }
                    else
                    {
                        var t = now - end;
                        rel = $"{(int)t.TotalHours} sa {t.Minutes} dk gecikti";
                        statusText = "Bekliyor";
                        badgeColor = Color.FromArgb("#F59E0B");
                        stripeColor = Color.FromArgb("#F59E0B");
                    }
                }

                // Liste satırı için detay metni
                string ItemToPretty(MealItem i)
                {
                    var parts = new List<string> { i.Name };
                    if (i.Qty.HasValue || !string.IsNullOrWhiteSpace(i.Unit))
                        parts.Add($"{i.Qty?.ToString("0.##")}{(string.IsNullOrWhiteSpace(i.Unit) ? "" : " " + i.Unit)}".Trim());
                    if (i.Kcal.HasValue) parts.Add($"{i.Kcal} kcal");
                    if (!string.IsNullOrWhiteSpace(i.Note)) parts.Add(i.Note!);
                    return string.Join(" — ", parts);
                }

                var itemsText = itemsRes.Models.Count == 0
                    ? new List<string> { "İçerik belirtilmemiş" }
                    : itemsRes.Models.Select(ItemToPretty).ToList();

                // --- YENİ: kart üstündeki kısa özet (ilk 1-2 içerik + fazlaysa +N)
                string subtitle;
                if (itemsRes.Models.Count == 0)
                    subtitle = "İçerik eklenmedi";
                else
                {
                    var names = itemsRes.Models
                        .Select(i => i.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Take(2)
                        .ToList();

                    subtitle = string.Join(" + ", names);
                    var extra = itemsRes.Models.Count - names.Count;
                    if (extra > 0) subtitle += $" +{extra}";
                }

                // --- YENİ: toplam kcal chip
                int totalKcal = itemsRes.Models.Where(i => i.Kcal.HasValue).Sum(i => i.Kcal!.Value);
                string kcalText = totalKcal > 0 ? $"{totalKcal} kcal" : "";

                // --- YENİ: ÖÖ/ÖS saat aralığı
                var timeRange12 = $"{TimeLabel12(m.StartTime)} – {TimeLabel12(m.EndTime)}";

                // foto için cache-buster
                string? url = lastLog?.PhotoUrl;
                if (!string.IsNullOrWhiteSpace(url))
                    url += (url.Contains("?") ? "&" : "?") + "ts=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // slot adı daima başlık olsun
                var slotTitle = SlotToTitle(m.Slot);

                // alt satır: önce özel başlık (m.Title) varsa o, yoksa içerik özeti
                var underTitle = !string.IsNullOrWhiteSpace(m.Title) ? m.Title! : subtitle;

                vms.Add(new MealCardVm
                {
                    MealId = m.Id,
                    Slot = m.Slot,
                    SlotEmoji = SlotEmoji(m.Slot),

                    Title = slotTitle,        // <-- Hep "Kahvaltı / Ara Öğün 1 / Öğle / ..."
                    Subtitle = underTitle,    // <-- Varsa senin başlık, yoksa kısa içerik özeti

                    TimeRange = timeRange12,  // ÖÖ/ÖS’li saatler
                    Items = itemsText,

                    IsDone = isDone,
                    IsSkipped = isSkipped,
                    StatusText = statusText,
                    StatusBadgeColor = badgeColor,
                    StripeColor = stripeColor,

                    Start = m.StartTime,
                    End = m.EndTime,

                    PhotoUrl = url,
                    LastLogText = lastLog != null ? $"Son kayıt: {lastLog.LoggedAt.ToLocalTime():HH:mm}" : "",
                    HasLastLog = lastLog != null,
                    RelativeText = rel,

                    KcalText = kcalText                 // <— YENİ
                });
            }

            var waterRes = await Supa.Client.From<WaterLog>()
                .Filter("program_day_id", Operator.Equals, day.Id.ToString())
                .Filter("patient_id", Operator.Equals, patientId.ToString())
                .Get();

            var waterMl = waterRes.Models.Sum(w => w.Ml);
            var target = prog.DailyWaterTargetMl ?? 2000;

            return new TodayVm
            {
                ProgramId = prog.Id,
                ProgramDayId = day.Id,
                Meals = vms,
                WaterMl = Math.Max(0, waterMl),
                TargetMl = target
            };
        }


        public async Task LogMealAsync(Guid mealId, string status, string? photoUrl)
        {
            await Supa.InitAsync();

            var uidStr = Supa.Client.Auth.CurrentUser?.Id
                         ?? throw new InvalidOperationException("Auth yok (hasta oturumu).");
            if (!Guid.TryParse(uidStr, out var uid))
                throw new InvalidOperationException("Auth UID geçersiz.");

            var log = new MealLog
            {
                Id = Guid.NewGuid(),
                MealId = mealId,
                PatientId = uid,          // <-- SADECE auth.uid()
                Status = status,
                PhotoUrl = photoUrl,
                LoggedAt = DateTime.UtcNow
            };

            await Supa.Client.From<MealLog>().Insert(log);
        }
        public async Task AddWaterAsync(Guid programDayId, Guid patientId, int ml)
        {
            await Supa.InitAsync();
            if (ml <= 0) throw new ArgumentException("ml must be > 0", nameof(ml));

            var w = new WaterLog
            {
                Id = Guid.NewGuid(),
                ProgramDayId = programDayId,
                PatientId = patientId,
                Ml = ml,
                LoggedAt = DateTime.UtcNow
            };
            await Supa.Client.From<WaterLog>().Insert(w);
        }

        // Yardımcılar (metotların üstüne ekleyebilirsin)
        private static string SlotToTitle(string slot) => slot switch
        {
            "Breakfast" => "Kahvaltı",
            "Snack1" => "Ara Öğün 1",
            "Lunch" => "Öğle",
            "Snack2" => "Ara Öğün 2",
            "Dinner" => "Akşam",
            _ => slot
        };

        private static string TimeLabel12(TimeOnly t)
        {
            // 12 saat + ÖÖ/ÖS
            var suffix = t.Hour < 12 ? "ÖÖ" : "ÖS";
            int hh = t.Hour % 12; if (hh == 0) hh = 12;
            return $"{hh:00}:{t.Minute:00} {suffix}";
        }

        private static string SlotEmoji(string slot) => slot switch
        {
            "Breakfast" => "🍳",
            "Snack1" => "🥪",
            "Lunch" => "🍛",
            "Snack2" => "🥗",
            "Dinner" => "🍽️",
            _ => "🍽️"
        };
    }

    // === ViewModels ===
    public class TodayVm
    {
        public Guid ProgramId { get; set; }
        public Guid ProgramDayId { get; set; }
        public List<MealCardVm> Meals { get; set; } = new();
        public int WaterMl { get; set; }
        public int TargetMl { get; set; }
    }

    public class MealCardVm
    {
        public Guid MealId { get; set; }
        public string Slot { get; set; } = "Breakfast";
        public string SlotEmoji { get; set; } = "🍳";
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";          // YENİ: kısa içerik özeti
        public string TimeRange { get; set; } = "";         // ÖÖ/ÖS’li saat aralığı
        public List<string> Items { get; set; } = new();

        public TimeOnly Start { get; set; }
        public TimeOnly End { get; set; }

        public bool IsDone { get; set; }
        public bool IsSkipped { get; set; }
        public string StatusText { get; set; } = "Bekliyor";
        public Color StatusBadgeColor { get; set; } = Color.FromArgb("#F59E0B");

        public string? PhotoUrl { get; set; }
        public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);

        public string RelativeText { get; set; } = "";
        public string LastLogText { get; set; } = "";
        public bool HasLastLog { get; set; }

        public string KcalText { get; set; } = "";          // YENİ
        public bool HasKcal => !string.IsNullOrWhiteSpace(KcalText);

        public Color StripeColor { get; set; } = Color.FromArgb("#E5E7EB");
        public bool ShowAction => !IsDone && !IsSkipped;
    }

}
