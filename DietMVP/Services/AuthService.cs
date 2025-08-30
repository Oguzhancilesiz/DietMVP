using DietMVP.Models;
using Microsoft.Maui.Storage;
using static Supabase.Postgrest.Constants; // Operator

namespace DietMVP.Services
{
    public class AuthService
    {
        private const string KeyAccess = "sb_access_token";
        private const string KeyRefresh = "sb_refresh_token";

        public async Task<bool> SignInAsync(string email, string pw)
        {
            try
            {
                await Supa.InitAsync();
                await Supa.Client.Auth.SignIn(email, pw);   // SDK sürümüne göre SignInWithPassword olabilir
                await SaveSessionTokensAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SignOutAsync()
        {
            try { await Supa.Client.Auth.SignOut(); } catch { /* neyse */ }
            await SecureStorage.SetAsync(KeyAccess, "");
            await SecureStorage.SetAsync(KeyRefresh, "");
        }

        /// <summary>Uygulama açılışında mevcut oturumu devam ettirmeyi dener.</summary>
        public async Task<bool> TryResumeSessionAsync()
        {
            await Supa.InitAsync();

            // Zaten geçerli oturum varsa direkt dön
            var cur = Supa.Client.Auth.CurrentSession;
            if (cur?.User != null && !string.IsNullOrWhiteSpace(cur.AccessToken))
                return true;

            // Kaydedilmiş token'ları çek
            var access = await SecureStorage.GetAsync("sb_access_token");
            var refresh = await SecureStorage.GetAsync("sb_refresh_token");
            if (string.IsNullOrWhiteSpace(refresh))
                return false;

            try
            {
                // 1) Tokenları client'a yükle (çoğu sürümde bu var)
                await Supa.Client.Auth.SetSession(access, refresh);

                // 2) Sürümünde RefreshSession parametresiz. Varsa çağır, yoksa sorun değil.
                try { await Supa.Client.Auth.RefreshSession(); } catch { /* bazı sürümlerde gerekmiyor */ }

                // 3) Yenilenen tokenları geri kaydet
                await SaveSessionTokensAsync();

                return Supa.Client.Auth.CurrentSession?.User != null;
            }
            catch
            {
                return false;
            }
        }


        public async Task<Profile?> GetCurrentProfileAsync()
        {
            await Supa.InitAsync();

            var uid = Supa.Client.Auth.CurrentSession?.User?.Id;
            if (string.IsNullOrWhiteSpace(uid) || !Guid.TryParse(uid, out var userId))
                return null;

            var resp = await Supa.Client.From<Profile>()
                .Filter("id", Operator.Equals, userId.ToString())
                .Get();

            return resp.Models.FirstOrDefault();
        }

        public async Task EnsureDoctorProfileAsync(string fullName)
        {
            await Supa.InitAsync();

            var user = Supa.Client.Auth.CurrentUser ?? throw new Exception("No auth");
            if (!Guid.TryParse(user.Id, out var userId))
                throw new Exception($"Auth UserId GUID değil: {user.Id}");

            var prof = new Profile
            {
                Id = userId,
                Role = "doctor",
                FullName = fullName,
                DailyWaterTargetMl = 2000
            };

            await Supa.Client.From<Profile>().Upsert(prof);
        }

        private async Task SaveSessionTokensAsync()
        {
            var s = Supa.Client.Auth.CurrentSession;
            if (s == null) return;
            await SecureStorage.SetAsync(KeyAccess, s.AccessToken ?? "");
            await SecureStorage.SetAsync(KeyRefresh, s.RefreshToken ?? "");
        }
    }
}
