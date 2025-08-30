using System.Collections.ObjectModel;
using DietMVP.Models;
using DietMVP.Services;
using DietMVP.Utils;

namespace DietMVP.Pages.Doctor;

public partial class PatientsPage : ContentPage
{
    private readonly PatientService _ps = new();
    private readonly ObservableCollection<Profile> _items = new();
    private readonly List<Profile> _all = new();
    private CancellationTokenSource? _searchCts;

    public PatientsPage()
    {
        InitializeComponent();
        PatientsList.ItemsSource = _items;
        Toast.Register(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync(bool showToast = true)
    {
        _all.Clear();
        _items.Clear();

        var list = await _ps.GetPatientsAsync();
        _all.AddRange(list);
        foreach (var p in list) _items.Add(p);

        UpdateStats();
        if (showToast) await Toast.Show($"Toplam {_items.Count} kişi");
    }

    private void UpdateStats(string? query = null)
    {
        var total = _all.Count;
        var shown = _items.Count;
        Stats.Text = string.IsNullOrWhiteSpace(query)
            ? $"Toplam: {total} • Listelenen: {shown}"
            : $"Toplam: {total} • Eşleşen: {shown} • Arama: “{query}”";
    }

    private async void OnRefresh(object? sender, EventArgs e)
    {
        try { await LoadAsync(showToast: false); }
        finally { RefreshViewControl.IsRefreshing = false; } // ← isim düzeltildi
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = (e.NewTextValue ?? "").Trim();
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = DebouncedFilterAsync(query, _searchCts.Token);
    }

    private async Task DebouncedFilterAsync(string query, CancellationToken token)
    {
        try { await Task.Delay(250, token); } catch { return; }
        if (token.IsCancellationRequested) return;

        if (string.IsNullOrWhiteSpace(query))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _items.Clear();
                foreach (var p in _all) _items.Add(p);
                UpdateStats();
            });
            return;
        }

        var needle = query.ToLowerInvariant();
        var local = _all.Where(p =>
            (!string.IsNullOrEmpty(p.FullName) && p.FullName.ToLowerInvariant().Contains(needle))
        ).ToList();

        try
        {
            var remote = await _ps.SearchPatientsAsync(query);
            var merged = remote.Concat(local).GroupBy(p => p.Id).Select(g => g.First()).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _items.Clear();
                foreach (var p in merged) _items.Add(p);
                UpdateStats(query);
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _items.Clear();
                foreach (var p in local) _items.Add(p);
                UpdateStats(query);
            });
        }
    }

    // Detay butonu
    private async void OnGoDetail(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is Profile p)
            await Navigation.PushAsync(new PatientDetailPage(p));
    }

    private async void OnGoReport(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is Profile p)
            await Navigation.PushAsync(new ReportPage(p));
    }
    // sağa kaydırma aksiyonları (SwipeView)
    private async void OnSwipeDetail(object sender, EventArgs e)
    {
        if (sender is SwipeItem { CommandParameter: Profile p })
            await Navigation.PushAsync(new PatientDetailPage(p));
    }

    private async void OnSwipeReport(object sender, EventArgs e)
    {
        if (sender is SwipeItem { CommandParameter: Profile p })
            await Navigation.PushAsync(new ReportPage(p));
    }

    // üstteki hızlı butonlar
    private async void OnAddNew(object sender, EventArgs e)
    {
        // "Ekle" sekmesine at
        await Shell.Current.GoToAsync("//quickcreate");
    }

    private async void OnOpenQuickProgram(object sender, EventArgs e)
    {
        // hızlı program oluşturma sayfana yönlendir
        await Shell.Current.GoToAsync("//quickcreate");
    }

    // (İsteğe bağlı) daha şık busy animasyonu kullanmak istersen:
    private async Task WithBusy(string text, Func<Task> work)
    {
        BusyText.Text = text;
        BusyOverlay.IsVisible = true;
        await Task.WhenAll(
            BusyCard.FadeTo(1, 120),
            BusyCard.TranslateTo(0, 0, 120, Easing.CubicOut)
        );

        try { await work(); }
        finally
        {
            await Task.WhenAll(
                BusyCard.FadeTo(0, 120),
                BusyCard.TranslateTo(0, 40, 120, Easing.CubicIn)
            );
            BusyOverlay.IsVisible = false;
        }
    }

}
