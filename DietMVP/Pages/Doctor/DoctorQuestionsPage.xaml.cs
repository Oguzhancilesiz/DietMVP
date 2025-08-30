using System.Collections.Generic;
using System.Collections.ObjectModel;
using DietMVP.Models;
using DietMVP.Services;
using DietMVP.Utils;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Pages.Doctor;

public partial class DoctorQuestionsPage : ContentPage
{
    private readonly Profile _me; // doktor
    private readonly QnaService _svc = new();
    private CancellationTokenSource? _watcherCts;

    private string _status = "all";
    private string _query = "";
    private DateOnly _start;
    private DateOnly _end;

    public ObservableCollection<QuestionVm> Items { get; } = new();

    public DoctorQuestionsPage()
    {
        InitializeComponent();

        _me = AppSession.CurrentProfile ?? new Profile { FullName = "Doktor" };

        var today = DateOnly.FromDateTime(DateTime.Now);
        _end = today; _start = today.AddDays(-30);

        DpStart.Date = _start.ToDateTime(TimeOnly.MinValue);
        DpEnd.Date = _end.ToDateTime(TimeOnly.MinValue);

        List.ItemsSource = Items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // watcher'ý her giriþte tazeleyelim
        _watcherCts?.Cancel();
        _watcherCts = new CancellationTokenSource();

        await LoadAsync();

        _svc.StartDoctorWatcher(_me.Id, async vm =>
        {
            var id = NotificationHelper.IdFromGuid(vm.Id);
            await NotificationHelper.ShowNowAsync(id, "Yeni soru var", $"{vm.Title}\n{vm.ShortQ}");
            await LoadAsync();
        }, _watcherCts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _watcherCts?.Cancel();
    }

    private async Task LoadAsync()
    {
        // 1) Sorularý al
        var vms = await _svc.LoadForDoctorAsync(_me.Id, _start, _end, _status, _query);

        // 2) Hasta adlarýný toplu çek
        var ids = vms.Select(v => v.PatientId).Distinct().ToList();
        if (ids.Count > 0)
        {
            await Supa.InitAsync();

            // IMPORTANT: Operator.In liste ister (Guid listesi veya string listesi olur)
            var idList = ids.Select(g => g.ToString()).ToList(); // veya: ids.ToList()

            var res = await Supa.Client
                .From<Profile>()
                .Filter("id", Operator.In, idList)   // <— CSV yerine List kullan
                .Get();

            var dict = res.Models.ToDictionary(
                p => p.Id,
                p => string.IsNullOrWhiteSpace(p.FullName) ? "Hasta" : p.FullName
            );

            foreach (var vm in vms)
                vm.PatientName = dict.TryGetValue(vm.PatientId, out var name) ? name : "Hasta";
        }

        // 3) UI’ý besle
        Items.Clear();
        foreach (var q in vms) Items.Add(q);
    }


    private async void OnChip(object sender, EventArgs e)
    {
        _status = (sender as Button)?.ClassId ?? "all";
        await LoadAsync();
    }

    private async void OnDateChanged(object sender, DateChangedEventArgs e)
    {
        _start = DateOnly.FromDateTime(DpStart.Date);
        _end = DateOnly.FromDateTime(DpEnd.Date);
        if (_end < _start) _end = _start;
        await LoadAsync();
    }

    private async void OnSearch(object sender, EventArgs e)
    {
        _query = TxtQuery.Text ?? "";
        await LoadAsync();
    }

    private async void OnAnswer(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not QuestionVm vm) return;

        var editor = (sender as Button)?.Parent is Grid g
            ? g.FindByName<Editor>("EdAnswer")
            : null;

        var text = editor?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            await DisplayAlert("Uyarý", "Yanýt metni boþ olamaz.", "Tamam");
            return;
        }

        await _svc.AnswerAsync(vm.Id, text);
        await LoadAsync();
    }



    // --- Detay sayfasýna git (hasta profiline)
    private async void OnOpenDetail(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not QuestionVm vm) return;

        try
        {
            await Supa.InitAsync();
            var res = await Supa.Client.From<Profile>()
                .Where(p => p.Id == vm.PatientId)
                .Get();

            var p = res.Models.FirstOrDefault();
            if (p is null)
            {
                await DisplayAlert("Bulunamadý", "Hasta profili bulunamadý.", "Tamam");
                return;
            }

            await Navigation.PushAsync(new PatientDetailPage(p));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", ex.Message, "Tamam");
        }
    }
}
