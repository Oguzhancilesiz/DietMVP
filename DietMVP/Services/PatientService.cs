using System.Text.Json;
using DietMVP.Models;
using Supabase.Gotrue.Exceptions;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Services
{
    public class PatientService
    {
        /// <summary>
        /// E-posta + şifre ile hasta oluşturur. Zaten varsa e-postadan UID bulur ve profile'ı upsert eder.
        /// </summary>
        public async Task<Guid> CreatePatientAsync(string email, string password, string fullName, int waterTargetMl)
        {
            await Supa.InitAsync();

            try
            {
                Guid userId;

                // 1) Doktor oturumunu bozmadan kullanıcı yarat (ephemeral client)
                try
                {
                    userId = await Supa.SignUpEphemeralAsync(email, password);
                }
                catch (GotrueException ex) when (IsAlreadyExists(ex))
                {
                    // Zaten kayıtlı → UID'yi RPC ile bul
                    userId = await GetUserIdByEmailAsync(email);
                    if (userId == Guid.Empty)
                        throw new InvalidOperationException("Bu e-posta kayıtlı ama kullanıcı bulunamadı.");
                }

                // 2) Profili doktor olarak upsert et (RLS: doctor_insert_patient_profile gerekli)
                await UpsertPatientProfileAsync(userId, fullName, waterTargetMl);

                return userId;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Hasta oluşturulamadı: {ExtractNiceMessage(ex)}");
            }
        }

        private async Task UpsertPatientProfileAsync(Guid userId, string fullName, int waterTargetMl)
        {
            var prof = new Profile
            {
                Id = userId,
                Role = Roles.Patient,   // 'patient' küçük harf
                FullName = fullName,
                DailyWaterTargetMl = waterTargetMl
            };
            await Supa.Client.From<Profile>().Upsert(prof);
        }

        public async Task<List<Profile>> GetPatientsAsync()
        {
            await Supa.InitAsync();
            var q = await Supa.Client.From<Profile>()
                .Where(x => x.Role == Roles.Patient)
                .Order(x => x.FullName, Ordering.Ascending)
                .Get();
            return q.Models;
        }

        public async Task<List<Profile>> SearchPatientsAsync(string query)
        {
            await Supa.InitAsync();
            var term = (query ?? "").Trim();

            var byName = await Supa.Client.From<Profile>()
                .Where(p => p.Role == Roles.Patient)
                .Filter("full_name", Operator.ILike, $"%{term}%")
                .Order(x => x.FullName, Ordering.Ascending)
                .Get();

            var list = byName.Models;

            if (term.Contains('@'))
            {
                var id = await GetUserIdByEmailAsync(term);
                if (id != Guid.Empty)
                {
                    var prof = await GetProfileByIdAsync(id);
                    if (prof != null && list.All(p => p.Id != prof.Id))
                        list.Add(prof);
                }
            }

            return list;
        }

        public async Task<Profile?> FindByEmailAsync(string email)
        {
            var id = await GetUserIdByEmailAsync(email);
            return id == Guid.Empty ? null : await GetProfileByIdAsync(id);
        }

        private async Task<Profile?> GetProfileByIdAsync(Guid id)
        {
            await Supa.InitAsync();
            var res = await Supa.Client.From<Profile>()
                .Filter("id", Operator.Equals, id.ToString())
                .Get();
            return res.Models.FirstOrDefault();
        }

        private async Task<Guid> GetUserIdByEmailAsync(string email)
        {
            try
            {
                var payload = new Dictionary<string, object> { ["p_email"] = email };
                var resp = await Supa.Client.Rpc("get_user_id_by_email", payload);
                if (string.IsNullOrWhiteSpace(resp.Content))
                    return Guid.Empty;

                using var doc = JsonDocument.Parse(resp.Content);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                    return Guid.Empty;

                var val = root[0].GetProperty("get_user_id_by_email").GetString();
                return Guid.TryParse(val, out var id) ? id : Guid.Empty;
            }
            catch { return Guid.Empty; }
        }

        private static bool IsAlreadyExists(GotrueException ex)
        {
            var s = ((ex.Message ?? "") + " " + ex).ToLowerInvariant();
            return s.Contains("user_already_exists") || s.Contains("already registered") || s.Contains("already exists");
        }

        private static string ExtractNiceMessage(Exception ex)
        {
            var msg = ex.Message?.Trim();
            if (string.IsNullOrWhiteSpace(msg)) return "Bilinmeyen hata.";
            return msg.Replace("Supabase.Gotrue.Exceptions.GotrueException:", "").Trim();
        }
    }
}
