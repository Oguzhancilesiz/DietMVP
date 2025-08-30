using System.Linq;
using DietMVP.Models;
using Microsoft.Maui.Graphics;
using static Supabase.Postgrest.Constants; // Operator

namespace DietMVP.Services
{
    // ==== VMs ====

    public class ReportSummaryVM
    {
        public int Days { get; set; }
        public int PlannedMeals { get; set; }
        public int DoneMeals { get; set; }
        public int SkippedMeals { get; set; }
        public int AdherencePct { get; set; }                  // 0..100
        public decimal TotalWaterLiters { get; set; }
        public decimal AvgWaterLitersPerDay { get; set; }
        public string RangeText { get; set; } = "";
    }

    public class ReportDayVM
    {
        public Guid DayId { get; set; }
        public DateOnly Date { get; set; }
        public string DateText => Date.ToString("dd.MM.yyyy");

        public int Planned { get; set; }
        public int Done { get; set; }
        public int Skipped { get; set; }

        public decimal WaterLiters { get; set; }
        public string WaterLitersText => $"{WaterLiters:0.##} L";

        public double Adherence => Planned == 0 ? 0 : (double)Done / Planned;   // 0..1
        public string AdherenceLabel => $"{(int)Math.Round(Adherence * 100)}%";

        public string StatusText =>
            Planned == 0 ? "plan yok" :
            Done == Planned ? "tamamlandı" :
            Done == 0 ? "hiç yapılmadı" :
            $"{Planned - Done} eksik";

        public Color BadgeColor =>
            Adherence >= 0.8 ? Color.FromArgb("#10B981") :
            Adherence >= 0.5 ? Color.FromArgb("#F59E0B") :
                               Color.FromArgb("#EF4444");
    }

    public class PatientReportVM
    {
        public ReportSummaryVM Summary { get; set; } = new();
        public List<ReportDayVM> Days { get; set; } = new();
    }

    public class ReportDetailVM
    {
        public ReportDetailSummaryVM Summary { get; set; } = new();
        public List<ReportDetailDayVM> Days { get; set; } = new();
    }

    public class ReportDetailSummaryVM
    {
        public int Days { get; set; }
        public int PlannedMeals { get; set; }
        public int DoneMeals { get; set; }
        public int SkippedMeals { get; set; }
        public int AdherencePct { get; set; }
        public decimal TotalWaterLiters { get; set; }
        public decimal AvgWaterLitersPerDay { get; set; }
        public string RangeText { get; set; } = "";
    }

    public class ReportDetailDayVM
    {
        public DateOnly Date { get; set; }
        public int Planned { get; set; }
        public int Done { get; set; }
        public int Skipped { get; set; }
        public double Adherence { get; set; }
        public decimal TotalWaterLiters { get; set; }
        public List<ReportDetailMealVM> Meals { get; set; } = new();

        public string DateText => $"{Date:dd MMMM yyyy}";
        public string WaterLitersText => $"{TotalWaterLiters:0.##} L";
        public string StatusText => Adherence >= 0.8 ? "iyi uyum" : Adherence >= 0.5 ? "orta" : "zayıf";
        public string AdherenceLabel => $"{(int)Math.Round(Adherence * 100)}%";
        public Color BadgeColor => Adherence >= 0.8 ? Color.FromArgb("#16A34A")
                                   : Adherence >= 0.5 ? Color.FromArgb("#F59E0B")
                                   : Color.FromArgb("#DC2626");
    }

    public class ReportDetailMealVM
    {
        public string MealName { get; set; } = "";
        public bool IsDone { get; set; }
        public DateTime? CompletedAtLocal { get; set; }
        public string? Note { get; set; }
        public List<string> Photos { get; set; } = new();

        public string StatusText => IsDone ? "Yapıldı" : "Yapılmadı";
        public string CompletedAtText =>
            IsDone && CompletedAtLocal.HasValue
                ? CompletedAtLocal.Value.ToString("dd.MM.yyyy HH:mm", new System.Globalization.CultureInfo("tr-TR"))
                : "—";

