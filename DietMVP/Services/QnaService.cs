using DietMVP.Models;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Services;

public class QnaService
{
    public async Task AskAsync(Guid patientId, Guid doctorId, string title, string body)
    {
        await Supa.InitAsync();
        var q = new Question
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            Title = title?.Trim(),
            Body = body?.Trim(),
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };
        await Supa.Client.From<Question>().Insert(q);
    }

    public async Task AnswerAsync(Guid questionId, string answer)
    {
        await Supa.InitAsync();
        var upd = new Question
        {
            AnswerText = answer?.Trim(),
            Status = "Answered",
            AnsweredAt = DateTime.UtcNow
        };
        await Supa.Client
            .From<Question>()
            .Where(x => x.Id == questionId)
            .Set(x => x.AnswerText!, upd.AnswerText!)
            .Set(x => x.Status, upd.Status)
            .Set(x => x.AnsweredAt!, upd.AnsweredAt)
            .Update();
    }

    // Hasta listesi
    public async Task<List<QuestionVm>> LoadForPatientAsync(Guid patientId, DateOnly start, DateOnly end, string status = "all", string query = "")
    {
        await Supa.InitAsync();

        var from = start.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var to = end.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

        var req = Supa.Client.From<Question>()
            .Filter("patient_id", Operator.Equals, patientId.ToString())
            .Filter("created_at", Operator.GreaterThanOrEqual, from.ToString("o"))
            .Filter("created_at", Operator.LessThanOrEqual, to.ToString("o"))
            .Order(q => q.CreatedAt, Ordering.Descending);

        var res = await req.Get();
        var list = res.Models;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var ql = query.Trim().ToLowerInvariant();
            list = list.Where(x =>
                (x.Title ?? "").ToLowerInvariant().Contains(ql) ||
                (x.Body ?? "").ToLowerInvariant().Contains(ql) ||
                (x.AnswerText ?? "").ToLowerInvariant().Contains(ql)
            ).ToList();
        }

        if (status != "all")
            list = list.Where(x => string.Equals(x.Status, MapStatus(status), StringComparison.OrdinalIgnoreCase)).ToList();

        return list.Select(ToVm).ToList();
    }

    // Doktor listesi
    public async Task<List<QuestionVm>> LoadForDoctorAsync(Guid doctorId, DateOnly start, DateOnly end, string status = "all", string query = "")
    {
        await Supa.InitAsync();

        var from = start.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var to = end.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

        var req = Supa.Client.From<Question>()
            .Filter("doctor_id", Operator.Equals, doctorId.ToString())
            .Filter("created_at", Operator.GreaterThanOrEqual, from.ToString("o"))
            .Filter("created_at", Operator.LessThanOrEqual, to.ToString("o"))
            .Order(q => q.CreatedAt, Ordering.Descending);

        var res = await req.Get();
        var list = res.Models;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var ql = query.Trim().ToLowerInvariant();
            list = list.Where(x =>
                (x.Title ?? "").ToLowerInvariant().Contains(ql) ||
                (x.Body ?? "").ToLowerInvariant().Contains(ql) ||
                (x.AnswerText ?? "").ToLowerInvariant().Contains(ql)
            ).ToList();
        }

        if (status != "all")
            list = list.Where(x => string.Equals(x.Status, MapStatus(status), StringComparison.OrdinalIgnoreCase)).ToList();

        return list.Select(ToVm).ToList();
    }

    // Basit watcher: app açıkken 15 sn’de bir kontrol edip yeni/yanıtlananları bildir
    public void StartPatientWatcher(Guid patientId, Func<QuestionVm, Task> onAnswered, CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            DateTime last = DateTime.UtcNow.AddMinutes(-10);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Supa.InitAsync();
                    var res = await Supa.Client.From<Question>()
                        .Filter("patient_id", Operator.Equals, patientId.ToString())
                        .Filter("answered_at", Operator.GreaterThan, last.ToString("o"))
                        .Get();

                    var items = res.Models.Where(x => x.AnsweredAt != null && x.Status.Equals("Answered", StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var q in items)
                        await onAnswered(ToVm(q));

                    if (items.Count > 0)
                        last = DateTime.UtcNow;
                }
                catch { /* yut */ }

                await Task.Delay(TimeSpan.FromSeconds(15), token);
            }
        }, token);
    }

    public void StartDoctorWatcher(Guid doctorId, Func<QuestionVm, Task> onNewQuestion, CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            DateTime last = DateTime.UtcNow.AddMinutes(-10);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Supa.InitAsync();
                    var res = await Supa.Client.From<Question>()
                        .Filter("doctor_id", Operator.Equals, doctorId.ToString())
                        .Filter("created_at", Operator.GreaterThan, last.ToString("o"))
                        .Get();

                    var items = res.Models
                        .Where(x => x.Status.Equals("Open", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.CreatedAt)
                        .ToList();

                    foreach (var q in items)
                        await onNewQuestion(ToVm(q));

                    if (items.Count > 0)
                        last = DateTime.UtcNow;
                }
                catch { /* yut */ }

                await Task.Delay(TimeSpan.FromSeconds(15), token);
            }
        }, token);
    }

    private static string MapStatus(string chip) => chip switch
    {
        "answered" => "Answered",
        "open" => "Open",
        "closed" => "Closed",
        _ => chip
    };

    private static QuestionVm ToVm(Question q)
    {
        var localCreated = q.CreatedAt.ToLocalTime();
        var localAnswered = q.AnsweredAt?.ToLocalTime();

        var (badge, color) = q.Status switch
        {
            "Answered" => ("Yanıtlandı", Color.FromArgb("#10B981")),
            "Closed" => ("Kapalı", Color.FromArgb("#6B7280")),
            _ => ("Cevapsız", Color.FromArgb("#F59E0B"))
        };

        return new QuestionVm
        {
            Id = q.Id,
            PatientId = q.PatientId,
            DoctorId = q.DoctorId,
            Title = q.Title ?? "(Başlıksız)",
            Body = q.Body ?? "",
            AnswerText = q.AnswerText ?? "",
            Status = q.Status,
            StatusText = badge,
            BadgeColor = color,
            CreatedAt = localCreated,
            AnsweredAt = localAnswered
        };
    }
    /// <summary>Tek doktor varsayımı: role='Doctor' olan ilk profili döndürür.</summary>
    public async Task<Profile?> GetDefaultDoctorAsync()
    {
        await Supa.InitAsync();

        var res = await Supa.Client.From<Profile>()
            // case-insensitive
            .Filter("role", Operator.ILike, "doctor")
            .Order(p => p.FullName!, Ordering.Ascending)
            .Limit(1)
            .Get();

        return res.Models.FirstOrDefault();
    }

    /// <summary>Yalnızca ID lazımsa.</summary>
    public async Task<Guid?> GetDefaultDoctorIdAsync()
    {
        var p = await GetDefaultDoctorAsync();
        return p?.Id;
    }
}

public class QuestionVm
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string AnswerText { get; set; } = "";
    public string Status { get; set; } = "Open";

    public string StatusText { get; set; } = "Cevapsız";
    public Color BadgeColor { get; set; } = Color.FromArgb("#F59E0B");

    public DateTime CreatedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public string? PatientName { get; set; }

    // Bildirim metinleri
    public string ShortQ => (Body.Length <= 90) ? Body : Body[..90] + "…";
    public string ShortA => string.IsNullOrWhiteSpace(AnswerText)
        ? ""
        : (AnswerText.Length <= 90 ? AnswerText : AnswerText[..90] + "…");
}
