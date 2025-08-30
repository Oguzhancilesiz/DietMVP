
using Supabase;

namespace DietMVP
{
    public static class Supa
    {
        private static Client _client;

        // Bunları kendi projenin configinden oku istersen
        private const string Url = "https://sldijodzpxdqtdcnudqf.supabase.co";
        private const string Anon = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InNsZGlqb2R6cHhkcXRkY251ZHFmIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NTYyOTAwMzksImV4cCI6MjA3MTg2NjAzOX0.pAOMCu_5nSWLx_myLzUAwj2muwUBzsOQ84bm7YWBlB8"; // anon public key

        public static Client Client => _client ??= new Client(
            Url, Anon,
            new SupabaseOptions
            {
                AutoConnectRealtime = false,
                AutoRefreshToken = true
            });

        private static bool _inited;
        public static async Task InitAsync()
        {
            if (_inited) return;
            await Client.InitializeAsync();
            _inited = true;
        }

        // Geçici client: doktor oturumunu bozmadan SignUp için
        public static Client NewEphemeralClient()
            => new Client(
                Url, Anon,
                new SupabaseOptions
                {
                    AutoConnectRealtime = false,
                    AutoRefreshToken = false   // kalıcı oturum yok
                });

        // Kolaylık: sadece UID döndür
        public static async Task<Guid> SignUpEphemeralAsync(string email, string password)
        {
            var tmp = NewEphemeralClient();     // DİKKAT: using YOK
            await tmp.InitializeAsync();

            var signUp = await tmp.Auth.SignUp(email, password);
            var idStr = signUp?.User?.Id;
            if (!Guid.TryParse(idStr, out var uid))
                throw new InvalidOperationException("Kullanıcı oluşturuldu ama UID alınamadı.");

            // Kalıcı session tutmadığımız için ekstra temizliğe gerek yok
            return uid;
        }
    }
}