        public bool HasNote => !string.IsNullOrWhiteSpace(Note);
        public bool HasPhotos => Photos is { Count: > 0 };
        public Color StatusBadgeColor => IsDone ? Color.FromArgb("#16A34A") : Color.FromArgb("#DC2626");
    }

    // ==== Service ====

    public class ReportService
    {
        private static List<string> ToStringList(IEnumerable<Guid> ids) => ids.Select(x => x.ToString()).ToList();

        public async Task<PatientReportVM> BuildAsync(Guid patientId)
        {
            await Supa.InitAsync();

            // 1) programs: patient_id
            var progsResp = await Supa.Client.From<ProgramEntity>()
                .Filter("patient_id", Operator.Equals, patientId.ToString())
                .Get();

            var progs = progsResp.Models.OrderBy(p => p.StartDate).ToList();

            if (!progs.Any())
            {
                return new PatientReportVM
                {
                    Summary = new ReportSummaryVM { Days = 0, RangeText = "Program yok" },
                    Days = new()
                };
            }

            // 2) program_days: program_id
            var progIds = progs.Select(p => p.Id).ToList();
            var days = new List<ProgramDay>();
            if (progIds.Any())
            {
                var daysResp = await Supa.Client.From<ProgramDay>()
                    .Filter("program_id", Operator.In, ToStringList(progIds))
                    .Get();
                days = daysResp.Models.OrderBy(d => d.LocalDate).ToList();
            }

            if (!days.Any())
            {
                var first = progs.First(); var last = progs.Last();
                return new PatientReportVM
                {
                    Summary = new ReportSummaryVM
                    {
                        Days = 0,
                        PlannedMeals = 0,
                        DoneMeals = 0,
                        SkippedMeals = 0,
                        AdherencePct = 0,
                        TotalWaterLiters = 0,
                        AvgWaterLitersPerDay = 0,
                        RangeText = $"{first.StartDate:dd.MM.yyyy} → {last.EndDate:dd.MM.yyyy}"
                    },
                    Days = new()
                };
            }

            // 3) meals: program_day_id
            var dayIds = days.Select(d => d.Id).ToList();
            var meals = new List<Meal>();
            if (dayIds.Any())
            {
                var mealsResp = await Supa.Client.From<Meal>()
                    .Filter("program_day_id", Operator.In, ToStringList(dayIds))
                    .Get();
                meals = mealsResp.Models.ToList();
            }

            // 4) meal_logs: patient_id + meal_id
            var mealLogs = new List<MealLog>();
            var mealIds = meals.Select(m => m.Id).ToList();
            if (mealIds.Any())
            {
                var mealLogsResp = await Supa.Client.From<MealLog>()
                    .Filter("patient_id", Operator.Equals, patientId.ToString())
                    .Filter("meal_id", Operator.In, ToStringList(mealIds))
                    .Get();
                mealLogs = mealLogsResp.Models.ToList();
            }

            // 5) water_logs: patient_id + program_day_id
            var waterLogs = new List<WaterLog>();
            if (dayIds.Any())
            {
                var waterLogsResp = await Supa.Client.From<WaterLog>()
                    .Filter("patient_id", Operator.Equals, patientId.ToString())
                    .Filter("program_day_id", Operator.In, ToStringList(dayIds))
                    .Get();
                waterLogs = waterLogsResp.Models.ToList();
            }

            // 6) Günlük derleme
            var byDay = days.ToDictionary(d => d.Id, d => new ReportDayVM
            {
                DayId = d.Id,
                Date = d.LocalDate
            });

            foreach (var m in meals)
                if (byDay.TryGetValue(m.ProgramDayId, out var dvm))
                    dvm.Planned++;

            foreach (var log in mealLogs)
            {
                var meal = meals.FirstOrDefault(x => x.Id == log.MealId);
                if (meal == null) continue;
                if (!byDay.TryGetValue(meal.ProgramDayId, out var dvm)) continue;

                var eaten = string.Equals(log.Status, "Eaten", StringComparison.OrdinalIgnoreCase);
                var skipped = string.Equals(log.Status, "Skipped", StringComparison.OrdinalIgnoreCase);
                if (eaten) dvm.Done++;
                else if (skipped) dvm.Skipped++;
            }

            foreach (var w in waterLogs)
                if (byDay.TryGetValue(w.ProgramDayId, out var dvm))
                    dvm.WaterLiters += (decimal)w.Ml / 1000m;

            var dayList = byDay.Values.OrderByDescending(v => v.Date).ToList();

            // 7) Özet
            var totalPlanned = meals.Count;
            var totalDone = mealLogs.Count(l => string.Equals(l.Status, "Eaten", StringComparison.OrdinalIgnoreCase));
            var totalSkipped = mealLogs.Count(l => string.Equals(l.Status, "Skipped", StringComparison.OrdinalIgnoreCase));
            var totalWater = dayList.Sum(x => x.WaterLiters);
            var dayCount = dayList.Count;
            var adhPct = totalPlanned == 0 ? 0 : (int)Math.Round(100.0 * totalDone / totalPlanned);

            var firstProg = progs.First();
            var lastProg = progs.Last();

            return new PatientReportVM
            {
                Summary = new ReportSummaryVM
                {
                    Days = dayCount,
                    PlannedMeals = totalPlanned,
                    DoneMeals = totalDone,
                    SkippedMeals = totalSkipped,
                    AdherencePct = adhPct,
                    TotalWaterLiters = totalWater,
                    AvgWaterLitersPerDay = dayCount == 0 ? 0 : totalWater / dayCount,
                    RangeText = $"{firstProg.StartDate:dd.MM.yyyy} → {lastProg.EndDate:dd.MM.yyyy}"
                },
                Days = dayList
            };
        }

