using System.Collections.ObjectModel;
using System.Globalization;
using DietMVP.Models;
using DietMVP.Services;
using Microsoft.Maui.Controls;

namespace DietMVP.Pages.Patient;

public partial class ReportPage : ContentPage
{
    private readonly Profile _me;
    private readonly PatientReportService _svc = new();   // <- senin isim
    private readonly CultureInfo _tr = new("tr-TR");

    // UI state
    private DateOnly _start;
    private DateOnly _end;
    private string _statusFilter = "all"; // all, ontime, early, late, skipped, missed, inprog, upcoming
    private bool _onlyPhotos = false;
    private string _query = "";

    public ObservableCollection<DayGroupVm> Groups { get; } = new();

    public ReportPage()
    {
        InitializeComponent();
        BindingContext = this;

        _me = AppSession.CurrentProfile ?? new Profile { FullName = "Hasta" };

        // Varsayılan: Son 7 gün (yerel saat)
        var today = DateOnly.FromDateTime(DateTime.Now);
        _end = today;
        _start = today.AddDays(-6);

        DpStart.Date = _start.ToDateTime(TimeOnly.MinValue);
        DpEnd.Date = _end.ToDateTime(TimeOnly.MinValue);

        UpdateRangeLabel();

        _ = LoadAsync();
        HighlightSelectedChip();
    }

    private void UpdateRangeLabel()
    {
        LblRange.Text = $"{_start.ToDateTime(TimeOnly.MinValue):dd.MM.yyyy} — {_end.ToDateTime(TimeOnly.MinValue):dd.MM.yyyy}";
    }

    private async Task LoadAsync()
    {
        RefreshHost.IsRefreshing = true;

        var report = await _svc.LoadReportAsync(_me.Id, _start, _end);

        // filtre uygula
        var filtered = ApplyFilters(report);

        // özet
        SumTotal.Text = filtered.Total.ToString();
        SumDone.Text = filtered.Done.ToString();
        SumOnTime.Text = filtered.DoneOnTime.ToString();
        SumLate.Text = filtered.DoneLate.ToString();
        SumEarly.Text = filtered.DoneEarly.ToString();
        SumSkipped.Text = filtered.Skipped.ToString();
        SumMissed.Text = filtered.Missed.ToString();
        SumUpcoming.Text = filtered.Upcoming.ToString();

        // gruplar
        Groups.Clear();
        foreach (var g in filtered.Days)
            Groups.Add(g);
        List.ItemsSource = Groups;

        RefreshHost.IsRefreshing = false;
    }

