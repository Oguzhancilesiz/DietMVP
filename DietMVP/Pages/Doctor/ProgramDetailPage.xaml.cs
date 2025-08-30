using System.Globalization;
using DietMVP.Models;
using DietMVP.Services;

namespace DietMVP.Pages.Doctor;

[QueryProperty(nameof(ProgramId), "programId")]
public partial class ProgramDetailPage : ContentPage
{
    public string? ProgramId { get; set; }

    private readonly ProgramEntity _prog;
    private readonly DietService _diet = new();

    public ProgramDetailPage(ProgramEntity prog)
    {
        InitializeComponent();
        _prog = prog;
        Title = $"Program: {_prog.StartDate:dd.MM} - {_prog.EndDate:dd.MM}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // (İstersek burada ProgramId ile fetch yapabiliriz, şimdilik ctor’da gelen _prog kullanılıyor)
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await ShowBusy("Günler yükleniyor...");

        try
        {
            // günleri çek
            var days = await _diet.GetDaysAsync(_prog.Id);

            // header metinleri
            HeaderRange.Text = $"{_prog.StartDate:dd.MM.yyyy} → {_prog.EndDate:dd.MM.yyyy}";
            HeaderInfo.Text = $"{days.Count} gün • Su hedefi: {(_prog.DailyWaterTargetMl ?? 2000)} ml";

            // yemekleri paralel çek (performans)
            var tasks = days.ToDictionary(d => d, d => _diet.GetMealsOfDayAsync(d.Id));
            await Task.WhenAll(tasks.Values);

            var ci = new CultureInfo("tr-TR");
            var rows = new List<DayRow>();
            for (int i = 0; i < days.Count; i++)
            {
                var d = days[i];
                var meals = tasks[d].Result;

                rows.Add(new DayRow
                {
                    Day = d,
                    DateText = d.LocalDate.ToString("dd.MM.yyyy"),
                    SubtitleText = $"Gün {i + 1} • {d.LocalDate.ToString("dddd", ci)}",
                    Slots = meals.Select(m => MapSlot(m.Slot)).Distinct().ToList(),
                    FooterText = $"{meals.Count} öğün • Su hedefi: {(_prog.DailyWaterTargetMl ?? 2000)} ml"
                });
            }

            DaysList.ItemsSource = rows;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", ex.Message, "Tamam");
            DaysList.ItemsSource = Array.Empty<DayRow>();
        }
        finally
        {
            if (Refresh.IsRefreshing) Refresh.IsRefreshing = false;
            await HideBusy();
        }
    }

    // Kart tıklanınca gün detayına git
    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if ((sender as Border)?.BindingContext is DayRow row)
            await Navigation.PushAsync(new DayDetailPage(row.Day));
    }

    // Pull-to-refresh
    private async void OnRefresh(object sender, EventArgs e) => await LoadAsync();

    private static string MapSlot(string slot) => slot switch
    {
        "Breakfast" => "Kahvaltı",
        "Snack1" => "Ara 1",
        "Lunch" => "Öğle",
        "Snack2" => "Ara 2",
        "Dinner" => "Akşam",
        _ => slot
    };

    // ---------- Busy helpers (animasyonlu) ----------
    private async Task ShowBusy(string text)
    {
        BusyText.Text = text;
        BusyOverlay.IsVisible = true;
        BusyOverlay.InputTransparent = false;
        BusyCard.Opacity = 0;
        BusyCard.Scale = 0.96;
        await Task.WhenAll(
            BusyCard.FadeTo(1, 140, Easing.CubicOut),
            BusyCard.ScaleTo(1.0, 140, Easing.CubicOut)
        );
    }

    private async Task HideBusy()
    {
        await Task.WhenAll(
            BusyCard.FadeTo(0, 120, Easing.CubicIn),
            BusyCard.ScaleTo(0.96, 120, Easing.CubicIn)
        );
        BusyOverlay.IsVisible = false;
        BusyOverlay.InputTransparent = true;
    }
}

// XAML’in ItemTemplate’i buna bound oluyor
public class DayRow
{
    public ProgramDay Day { get; set; } = default!;
    public string DateText { get; set; } = "";
    public string SubtitleText { get; set; } = "";
    public List<string> Slots { get; set; } = new();
    public string FooterText { get; set; } = "";
}
