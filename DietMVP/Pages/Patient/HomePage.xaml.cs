using System.Globalization;
using DietMVP.Models;
using DietMVP.Services;
using DietMVP.Utils;
using Microsoft.Maui.Media;

namespace DietMVP.Pages.Patient;

public partial class HomePage : ContentPage
{
    private readonly Profile _me;
    private readonly PatientHomeService _svc = new();
    private readonly StorageService _storage = new();

    private TodayVm? _today;

    private const int _cupSize = 250;
    private int _pendingWaterDelta = 0;

    private readonly CultureInfo _tr = new("tr-TR");
    private DateOnly _lastUiDate = DateOnly.FromDateTime(DateTime.Now);
    private CancellationTokenSource? _dayWatcherCts;

    // RING
    private readonly WaterRingDrawable _ring = new();

    public HomePage()
    {
        InitializeComponent();

        _me = AppSession.CurrentProfile ?? new Profile { FullName = "Hasta" };
        Header.Text = $"{_me.FullName} – Bugün";
        LblToday.Text = DateTime.Today.ToString("dddd, dd.MM.yyyy", _tr);
        LblCupInfo.Text = $"1 bardak = {_cupSize} ml";

        WaterRing.Drawable = _ring;
    }

    public HomePage(Profile me) : this() { _me = me; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateHeaderDate();
        _lastUiDate = DateOnly.FromDateTime(Clock.NowTR().DateTime);
        await LoadTodayAsync();
        StartDayWatcher();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _dayWatcherCts?.Cancel();
    }

    private async Task LoadTodayAsync()
    {
        await WithBusy("Plan yükleniyor...", async () =>
        {
            _today = await _svc.LoadTodayAsync(_me.Id);
            UpdateHeaderDate();

            MealsList.ItemsSource = _today?.Meals ?? new List<MealCardVm>();
            _pendingWaterDelta = 0;
            UpdateWaterUi();
        });

        RefreshHost.IsRefreshing = false;

        NotificationService.CancelAllPending();
        if (_today?.Meals != null)
            await NotificationService.ScheduleMealsForTodayAsync(_today.Meals, _me.Id);
        await NotificationService.ScheduleWaterAsync(new TimeOnly(9, 0), new TimeOnly(21, 0), TimeSpan.FromMinutes(90));
        NotificationService.StartMidnightRescheduler(async () =>
        {
            await LoadTodayAsync();
            NotificationService.CancelAllPending();
            if (_today?.Meals != null)
                await NotificationService.ScheduleMealsForTodayAsync(_today.Meals, _me.Id);
            await NotificationService.ScheduleWaterAsync(new TimeOnly(9, 0), new TimeOnly(21, 0), TimeSpan.FromMinutes(90));
        });
    }