    private ReportResult ApplyFilters(ReportResult src)
    {
        bool StatusPass(MealReportVm m) => _statusFilter switch
        {
            "ontime" => m.State == MealState.DoneOnTime,
            "early" => m.State == MealState.DoneEarly,
            "late" => m.State == MealState.DoneLate,
            "skipped" => m.State == MealState.Skipped,
            "missed" => m.State == MealState.Missed,
            "inprog" => m.State == MealState.InProgress,
            "upcoming" => m.State == MealState.Upcoming,
            _ => true
        };

        bool PhotoPass(MealReportVm m) => !_onlyPhotos || m.HasPhoto;

        bool QueryPass(MealReportVm m)
        {
            if (string.IsNullOrWhiteSpace(_query)) return true;
            var q = _query.Trim().ToLowerInvariant();
            if ((m.Title ?? "").ToLowerInvariant().Contains(q)) return true;
            if ((m.ItemsJoined ?? "").ToLowerInvariant().Contains(q)) return true;
            return false;
        }

        var days = new List<DayGroupVm>();
        int tot = 0, done = 0, ontime = 0, early = 0, late = 0, skipped = 0, missed = 0, upcoming = 0, inprog = 0;

        foreach (var d in src.Days)
        {
            var items = d.Items.Where(m => StatusPass(m) && PhotoPass(m) && QueryPass(m)).ToList();
            if (items.Count == 0) continue;

            var total = items.Count;
            var doneCount = items.Count(x => x.IsDone);
            var lateCount = items.Count(x => x.State == MealState.DoneLate);
            var notDoneCount = items.Count(x => x.State is MealState.Skipped or MealState.Missed);

            var g = new DayGroupVm
            {
                Date = d.Date,
                Header = d.Header,
                Items = new ObservableCollection<MealReportVm>(items),
                SmallSummary =
                    $"{items.Count} öğün • " +
                    $"{items.Count(x => x.State == MealState.DoneOnTime)} zamanında, " +
                    $"{items.Count(x => x.State == MealState.DoneLate)} geç, " +
                    $"{items.Count(x => x.State is MealState.Skipped or MealState.Missed)} yapılmadı",

                // yeni alanlar
                Total = total,
                Done = doneCount,
                Late = lateCount,
                NotDone = notDoneCount,
                CompletionRate = total == 0 ? 0 : (double)doneCount / total
            };

            days.Add(g);

            tot += items.Count;
            done += items.Count(x => x.IsDone);
            ontime += items.Count(x => x.State == MealState.DoneOnTime);
            early += items.Count(x => x.State == MealState.DoneEarly);
            late += items.Count(x => x.State == MealState.DoneLate);
            skipped += items.Count(x => x.State == MealState.Skipped);
            missed += items.Count(x => x.State == MealState.Missed);
            upcoming += items.Count(x => x.State == MealState.Upcoming);
            inprog += items.Count(x => x.State == MealState.InProgress);
        }

        return new ReportResult
        {
            Days = days,
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

    // --------- Events
    private async void OnRefresh(object sender, EventArgs e) => await LoadAsync();

    // DİKKAT: DatePicker için DateSelected event'i
    private async void OnDateChanged(object sender, DateChangedEventArgs e)
    {
        _start = DateOnly.FromDateTime(DpStart.Date);
        _end = DateOnly.FromDateTime(DpEnd.Date);
        if (_end < _start) _end = _start;
        UpdateRangeLabel();
        await LoadAsync();
    }

    private async void OnQuickRange(object sender, EventArgs e)
    {
        var param = (sender as Button)?.CommandParameter?.ToString() ?? "7";
        var now = DateOnly.FromDateTime(DateTime.Now);

        switch (param)
        {
            case "today":
                _start = now; _end = now; break;
            case "month":
                var dt = DateTime.Now;
                _start = new DateOnly(dt.Year, dt.Month, 1);
                _end = new DateOnly(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month));
                break;
            default: // "7"
                _end = now; _start = now.AddDays(-6); break;
        }

        DpStart.Date = _start.ToDateTime(TimeOnly.MinValue);
        DpEnd.Date = _end.ToDateTime(TimeOnly.MinValue);
        UpdateRangeLabel();
        await LoadAsync();
    }

    private async void OnStatusChip(object sender, EventArgs e)
    {
        var id = (sender as Button)?.ClassId ?? "all";
        _statusFilter = id;
        HighlightSelectedChip();
        await LoadAsync();
    }

    private void HighlightSelectedChip()
    {
        if (StatusChips == null) return; // Hot Reload sırasında null olmasın

        foreach (var child in StatusChips.Children)
        {
            if (child is Button b)
            {
                var selected = string.Equals(b.ClassId, _statusFilter, StringComparison.OrdinalIgnoreCase);
                b.BackgroundColor = selected ? Color.FromArgb("#6D28D9")
                    : (Application.Current!.Resources.TryGetValue("Color.Surface", out var c) ? (Color)c : Color.FromArgb("#F3F4F6"));
                b.TextColor = selected ? Colors.White : (Application.Current!.Resources.TryGetValue("Color.Ink", out var ink) ? (Color)ink : Colors.Black);
                b.CornerRadius = 14;
            }
        }
    }

    private async void OnApplyFilters(object? sender, EventArgs e)
    {
        _query = TxtQuery.Text ?? "";
        await LoadAsync();
    }

    private async void OnFilterEnter(object? sender, EventArgs e)
    {
        _query = TxtQuery.Text ?? "";
        await LoadAsync();
    }

    // Switch için ayrı handler (delegate imzası farklı)
    private async void OnPhotoToggle(object sender, ToggledEventArgs e)
    {
        _onlyPhotos = e.Value;
        await LoadAsync();
    }
}

// ====== ViewModels ======
public class DayGroupVm
{
    public DateOnly Date { get; set; }
    public string Header { get; set; } = "";
    public string SmallSummary { get; set; } = "";
    public ObservableCollection<MealReportVm> Items { get; set; } = new();
    // YENİ: header’daki çipler ve progress için
    public int Total { get; set; }
    public int Done { get; set; }
    public int Late { get; set; }
    public int NotDone { get; set; } // skipped + missed
    public double CompletionRate { get; set; } // 0..1
}

public enum MealState { Upcoming, InProgress, DoneOnTime, DoneEarly, DoneLate, Skipped, Missed }

public class MealReportVm
{
    public Guid MealId { get; set; }
    public string SlotEmoji { get; set; } = "🍽️";
    public string Title { get; set; } = "";
    public string TimeRange { get; set; } = "";
    public List<string> Items { get; set; } = new();
    public string? ItemsJoined { get; set; }

    public bool IsDone { get; set; }
    public MealState State { get; set; }
    public string StatusText { get; set; } = "";
    public string RelativeText { get; set; } = "";

    public string? PhotoUrl { get; set; }
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);

    public Color BadgeColor { get; set; } = Color.FromArgb("#F59E0B");
}
