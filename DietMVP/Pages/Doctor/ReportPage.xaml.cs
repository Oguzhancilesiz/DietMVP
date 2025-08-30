using System.Collections.ObjectModel;
using DietMVP.Models;
using DietMVP.Services;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Pages.Doctor;

public partial class ReportPage : ContentPage
{
    private readonly Profile _patient;
    private readonly ReportService _report = new();

    // Programý biten hastalar listesi
    private readonly ObservableCollection<EndedPatientVm> _endedItems = new();

    public ReportPage(Profile patient)
    {
        InitializeComponent();
        _patient = patient;
        HeaderTitle.Text = $"{_patient.FullName} – Rapor";
        EndedList.ItemsSource = _endedItems;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await WithBusy("Rapor hazýrlanýyor...", async () =>
        {
            await LoadReportAsync();
            await LoadEndedPatientsAsync();
        });
    }

    private async Task LoadReportAsync()
    {
        try
        {
            var rep = await _report.BuildAsync(_patient.Id);

            SumDays.Text = rep.Summary.Days.ToString();
            SumMealPlanned.Text = rep.Summary.PlannedMeals.ToString();
            SumMealDone.Text = rep.Summary.DoneMeals.ToString();
            SumMealSkipped.Text = rep.Summary.SkippedMeals.ToString();

            var adh = rep.Summary.AdherencePct;
            SumAdh.Text = $"{adh:0}%";
            SumAdhDesc.Text = adh >= 80 ? "iyi uyum" : adh >= 50 ? "orta" : "zayýf";

            SumWaterLiters.Text = $"{rep.Summary.TotalWaterLiters:0.##}";
            AvgWaterLiters.Text = $"{rep.Summary.AvgWaterLitersPerDay:0.##}";
            SumRange.Text = rep.Summary.RangeText;

            HdrRange.Text = rep.Summary.RangeText; // hero alt baþlýðý

            DaysList.ItemsSource = rep.Days;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", $"Veri alýnamadý: {ex.Message}", "Tamam");
            DaysList.ItemsSource = Array.Empty<ReportDayVM>();
        }
    }

    /// <summary>
    /// Son 30 günde programý bitmiþ hastalarý listeler.
    /// </summary>
    private async Task LoadEndedPatientsAsync()
    {
        _endedItems.Clear();

        await Supa.InitAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var cutoff = today.AddDays(-30);

        // 1) Son 30 gün içinde biten tüm programlar
        var progRes = await Supa.Client.From<ProgramEntity>()
            .Filter("end_date", Operator.LessThan, today.ToString("yyyy-MM-dd"))
            .Filter("end_date", Operator.GreaterThanOrEqual, cutoff.ToString("yyyy-MM-dd"))
            .Order(p => p.EndDate, Ordering.Descending)
            .Get();

        if (progRes.Models.Count == 0)
        {
            EndedCard.IsVisible = false;
            return;
        }

        // 2) Hasta bazýnda en son biten programý seç
        var lastByPatient = progRes.Models
            .GroupBy(p => p.PatientId)
            .Select(g => g.OrderByDescending(x => x.EndDate).First())
            .ToList();

        // 3) Profil bilgilerini al (küçük listeler için tek tek almak basit)
        var vms = new List<EndedPatientVm>();
        foreach (var pr in lastByPatient)
        {
            try
            {
                var profRes = await Supa.Client.From<Profile>()
                    .Filter("id", Operator.Equals, pr.PatientId.ToString())
                    .Get();

                var prof = profRes.Models.FirstOrDefault();
                if (prof == null) continue;

                // Ayný ekranda incelediðin hastayý istersen çýkar:
                // if (prof.Id == _patient.Id) continue;

                var daysAgo = (DateTime.Today - pr.EndDate.ToDateTime(TimeOnly.MinValue)).Days;
                var endText = $"Bitti: {pr.EndDate:dd.MM.yyyy} ({daysAgo} gün önce)";

                vms.Add(new EndedPatientVm
                {
                    Patient = prof,
                    Name = string.IsNullOrWhiteSpace(prof.FullName) ? "Hasta" : prof.FullName,
                    EndText = endText,
                    LastEnd = pr.EndDate
                });
            }
            catch { /* tek profil hatasý tüm listeyi bozmasýn */ }
        }

        // 4) En yeni biten en üstte
        foreach (var vm in vms.OrderByDescending(x => x.LastEnd))
            _endedItems.Add(vm);

        EndedCard.IsVisible = _endedItems.Count > 0;
    }

    private async void OnCreateProgramForEnded(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not EndedPatientVm vm) return;
        await Navigation.PushAsync(new ProgramWizardPage(vm.Patient));
    }

    private async void OnReportDetailClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ReportDetailPage(_patient));
    }

    #region Busy helpers
    private void ShowBusy(string text)
    {
        BusyText.Text = text;
        BusyOverlay.InputTransparent = false;
        BusyOverlay.Opacity = 0;
        BusyOverlay.IsVisible = true;
        _ = BusyOverlay.FadeTo(1, 120);
    }

    private async Task HideBusy()
    {
        await BusyOverlay.FadeTo(0, 120);
        BusyOverlay.IsVisible = false;
        BusyOverlay.InputTransparent = true;
    }

    private async Task WithBusy(string text, Func<Task> work)
    {
        ShowBusy(text);
        try { await work(); }
        finally { await HideBusy(); }
    }
    #endregion
}

public class EndedPatientVm
{
    public Profile Patient { get; set; } = default!;
    public string Name { get; set; } = "";
    public string EndText { get; set; } = "";
    public DateOnly LastEnd { get; set; }
}
