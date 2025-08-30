using Microsoft.Maui.Storage;
using System.IO;

namespace DietMVP.Services
{
    public class StorageService
    {
        // Yeni: byte[] alan overload (HomePage burayı kullanıyor)
        public async Task<string> UploadMealPhotoAsync(Guid patientId, Guid mealId, byte[] bytes, string ext = "jpg", string contentType = "image/jpeg")
        {
            await Supa.InitAsync();

            ext = string.IsNullOrWhiteSpace(ext) ? "jpg" : ext.Trim('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentType))
                contentType = (ext == "png") ? "image/png" : "image/jpeg";

            var path = $"{patientId:D}/{DateTime.UtcNow:yyyy/MM/dd}/{mealId:D}_{Guid.NewGuid():N}.{ext}";
            var bucket = Supa.Client.Storage.From("meal-photos");

            await bucket.Upload(bytes, path, new Supabase.Storage.FileOptions
            {
                CacheControl = "60",             
                ContentType = contentType,
                Upsert = true
            });

            return bucket.GetPublicUrl(path);
        }

        public async Task<string> UploadMealPhotoAsync(Guid mealId, byte[] bytes, string ext = "jpg", string contentType = "image/jpeg")
        {
            await Supa.InitAsync();

            var owner = Supa.Client.Auth.CurrentUser?.Id
                        ?? throw new InvalidOperationException("Auth yok (hasta oturumu).");

            ext = string.IsNullOrWhiteSpace(ext) ? "jpg" : ext.Trim('.').ToLowerInvariant();
            contentType = string.IsNullOrWhiteSpace(contentType) ? (ext == "png" ? "image/png" : "image/jpeg") : contentType;

            // policy: path auth.uid() ile başlamalı
            var path = $"{owner}/{DateTime.UtcNow:yyyy/MM/dd}/{mealId:D}_{Guid.NewGuid():N}.{ext}";
            var bucket = Supa.Client.Storage.From("meal-photos");

            await bucket.Upload(bytes, path, new Supabase.Storage.FileOptions
            {
                CacheControl = "60",
                ContentType = contentType,
                Upsert = true
            });

            return bucket.GetPublicUrl(path);
        }
    }
}
