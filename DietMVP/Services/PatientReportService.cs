using DietMVP.Models;
using DietMVP.Pages.Patient;
using Microsoft.Maui.Controls;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Services;

public class PatientReportService
{
    public async Task<ReportResult> LoadReportAsync(Guid patientId, DateOnly from, DateOnly to)
    {
        await Supa.InitAsync();

        // İlgili program
        var progs = await Supa.Client.From<ProgramEntity>()
            .Filter("patient_id", Operator.Equals, patientId.ToString())
            .Filter("end_date", Operator.GreaterThanOrEqual, from.ToString("yyyy-MM-dd"))
            .Filter("start_date", Operator.LessThanOrEqual, to.ToString("yyyy-MM-dd"))
            .Order(p => p.StartDate, Ordering.Ascending)
            .Get();

        var prog = progs.Models.FirstOrDefault();
        if (prog == null)
            return new ReportResult();

        // Günler (aralık)
        var dayRes = await Supa.Client.From<ProgramDay>()
            .Filter("program_id", Operator.Equals, prog.Id.ToString())
            .Filter("local_date", Operator.GreaterThanOrEqual, from.ToString("yyyy-MM-dd"))
            .Filter("local_date", Operator.LessThanOrEqual, to.ToString("yyyy-MM-dd"))
            .Order(d => d.LocalDate, Ordering.Ascending)
            .Get();

        var now = DateTime.Now;

        var daysOut = new List<DayGroupVm>();
        int tot = 0, done = 0, ontime = 0, early = 0, late = 0, skipped = 0, missed = 0, upcoming = 0, inprog = 0;

        foreach (var d in dayRes.Models)
        {
            var mealRes = await Supa.Client.From<Meal>()
                .Filter("program_day_id", Operator.Equals, d.Id.ToString())
                .Order(m => m.StartTime, Ordering.Ascending)
                .Get();

            var dayItems = new List<MealReportVm>();

            foreach (var m in mealRes.Models)
            {
                // Items
                var itemsRes = await Supa.Client.From<MealItem>()
                    .Filter("meal_id", Operator.Equals, m.Id.ToString())
                    .Order(i => i.Sort, Ordering.Ascending)
                    .Get();

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

                // Son log
                var logsRes = await Supa.Client.From<MealLog>()
                    .Filter("patient_id", Operator.Equals, patientId.ToString())
                    .Filter("meal_id", Operator.Equals, m.Id.ToString())
                    .Order(l => l.LoggedAt, Ordering.Descending)
                    .Get();

                var lastLog = logsRes.Models.FirstOrDefault();

                // durum hesabı
                var start = d.LocalDate.ToDateTime(m.StartTime);
                var end = d.LocalDate.ToDateTime(m.EndTime);

                var vm = new MealReportVm
                {
                    MealId = m.Id,
                    SlotEmoji = SlotEmoji(m.Slot),
                    Title = string.IsNullOrWhiteSpace(m.Title) ? SlotToTitle(m.Slot) : m.Title!,
                    TimeRange = $"{m.StartTime:hh\\:mm} – {m.EndTime:hh\\:mm}",
                    Items = itemsText,
                    ItemsJoined = string.Join(" | ", itemsText),
                    PhotoUrl = BuildPhotoUrl(lastLog?.PhotoUrl)
                };

                if (lastLog == null)
                {
                    if (now < start)
                    {
                        vm.State = MealState.Upcoming; vm.StatusText = "Bekliyor";
                        vm.BadgeColor = Color.FromArgb("#F59E0B");
                        var t = start - now; vm.RelativeText = $"{(int)t.TotalHours} sa {t.Minutes} dk sonra";
                        upcoming++;
                    }
                    else if (now <= end)
                    {
                        vm.State = MealState.InProgress; vm.StatusText = "Devam ediyor";
                        vm.BadgeColor = Color.FromArgb("#3B82F6");
                        var t = end - now; vm.RelativeText = $"Şimdi • {t.Minutes} dk kaldı";
                        inprog++;
                    }
                    else
                    {
                        vm.State = MealState.Missed; vm.StatusText = "Kaçırıldı";
                        vm.BadgeColor = Color.FromArgb("#EF4444");
                        var t = now - end; vm.RelativeText = $"{(int)t.TotalHours} sa {t.Minutes} dk gecikti";
                        missed++;
                    }
                }
                else
                {
                    var skippedFlag = string.Equals(lastLog.Status, "Skipped", StringComparison.OrdinalIgnoreCase);
                    vm.IsDone = !skippedFlag;

                    if (skippedFlag)
                    {
                        vm.State = MealState.Skipped;
                        vm.StatusText = "Atlandı";
                        vm.BadgeColor = Color.FromArgb("#EF4444");
                        vm.RelativeText = "Atlandı";
                        skipped++;
                    }
                    else
                    {
                        var eaten = lastLog.LoggedAt.ToLocalTime();
                        if (eaten < start)
                        {
                            vm.State = MealState.DoneEarly;
                            vm.StatusText = "Yapıldı (Erken)";
                            vm.BadgeColor = Color.FromArgb("#10B981");
                            var t = start - eaten; vm.RelativeText = $"Zamanından önce • {(int)t.TotalHours} sa {t.Minutes} dk";
                            early++; done++;
                        }
                        else if (eaten <= end)
                        {
                            vm.State = MealState.DoneOnTime;
                            vm.StatusText = "Yapıldı";
                            vm.BadgeColor = Colors.Green;
                            vm.RelativeText = "Zamanında yendi 👌";
                            ontime++; done++;
                        }
                        else
                        {
                            vm.State = MealState.DoneLate;
                            vm.StatusText = "Yapıldı (Geç)";
                            vm.BadgeColor = Color.FromArgb("#16A34A");
                            var t = eaten - end; vm.RelativeText = $"{(int)t.TotalHours} sa {t.Minutes} dk geç";
                            late++; done++;
                        }
                    }
                }

                tot++;
                dayItems.Add(vm);
            }

            var header = d.LocalDate.ToDateTime(TimeOnly.MinValue)
                .ToString("dddd, dd.MM.yyyy", new System.Globalization.CultureInfo("tr-TR"));

            daysOut.Add(new DayGroupVm
            {
                Date = d.LocalDate,
                Header = header,
                Items = new System.Collections.ObjectModel.ObservableCollection<MealReportVm>(dayItems),
                SmallSummary = $"{dayItems.Count} öğün"
            });
        }

        return new ReportResult
        {
            Days = daysOut,
            Total = tot,
            Done = done,
            DoneOnTime = ontime,
            DoneEarly = early,
            DoneLate = late,
            Skipped = skipped,
            Missed = missed,
            Upcoming = upcoming,
            InProgress = inprog
        };
    }

    private static string BuildPhotoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url ?? "";
        return url + (url.Contains("?") ? "&" : "?") + "ts=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static string SlotToTitle(string slot) => slot switch
    {
        "Breakfast" => "Kahvaltı",
        "Snack1" => "Ara Öğün 1",
        "Lunch" => "Öğle",
        "Snack2" => "Ara Öğün 2",
        "Dinner" => "Akşam",
        _ => slot
    };

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

// Sonuç kabı
public class ReportResult
{
    public List<DayGroupVm> Days { get; set; } = new();

    public int Total { get; set; }
    public int Done { get; set; }
    public int DoneOnTime { get; set; }
    public int DoneEarly { get; set; }
    public int DoneLate { get; set; }
    public int Skipped { get; set; }
    public int Missed { get; set; }
    public int Upcoming { get; set; }
    public int InProgress { get; set; }
}
