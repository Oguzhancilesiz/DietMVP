using DietMVP.Models;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Pages.Doctor;

public class ProgramRow
{
    public ProgramEntity Entity { get; set; } = default!;
    public string Range { get; set; } = "";
    public string Info { get; set; } = "";
    public string Status { get; set; } = "";   // "Aktif", "Geçmiş", "Yakında"
    public Color BadgeColor { get; set; } = Colors.Gray;
    public bool IsActive { get; set; }
    public int DaysLeft { get; set; }          // aktifse kalan gün, yakındaysa başlangıca gün
}

public partial class PatientDetailPage : ContentPage
{
    private readonly Profile _p;
    private ProgramEntity? _active;
    private List<ProgramEntity> _programs = new();

    public PatientDetailPage(Profile p)
    {
        InitializeComponent();
        _p = p;

        TitleName.Text = p.FullName;
        LblPhone.Text = string.IsNullOrWhiteSpace(p.Phone) ? "Telefon: —" : $"Telefon: {p.Phone}";
        LblWater.Text = $"{p.DailyWaterTargetMl} ml";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // email'i gradient başlığa da yazalım (boş ise "-")
        LblEmail.Text = $"E-posta: {(_p is { } ? (_p as Profile)?.FullName ?? "-" : "-")}";

        await WithBusy("Programlar yükleniyor...", async () =>
        {
            await LoadProgramsAsync();
            PopulateHeader();
        });
    }


    private async Task LoadProgramsAsync()
    {
        await Supa.InitAsync();

        var res = await Supa.Client.From<ProgramEntity>()
            .Where(x => x.PatientId == _p.Id)
            .Order(x => x.StartDate, Ordering.Descending)
            .Get();

        _programs = res.Models;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Aktif olanı bul
        _active = _programs.FirstOrDefault(p => p.StartDate <= today && p.EndDate >= today);

        // Liste VM'leri
        var rows = new List<ProgramRow>();
        foreach (var pr in _programs)
        {
            var isActive = pr.StartDate <= today && pr.EndDate >= today;
            var isPast = pr.EndDate < today;
            var isSoon = pr.StartDate > today;

            string status;
            Color color;
            int days;
            if (isActive)
            {
                status = "Aktif";
                color = Color.FromArgb("#10B981"); // yeşil
                days = (pr.EndDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days + 1; // dahil
            }
            else if (isSoon)
            {
                status = "Yakında";
                color = Color.FromArgb("#3B82F6"); // mavi
                days = (pr.StartDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
            }
            else
            {
                status = "Geçmiş";
                color = Color.FromArgb("#9CA3AF"); // gri
                days = 0;
            }

            var range = $"{pr.StartDate:dd.MM.yyyy} → {pr.EndDate:dd.MM.yyyy}";
            var info = $"{pr.DaysCount} gün"
                     + (pr.DailyWaterTargetMl.HasValue ? $" • su hedefi: {pr.DailyWaterTargetMl.Value} ml" : "");

            if (isActive)
                info += $" • kalan: {days} gün";
            else if (isSoon)
                info += $" • başlangıca: {days} gün";

            rows.Add(new ProgramRow
            {
                Entity = pr,
                Range = range,
                Info = info,
                Status = status,
                BadgeColor = color,
                IsActive = isActive,
                DaysLeft = days
            });
        }

        // Badge renkleri için ItemAppearing ile set etmek istersen ek kod yazarsın; basit tutuyoruz.
        ProgramsList.ItemsSource = rows;
    }

    private void PopulateHeader()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Varsayılan
        ActiveBadge.BackgroundColor = Color.FromArgb("#9CA3AF"); // gri
        LblActiveBadge.Text = "Aktif değil";
        LblProgram.Text = "—";
        LblStatusDetail.Text = "";
        ProgressRow.IsVisible = false;
        PrgActive.IsVisible = false;

        if (_programs == null || _programs.Count == 0) return;

        if (_active is not null)
        {
            // AKTİF
            ActiveBadge.BackgroundColor = Color.FromArgb("#10B981"); // yeşil
            LblActiveBadge.Text = "Aktif";
            LblProgram.Text = $"{_active.StartDate:dd.MM.yyyy} → {_active.EndDate:dd.MM.yyyy}";

            var daysTotal = Math.Max(1, (_active.EndDate.ToDateTime(TimeOnly.MinValue) - _active.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1);
            var daysElapsed = Math.Clamp((today.ToDateTime(TimeOnly.MinValue) - _active.StartDate.ToDateTime(TimeOnly.MinValue)).Days + 1, 0, daysTotal);
            var daysLeft = Math.Max(0, daysTotal - daysElapsed);

            LblStatusDetail.Text = $"Kalan gün: {daysLeft}";
            ProgressRow.IsVisible = true;
            PrgActive.IsVisible = true;
            PrgActive.Progress = (double)daysElapsed / daysTotal;
            LblProgressText.Text = $"{daysElapsed}/{daysTotal} gün";
            return;
        }

        // YAKINDA mı?
        var upcoming = _programs
            .Where(p => p.StartDate > today)
            .OrderBy(p => p.StartDate)
            .FirstOrDefault();

        if (upcoming is not null)
        {
            ActiveBadge.BackgroundColor = Color.FromArgb("#3B82F6"); // mavi
            LblActiveBadge.Text = "Yakında";
            LblProgram.Text = $"{upcoming.StartDate:dd.MM.yyyy} → {upcoming.EndDate:dd.MM.yyyy}";

            var daysToStart = (upcoming.StartDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
            LblStatusDetail.Text = $"Başlangıca: {daysToStart} gün";
            return;
        }

        // DEĞİLSE: BİTMİŞ son programı göster
        var last = _programs
            .OrderByDescending(p => p.EndDate)
            .First();

        ActiveBadge.BackgroundColor = Color.FromArgb("#EF4444"); // kırmızı
        LblActiveBadge.Text = "Bitti";
        LblProgram.Text = $"{last.StartDate:dd.MM.yyyy} → {last.EndDate:dd.MM.yyyy}";

        var daysAgo = (DateTime.Today - last.EndDate.ToDateTime(TimeOnly.MinValue)).Days;
        LblStatusDetail.Text = $"Bitti: {last.EndDate:dd.MM.yyyy} ({daysAgo} gün önce)";
    }

    private async void OpenProgram(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ProgramRow row) return;
        await WithBusy("Açılıyor...", async () =>
        {
            await Navigation.PushAsync(new ProgramDetailPage(row.Entity));
        });
    }

    private async void CreateProgram(object sender, EventArgs e)
    {
        await WithBusy("Sihirbaz hazırlanıyor...", async () =>
        {
            await Navigation.PushAsync(new ProgramWizardPage(_p));
        });
    }

    private void ShowBusy(string text)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            BusyText.Text = text;
            BusyOverlay.InputTransparent = false;   // tıklamaları kilitle
            BusyOverlay.Opacity = 0;
            BusyOverlay.IsVisible = true;
            await BusyOverlay.FadeTo(1, 120);
        });
    }

    private void HideBusy()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await BusyOverlay.FadeTo(0, 120);
            BusyOverlay.IsVisible = false;
            BusyOverlay.InputTransparent = true;    // tekrar geçirgen
        });
    }

    private async Task WithBusy(string text, Func<Task> work)
    {
        ShowBusy(text);
        try { await work(); }
        finally { HideBusy(); }
    }
    private async void OpenReport(object sender, EventArgs e)
    {
        // Route saçmalığı yok; doğrudan sayfayı açıyoruz.
        await Navigation.PushAsync(new ReportPage(_p));
    }
}
