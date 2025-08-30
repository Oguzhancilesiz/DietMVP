using System.Collections.ObjectModel;
using DietMVP.Models;
using DietMVP.Services;
using DietMVP.Utils;
using Microsoft.Maui.Controls;

namespace DietMVP.Pages.Patient;

public partial class PatientQaPage : ContentPage
{
    private readonly Profile _me;
    private readonly QnaService _svc = new();

    private string _status = "all";
    private DateOnly _start;
    private DateOnly _end;

    private Guid? _doctorId;
    private string? _doctorName;

    private CancellationTokenSource? _watcherCts;

    public ObservableCollection<QuestionVm> Items { get; } = new();

    public PatientQaPage()
    {
        InitializeComponent();

        _me = AppSession.CurrentProfile ?? new Profile { FullName = "Hasta" };

        var today = DateOnly.FromDateTime(DateTime.Now);
        _end = today; _start = today.AddDays(-30);

        List.ItemsSource = Items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _watcherCts?.Cancel();
        _watcherCts = new CancellationTokenSource();

        await ResolveDoctorAsync();
        await LoadAsync();

        // seçili chip’i vurgula (YENİ)
        HighlightChips();

        _svc.StartPatientWatcher(_me.Id, async vm =>
        {
            var id = NotificationHelper.IdFromGuid(vm.Id);
            await NotificationHelper.ShowNowAsync(id,
                "Sorunuz yanıtlandı",
                $"{vm.Title}\nYanıt: {vm.ShortA}");
            await LoadAsync();
        }, _watcherCts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _watcherCts?.Cancel();
    }

    private async Task ResolveDoctorAsync()
    {
        try
        {
            var doc = await _svc.GetDefaultDoctorAsync();
            if (doc != null)
            {
                _doctorId = doc.Id;
                _doctorName = string.IsNullOrWhiteSpace(doc.FullName) ? "Doktor" : doc.FullName;
                LblDoctorInfo.Text = $"Soru {_doctorName}’a gönderilecek.";
            }
            else
            {
                _doctorId = null;
                _doctorName = null;
                LblDoctorInfo.Text = "Doktor bulunamadı. Lütfen yöneticinize bildirin.";
            }
        }
        catch
        {
            _doctorId = null;
            _doctorName = null;
            LblDoctorInfo.Text = "Doktor bilgisi alınamadı.";
        }
    }

    private async Task LoadAsync()
    {
        var data = await _svc.LoadForPatientAsync(_me.Id, _start, _end, _status, "");
        Items.Clear();
        foreach (var q in data) Items.Add(q);
    }

    private async void OnSend(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text) || string.IsNullOrWhiteSpace(EdBody.Text))
        {
            await DisplayAlert("Eksik", "Başlık ve soru metni gerekli.", "Tamam");
            return;
        }
        if (_doctorId == null)
        {
            await DisplayAlert("Hata", "Doktor bulunamadı. Lütfen tekrar deneyin.", "Tamam");
            return;
        }

        try
        {
            ((Button)sender).IsEnabled = false;   // YENİ: çift tıklamayı önle
            await _svc.AskAsync(_me.Id, _doctorId.Value, TxtTitle.Text!, EdBody.Text!);
            TxtTitle.Text = ""; EdBody.Text = "";
            await LoadAsync();
            await DisplayAlert("Gönderildi", "Sorunuz iletildi.", "Tamam");
        }
        finally { ((Button)sender).IsEnabled = true; }
    }

    private async void OnChip(object sender, EventArgs e)
    {
        _status = (sender as Button)?.ClassId ?? "all";
        HighlightChips();               // YENİ
        await LoadAsync();
    }

    // ============== YENİ: Chip vurgulama ==============
    private void HighlightChips()
    {
        foreach (var child in ChipBar.Children)
        {
            if (child is Button b)
            {
                var selected = string.Equals(b.ClassId, _status, StringComparison.OrdinalIgnoreCase);
                b.BackgroundColor = selected ? Color.FromArgb("#4F46E5")
                    : (Application.Current!.Resources.TryGetValue("Color.Surface", out var c)
                        ? (Color)c : Color.FromArgb("#F3F4F6"));
                b.TextColor = selected ? Colors.White
                    : (Application.Current!.Resources.TryGetValue("Color.Ink", out var i) ? (Color)i : Colors.Black);
            }
        }
    }
}
