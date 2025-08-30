using DietMVP.Models;
using DietMVP.Services;
using DietMVP.Utils;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Pages.Doctor;

public class ExpiringVm
{
    public ProgramEntity Prog { get; set; } = default!;
    public string PatientName { get; set; } = "Hasta";
    public string Range { get; set; } = "";
    public int DaysLeft { get; set; }
    public string DaysLeftText => DaysLeft <= 0 ? "Bugün bitiyor" : $"{DaysLeft} gün kaldı";
    public double Progress { get; set; } // 0..1 (tamamlanan oran)
}

public partial class DashboardPage : ContentPage
{
    private readonly Profile _me;

    public DashboardPage()
    {
        InitializeComponent();
        Toast.Register(this);

        _me = AppSession.CurrentProfile ?? new Profile { FullName = "Doktor" };
        LblToday.Text = DateTime.Now.ToString(
            "dddd, dd.MM.yyyy",
            new System.Globalization.CultureInfo("tr-TR")
        );
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefresh(object? sender, EventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await WithBusy("Veriler getiriliyor...", async () =>
        {
            await Supa.InitAsync();

            var today = DateOnly.FromDateTime(DateTime.Now);

            // Toplam hasta
            var patients = await Supa.Client.From<Profile>()
                .Where(p => p.Role == "patient")
                .Get();
            LblPatients.Text = patients.Models.Count.ToString();

            // Aktif program sayısı
            var activeProgs = await Supa.Client.From<ProgramEntity>()
                .Where(p => p.StartDate <= today)
                .Where(p => p.EndDate >= today)
                .Get();
            LblActivePrograms.Text = activeProgs.Models.Count.ToString();

            // Bugün planlanan öğün toplamı (gün * 5 varsayımı)
            var daysToday = await Supa.Client.From<ProgramDay>()
                .Where(d => d.LocalDate == today)
                .Get();
            var totalMealsToday = daysToday.Models.Count * 5;

            // Bugün (yerel -> UTC)
            var startLocal = DateTime.Today;
            var endLocal = startLocal.AddDays(1);
            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

            // Meal logs (bugün gerçekleşen)
            var mealLogs = await Supa.Client.From<MealLog>()
                .Filter("logged_at", Operator.GreaterThanOrEqual, startUtc)
                .Filter("logged_at", Operator.LessThan, endUtc)
                .Get();
            LblMealsToday.Text = $"{mealLogs.Models.Count} / {totalMealsToday}";

            // Water logs (bugün toplam ml)
            var waterLogs = await Supa.Client.From<WaterLog>()
                .Filter("logged_at", Operator.GreaterThanOrEqual, startUtc)
                .Filter("logged_at", Operator.LessThan, endUtc)
                .Get();
            var totalMl = waterLogs.Models.Sum(x => x.Ml);
            LblWaterToday.Text = $"{totalMl / 1000.0:0.##} L";

            // Cevapsız soru sayısı (yalnızca bu doktora)
            var openQs = await Supa.Client.From<Question>()
                .Where(q => q.DoctorId == _me.Id)
                .Where(q => q.Status == "Open")
                .Get();
            var openCount = openQs.Models.Count;
            BtnOpenQs.Text = openCount.ToString();
            LblOpenQsDesc.Text = openCount == 0
                ? "Tebrikler, açık soru yok."
                : $"{openCount} adet bekleyen soru";


            // Yakında bitecek programlar (bugün dahil, 3 gün içinde)
            var soon = today.AddDays(3);
            var expiringRes = await Supa.Client.From<ProgramEntity>()
                .Where(p => p.EndDate >= today)
                .Where(p => p.EndDate <= soon)
                .Order(p => p.EndDate, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();
            var expiring = expiringRes.Models;

            // İlgili hasta adlarını topla
            var patientIds = expiring.Select(p => p.PatientId).Distinct().ToList();
            var dict = new Dictionary<Guid, string>();
            if (patientIds.Count > 0)
            {
                // IN filtresi için liste veriyoruz
                var idList = patientIds.Select(x => x.ToString()).ToList();
                var profRes = await Supa.Client.From<Profile>()
                    .Filter("id", Operator.In, idList)
                    .Get();

                dict = profRes.Models.ToDictionary(
                    p => p.Id,
                    p => string.IsNullOrWhiteSpace(p.FullName) ? "Hasta" : p.FullName
                );
            }

            var expiringVms = expiring.Select(p =>
            {
                var totalDays = (p.EndDate.ToDateTime(TimeOnly.MinValue) - p.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
                var gone = (today.ToDateTime(TimeOnly.MinValue) - p.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1;
                if (gone < 0) gone = 0;
                var progress = totalDays > 0 ? Math.Clamp(gone / (double)totalDays, 0, 1) : 0;

                return new ExpiringVm
                {
                    Prog = p,
                    PatientName = dict.TryGetValue(p.PatientId, out var n) ? n : "Hasta",
                    Range = $"{p.StartDate:dd.MM.yyyy} → {p.EndDate:dd.MM.yyyy}",
                    DaysLeft = (p.EndDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days + 1,
                    Progress = progress
                };
            }).ToList();

            ExpiringList.ItemsSource = expiringVms;
            var expCount = expiringVms.Count;

            BtnEndingSoon.Text = expCount.ToString();
            LblEndingSoonDesc.Text = expCount == 0
                ? "Önümüzdeki 3 günde biten yok"
                : $"{expCount} program yakında bitiyor";


            await Toast.Show("Güncel veriler yüklendi.");
        });

        RefreshHost.IsRefreshing = false;
    }

    // Alt-sheet busy
    private async Task WithBusy(string text, Func<Task> work)
    {
        BusyText.Text = text;
        BusyOverlay.IsVisible = true;
        await Task.WhenAll(
            BusyCard.FadeTo(1, 140),
            BusyCard.TranslateTo(0, 0, 140, Easing.CubicOut)
        );

        try { await work(); }
        catch (Exception ex) { await Toast.Show($"Hata: {ex.Message}", 2400); }
        finally
        {
            await Task.WhenAll(
                BusyCard.FadeTo(0, 140),
                BusyCard.TranslateTo(0, 40, 140, Easing.CubicIn)
            );
            BusyOverlay.IsVisible = false;
        }
    }

    // NAV
    private async void GoProgramForExisting(object sender, EventArgs e)
    {
        try
        {
            await Toast.Show("Hasta listesi yükleniyor...");
            var ps = new PatientService();
            var list = await ps.GetPatientsAsync();
            if (list.Count == 0) { await Toast.Show("Kayıtlı hasta yok.", 1800); return; }

            var names = list.Select(p => p.FullName).ToArray();
            var pick = await DisplayActionSheet("Hasta seç", "Vazgeç", null, names);
            var sel = list.FirstOrDefault(p => p.FullName == pick);
            if (sel == null) return;

            await Navigation.PushAsync(new ProgramWizardPage(sel));
        }
        catch (Exception ex) { await Toast.Show($"Hata: {ex.Message}", 2500); }
    }

    private async void GoQuestions(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//sorucevap"); }
        catch (Exception ex) { await Toast.Show($"Hata: {ex.Message}", 2200); }
    }

    private async void ScrollToExpiring(object sender, EventArgs e)
    {
        try
        {
            if (SectionExpiring is null) return;
            await SectionExpiring.ScaleTo(1.04, 100, Easing.CubicOut);
            await SectionExpiring.ScaleTo(1.00, 100, Easing.CubicIn);
        }
        catch { /* yok say */ }
    }

    private async void GoNewPatient(object sender, EventArgs e)
    {
        try { await Navigation.PushAsync(new QuickCreatePage()); }
        catch (Exception ex) { await Toast.Show($"Hata: {ex.Message}", 2200); }
    }

    private async void GoPatients(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//patients"); }
        catch (Exception ex) { await Toast.Show($"Hata: {ex.Message}", 2200); }
    }


    private async void OpenProgram(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ExpiringVm vm) return;
        await Navigation.PushAsync(new ProgramDetailPage(vm.Prog));
    }
}