    private void StartDayWatcher()
    {
        _dayWatcherCts?.Cancel();
        _dayWatcherCts = new CancellationTokenSource();
        var token = _dayWatcherCts.Token;

        Device.StartTimer(TimeSpan.FromMinutes(1), () =>
        {
            if (token.IsCancellationRequested) return false;

            var today = DateOnly.FromDateTime(Clock.NowTR().DateTime);
            if (today != _lastUiDate)
            {
                _lastUiDate = today;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    UpdateHeaderDate();
                    await LoadTodayAsync();
                    await Toast("Yeni güne geçildi. Plan güncellendi.");
                });
            }
            return true;
        });
    }

    private void UpdateWaterUi()
    {
        if (_today == null) return;

        var current = _today.WaterMl + _pendingWaterDelta;
        if (current < 0) current = 0;

        var target = Math.Max(_today.TargetMl, _cupSize);
        LblWaterSummary.Text = $"{current} / {target} ml";
        WaterProgress.Progress = Math.Min(1.0, (double)current / target);

        // RING: 0..1
        _ring.Progress = Math.Clamp((double)current / target, 0, 1);
        WaterRing.Invalidate();
    }

    private void OnCupPlus(object sender, EventArgs e)
    {
        if (_today == null) return;
        _pendingWaterDelta += _cupSize;
        UpdateWaterUi();
    }

    private void OnCupMinus(object sender, EventArgs e)
    {
        if (_today == null) return;
        if (_today.WaterMl + _pendingWaterDelta >= _cupSize)
            _pendingWaterDelta -= _cupSize;
        UpdateWaterUi();
    }

    private async void OnSaveWater(object sender, EventArgs e)
    {
        if (_today == null || _today.ProgramDayId == Guid.Empty) return;

        if (_pendingWaterDelta == 0)
        {
            await Toast("Değişiklik yok.");
            return;
        }

        if (_pendingWaterDelta < 0)
        {
            // Tablo CHECK (ml > 0), negatif log atılamaz.
            await Toast("Azaltma şimdilik desteklenmiyor.");
            _pendingWaterDelta = 0;
            UpdateWaterUi();
            return;
        }

        await WithBusy("Su kaydediliyor...", async () =>
        {
            // <- HASTA ID geri geldi
            await _svc.AddWaterAsync(_today.ProgramDayId, _me.Id, _pendingWaterDelta);
            _today.WaterMl += _pendingWaterDelta;
            _pendingWaterDelta = 0;
            UpdateWaterUi();
            await Toast("Su güncellendi.");
        });
    }


    private async void OnLogMeal(object sender, EventArgs e)
    {
        if (_today == null) return;
        var vm = (MealCardVm)((Button)sender).CommandParameter;

        try
        {
            FileResult? photo = null;
            try { photo = await MediaPicker.Default.CapturePhotoAsync(); }
            catch (FeatureNotSupportedException) { }
            if (photo == null) photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo == null) { await Toast("Fotoğraf seçilmedi."); return; }

            byte[] bytes;
            await using (var s = await photo.OpenReadAsync())
            using (var ms = new MemoryStream()) { await s.CopyToAsync(ms); bytes = ms.ToArray(); }

            await WithBusy("Öğün kaydediliyor...", async () =>
            {
                var ext = Path.GetExtension(photo.FileName)?.Trim('.').ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext)) ext = "jpg";
                var contentType = ext == "png" ? "image/png" : "image/jpeg";

                var url = await _storage.UploadMealPhotoAsync(vm.MealId, bytes, ext, contentType);
                await _svc.LogMealAsync(vm.MealId, "Eaten", url);
                vm.PhotoUrl = url;

                await Toast("Fotoğraf kaydedildi.");
                await LoadTodayAsync();
            });
        }
        catch (PermissionException) { await Toast("Kamera/galeri izni gerekli."); }
        catch (Exception ex) { await Toast($"İşlem başarısız: {ex.Message}"); }
    }

    // RefreshView
    private async void OnRefresh(object sender, EventArgs e)
    {
        await LoadTodayAsync();
    }

    // --- Şık mini busy ---
    private async Task WithBusy(string text, Func<Task> work)
    {
        Msg.IsVisible = false;
        BusyText.Text = text;

        BusyOverlay.IsVisible = true;
        await Task.WhenAll(
            BusyCard.FadeTo(1, 120),
            BusyCard.TranslateTo(0, 0, 120, Easing.CubicOut)
        );

        try { await work(); }
        catch (Exception ex)
        {
            Msg.Text = ex.Message;
            Msg.IsVisible = true;
            await Toast(ex.Message);
        }
        finally
        {
            await Task.WhenAll(
                BusyCard.FadeTo(0, 120),
                BusyCard.TranslateTo(0, 40, 120, Easing.CubicIn)
            );
            BusyOverlay.IsVisible = false;
        }
    }

    private async Task Toast(string text, int ms = 1300)
    {
        ToastText.Text = text;
        ToastHost.Opacity = 0;
        ToastHost.IsVisible = true;
        await ToastHost.FadeTo(1, 120);
        await Task.Delay(ms);
        await ToastHost.FadeTo(0, 120);
        ToastHost.IsVisible = false;
    }

    private void UpdateHeaderDate()
    {
        var nowTr = Clock.NowTR().DateTime;
        Header.Text = $"{_me.FullName} – Bugün";
        LblToday.Text = nowTr.ToString("dddd, dd.MM.yyyy", _tr);
    }
}