        public async Task<ReportDetailVM> GetReportDetailAsync(Guid patientId, DateOnly from, DateOnly to)
        {
            await Supa.InitAsync();

            // 1) Bu hastanın programları
            var progsResp = await Supa.Client.From<ProgramEntity>()
                .Filter("patient_id", Operator.Equals, patientId.ToString())
                .Get();
            var progs = progsResp.Models.ToList();
            var progIds = progs.Select(p => p.Id).ToList();

            var result = new ReportDetailVM
            {
                Summary = new ReportDetailSummaryVM
                {
                    RangeText = $"{from:dd.MM.yyyy} – {to:dd.MM.yyyy}"
                },
                Days = new List<ReportDetailDayVM>()
            };

            if (!progIds.Any())
                return result; // program yok

            // 2) Tarih aralığındaki günler (program_days)
            var fromStr = from.ToString("yyyy-MM-dd");
            var toStr = to.ToString("yyyy-MM-dd");

            var daysResp = await Supa.Client.From<ProgramDay>()
                .Filter("program_id", Operator.In, progIds.Select(id => id.ToString()).ToList())
                .Filter("local_date", Operator.GreaterThanOrEqual, fromStr)
                .Filter("local_date", Operator.LessThanOrEqual, toStr)
                .Get();

            var days = daysResp.Models.OrderBy(d => d.LocalDate).ToList();
            if (!days.Any())
                return result; // bu aralıkta gün yok

            // 3) Günlerin öğünleri
            var dayIds = days.Select(d => d.Id).ToList();

            var meals = new List<Meal>();
            if (dayIds.Any())
            {
                var mealsResp = await Supa.Client.From<Meal>()
                    .Filter("program_day_id", Operator.In, dayIds.Select(id => id.ToString()).ToList())
                    .Get();
                meals = mealsResp.Models.ToList();
            }

            // 4) Bu günlerin öğünlerine ait MEAL LOG'lar (hastaya göre)
            var mealIds = meals.Select(m => m.Id).ToList();
            var mealLogs = new List<MealLog>();
            if (mealIds.Any())
            {
                var logsResp = await Supa.Client.From<MealLog>()
                    .Filter("patient_id", Operator.Equals, patientId.ToString())
                    .Filter("meal_id", Operator.In, mealIds.Select(id => id.ToString()).ToList())
                    .Get();
                mealLogs = logsResp.Models.ToList();
            }

            // 5) Su logları (gün bazlı)
            var waterLogs = new List<WaterLog>();
            {
                var waterResp = await Supa.Client.From<WaterLog>()
                    .Filter("patient_id", Operator.Equals, patientId.ToString())
                    .Filter("program_day_id", Operator.In, dayIds.Select(id => id.ToString()).ToList())
                    .Get();
                waterLogs = waterResp.Models.ToList();
            }

            // 6) Günleri derle
            foreach (var day in days)
            {
                var dayMeals = meals.Where(m => m.ProgramDayId == day.Id)
                                    .OrderBy(m => m.StartTime)
                                    .ToList();

                // Öğün detayları
                var mealVMs = new List<ReportDetailMealVM>();
                int doneCount = 0;
                int skippedCount = 0;

                foreach (var meal in dayMeals)
                {
                    // Bu öğünün en son log’u (LoggedAt’e göre)
                    var lastLog = mealLogs.Where(l => l.MealId == meal.Id)
                                          .OrderBy(l => l.LoggedAt)
                                          .LastOrDefault();

                    var isDone = lastLog != null && string.Equals(lastLog.Status, "Eaten", StringComparison.OrdinalIgnoreCase);
                    var isSkipped = lastLog != null && string.Equals(lastLog.Status, "Skipped", StringComparison.OrdinalIgnoreCase);
                    if (isDone) doneCount++;
                    if (isSkipped) skippedCount++;

                    var vm = new ReportDetailMealVM
                    {
                        MealName = !string.IsNullOrWhiteSpace(meal.Title) ? meal.Title! : SlotToTr(meal.Slot),
                        IsDone = isDone,
                        CompletedAtLocal = lastLog?.LoggedAt,
                        Note = meal.Note,
                        Photos = string.IsNullOrWhiteSpace(lastLog?.PhotoUrl)
                                ? new List<string>()
                                : new List<string> { lastLog!.PhotoUrl! }
                    };

                    mealVMs.Add(vm);
                }

                // Su toplamı (L)
                var waterMl = waterLogs.Where(w => w.ProgramDayId == day.Id).Sum(w => w.Ml);
                var waterLiters = (decimal)waterMl / 1000m;

                var planned = dayMeals.Count;
                var adh = planned == 0 ? 0 : (double)doneCount / planned;

                result.Days.Add(new ReportDetailDayVM
                {
                    Date = day.LocalDate,
                    Planned = planned,
                    Done = doneCount,
                    Skipped = skippedCount, // sadece açıkça "Skipped" loglananlar
                    Adherence = adh,
                    TotalWaterLiters = waterLiters,
                    Meals = mealVMs
                });
            }

            // 7) Özet
            var totalPlanned = result.Days.Sum(x => x.Planned);
            var totalDone = result.Days.Sum(x => x.Done);
            var totalSkipped = result.Days.Sum(x => x.Skipped);
            var totalWater = result.Days.Sum(x => x.TotalWaterLiters);
            var dayCount = result.Days.Count;
            var adhPct = totalPlanned == 0 ? 0 : (int)Math.Round(100.0 * totalDone / totalPlanned);

            result.Summary.Days = dayCount;
            result.Summary.PlannedMeals = totalPlanned;
            result.Summary.DoneMeals = totalDone;
            result.Summary.SkippedMeals = totalSkipped;
            result.Summary.AdherencePct = adhPct;
            result.Summary.TotalWaterLiters = totalWater;
            result.Summary.AvgWaterLitersPerDay = dayCount == 0 ? 0 : totalWater / dayCount;

            return result;
        }

        // Slot'u Türkçeleştir (çok bilimsel bir algoritma)
        private static string SlotToTr(string slot)
        {
            return slot switch
            {
                "Breakfast" => "Kahvaltı",
                "Snack1" => "Ara Öğün 1",
                "Lunch" => "Öğle",
                "Snack2" => "Ara Öğün 2",
                "Dinner" => "Akşam",
                _ => string.IsNullOrWhiteSpace(slot) ? "Öğün" : slot
            };
        }

    }
}
