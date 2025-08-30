using System.Collections.ObjectModel;
using System.Globalization;
using DietMVP.Models;
using DietMVP.Services;

namespace DietMVP.Pages.Doctor;

public class MealVm
{
    public Meal Meal { get; set; } = default!;
    public Guid Id => Meal.Id;
    public string Slot => Meal.Slot;
    public string SlotTr => Slot switch
    {
        "Breakfast" => "Kahvalt�",
        "Snack1" => "Ara ���n 1",
        "Lunch" => "��le",
        "Snack2" => "Ara ���n 2",
        "Dinner" => "Ak�am",
        _ => Slot
    };

    public TimeSpan StartTime { get => Meal.StartTime.ToTimeSpan(); set => Meal.StartTime = TimeOnly.FromTimeSpan(value); }
    public TimeSpan EndTime { get => Meal.EndTime.ToTimeSpan(); set => Meal.EndTime = TimeOnly.FromTimeSpan(value); }
    public string? Title { get => Meal.Title; set => Meal.Title = value; }
    public string? Note { get => Meal.Note; set => Meal.Note = value; }

    public ObservableCollection<MealItem> Items { get; set; } = new();
}

public partial class DayDetailPage : ContentPage
{
    private readonly ProgramDay _day;
    private readonly DietService _diet = new();
    private readonly MealItemService _items = new();
    private readonly ObservableCollection<MealVm> _rows = new();

    public DayDetailPage(ProgramDay day)
    {
        InitializeComponent();
        _day = day;

        var tr = new CultureInfo("tr-TR");
        DayTitle.Text = $"{day.LocalDate:dd.MM.yyyy} � {tr.DateTimeFormat.GetDayName(day.LocalDate.DayOfWeek)}";

        MealsList.ItemsSource = _rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMealsAsync();
    }

    private async Task RunBusy(string text, Func<Task> work)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BusyText.Text = text;
            BusyOverlay.IsVisible = true;
        });

        try { await work(); }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BusyOverlay.IsVisible = false;
            });
        }
    }

    private async Task ShowToast(string text, int ms = 1500)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ToastText.Text = text;
            ToastHost.Opacity = 0;
            ToastHost.IsVisible = true;
        });

        await ToastHost.FadeTo(1, 150);
        await Task.Delay(ms);
        await ToastHost.FadeTo(0, 150);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ToastHost.IsVisible = false;
        });
    }

    private async Task LoadMealsAsync()
    {
        await RunBusy("���nler y�kleniyor...", async () =>
        {
            _rows.Clear();
            var meals = await _diet.GetMealsOfDayAsync(_day.Id);
            foreach (var m in meals.OrderBy(x => x.StartTime))
            {
                var vm = new MealVm { Meal = m };
                var items = await _items.GetItemsAsync(m.Id);
                foreach (var it in items) vm.Items.Add(it);
                _rows.Add(vm);
            }
        });
    }

    private MealVm? RowFromSender(object sender) =>
        (sender as VisualElement)?.BindingContext as MealVm;

    // ���n kaydet
    private async void OnSaveMeal(object sender, EventArgs e)
    {
        Msg.IsVisible = false;
        var vm = RowFromSender(sender);
        if (vm == null) return;

        await RunBusy("���n kaydediliyor...", async () =>
        {
            await _diet.UpdateMealAsync(vm.Id, vm.Title, vm.Note, vm.Meal.StartTime, vm.Meal.EndTime);
        });
        await ShowToast($"{vm.SlotTr} kaydedildi.");
    }

    // Yemek ekle
    private async void OnAddItem(object sender, EventArgs e)
    {
        var vm = RowFromSender(sender);
        if (vm == null) return;

        var name = await DisplayPromptAsync("Yemek", "Ad�:", "Ekle", "Vazge�", maxLength: 80);
        if (string.IsNullOrWhiteSpace(name)) return;

        var qtyStr = await DisplayPromptAsync("Miktar", "�rn: 150", "Tamam", "Atla", keyboard: Keyboard.Numeric);
        decimal? qty = decimal.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var q) ? q : null;

        var unit = await DisplayPromptAsync("Birim", "gr / ml / adet", "Tamam", "Atla");
        var kcalStr = await DisplayPromptAsync("Kalori", "�rn: 200", "Tamam", "Atla", keyboard: Keyboard.Numeric);
        int? kcal = int.TryParse(kcalStr, out var k) ? k : null;

        await RunBusy("Yemek ekleniyor...", async () =>
        {
            var added = await _items.AddItemAsync(vm.Id, name.Trim(), qty, unit, kcal, null);
            MainThread.BeginInvokeOnMainThread(() => vm.Items.Insert(0, added));
        });
        await ShowToast("Yemek eklendi.");
    }

    // Yemek sil
    private async void OnDeleteItem(object sender, EventArgs e)
    {
        if (sender is SwipeItem si && si.BindingContext is MealItem item)
        {
            var ok = await DisplayAlert("Sil", $"{item.Name} silinsin mi?", "Evet", "Hay�r");
            if (!ok) return;

            await RunBusy("Siliniyor...", async () =>
            {
                await _items.DeleteItemAsync(item.Id);
                var row = _rows.FirstOrDefault(r => r.Items.Any(i => i.Id == item.Id));
                if (row != null)
                {
                    MainThread.BeginInvokeOnMainThread(() => row.Items.Remove(item));
                }
            });
            await ShowToast("Silindi.");
        }
    }

    // ���n ekle
    private async void OnAddMeal(object sender, EventArgs e)
    {
        var slot = await DisplayActionSheet("���n", "Vazge�", null, "Breakfast", "Snack1", "Lunch", "Snack2", "Dinner");
        if (string.IsNullOrEmpty(slot) || slot == "Vazge�") return;

        var sTxt = await DisplayPromptAsync("Ba�lang��", "HH:mm", initialValue: "08:00");
        var eTxt = await DisplayPromptAsync("Biti�", "HH:mm", initialValue: "09:00");

        if (!TimeSpan.TryParse(sTxt, out var ts) || !TimeSpan.TryParse(eTxt, out var te))
        {
            await ShowToast("Saat format� HH:mm olmal�.");
            return;
        }

        await RunBusy("���n ekleniyor...", async () =>
        {
            var created = await _diet.AddMealToDayAsync(_day.Id, slot, TimeOnly.FromTimeSpan(ts), TimeOnly.FromTimeSpan(te));
            var vm = new MealVm { Meal = created };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _rows.Add(vm);
                // s�ray� saatlere g�re koru
                var ordered = _rows.OrderBy(r => r.Meal.StartTime).ToList();
                _rows.Clear();
                foreach (var r in ordered) _rows.Add(r);
            });
        });
        await ShowToast("���n eklendi.");
    }

    private async void OnCloneDay(object sender, EventArgs e)
    {
        var choice = await DisplayActionSheet(
            "Bu g�n� kopyala",
            "Vazge�", null,
            "Kalan g�nlere uygula",
            "Kalan g�nlere uygula (�zerine yaz)"
        );
        if (string.IsNullOrEmpty(choice) || choice == "Vazge�") return;

        bool overwrite = choice.Contains("�zerine");
        int affected = 0;

        await RunBusy("Kopyalan�yor...", async () =>
        {
            affected = await _diet.CopyDayToRemainingAsync(_day.Id, overwrite);
        });

        await ShowToast($"Kalan {affected} g�ne kopyaland�.");
        await LoadMealsAsync();
    }
}
