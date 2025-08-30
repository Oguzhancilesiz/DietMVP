ESTEDİET · Doktor-Hasta Beslenme Takip Uygulaması

Kişiye özel beslenme programı atayan doktorlar ve günlük kayıt tutan hastalar için mobil uygulama.
Teknoloji: .NET MAUI (iOS/Android) + ASP.NET Core 9 API + EF Core + Supabase/PostgreSQL + JWT.

Kısa özet: Doktor için zahmetsiz program atama, hasta için net günlük plan

İçindekiler

Özellikler

Ekran Görüntüleri

Mimari

Teknoloji Yığını

Dizin Yapısı

Kurulum

Önkoşullar

API’yi Çalıştırma

Mobil (MAUI) Uygulamayı Çalıştırma

Ortam Değişkenleri

Temel Uç Noktalar

Veri Modeli (özet)

Yol Haritası

Katkı ve Geliştirme

Lisans

Özellikler

Doktor

Kişi yönetimi: hasta ekleme, filtreleme, arama

Program atama: başlangıç/bitiş, kalan gün sayacı, ilerleme yüzdesi

Haftalık özet ve rapor görünümü

Hastadan gelen soruları yanıtlama

Hasta

Günlük öğün planı ve durum işaretleme (tamamlandı/atlanıldı)

Not ve fotoğraf ile kayıt tutma

Doktora soru gönderme

Bildirim/hatırlatmalar, basit offline kullanım (son plan önbelleği)

Genel/Altyapı

JWT tabanlı kimlik doğrulama ve rol yönetimi (Doktor/Hasta)

Katmanlı mimari, servis + repository

EF Core ile PostgreSQL/Supabase

Temiz UI/UX, MVVM (MAUI)

Ekran Görüntüleri

/docs/screenshots/ içine koyun:

01-login.png

02-doctor-dashboard.png

03-patient-plan.png

04-program-detail.png

05-weekly-summary.png




![Login](docs/screenshots/01-login.png)

[ MAUI Mobile ]  <—REST—>  [ ASP.NET Core 9 API ]  <—EF Core—>  [ PostgreSQL / Supabase ]
       MVVM                         JWT, Roles                       SQL, Storage


esteddiet/
├─ api/
│  ├─ src/
│  │  ├─ Estediet.Api                  # Web API
│  │  ├─ Estediet.Application          # Services, DTOs, Validators
│  │  ├─ Estediet.Domain               # Entities, Enums
│  │  └─ Estediet.Infrastructure       # EF Core, Repositories, Migrations
│  └─ tests/                           # (opsiyonel) API testleri
├─ mobile/
│  ├─ DietMVP/                         # MAUI proje
│  ├─ Resources/                       # Görseller, stiller
│  └─ docs/                            # Mobil özel notlar
├─ docs/
│  ├─ screenshots/
│  └─ architecture/
└─ README.md


Önkoşullar

.NET 9 SDK

MAUI workload: dotnet workload install maui

Android SDK/Emülatör veya fiziksel cihaz, iOS için Xcode kurulu bir macOS (derleme/iç dağıtım)

PostgreSQL 14+ (lokalde) veya Supabase hesabı





git clone https://github.com/<kullanici>/<repo>.git
cd esteddiet/api

# Bağımlılıklar
dotnet restore

# appsettings.Development.json örneği oluşturun
cp src/Estediet.Api/appsettings.Development.sample.json \
   src/Estediet.Api/appsettings.Development.json

# Veritabanı migrasyonları
dotnet ef database update --project src/Estediet.Infrastructure --startup-project src/Estediet.Api

# Çalıştır
dotnet run --project src/Estediet.Api


{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=esteddiet;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Issuer": "Estediet",
    "Audience": "EstedietClient",
    "Key": "CHANGE_ME_TO_A_LONG_RANDOM_KEY",
    "AccessTokenMinutes": 120
  },
  "Supabase": {
    "Url": "https://YOUR-PROJECT.supabase.co",
    "AnonKey": "YOUR-ANON-KEY"
  },
  "Logging": {
    "LogLevel": { "Default": "Information" }
  }
}
cd ../mobile/DietMVP

# API adresini ayarlayın (örn. Env.cs veya Secrets)
# public const string ApiBaseUrl = "http://10.0.2.2:5000"; // Android emülatör için
# public const string ApiBaseUrl = "http://<LAN-IP>:5000"; // Fiziksel cihaz için

dotnet restore

# Android
dotnet build -t:Run -f net9.0-android

# iOS (macOS üzerinde)
dotnet build -t:Run -f net9.0-ios
| Anahtar                                                               | Nerede | Açıklama                      |
| --------------------------------------------------------------------- | ------ | ----------------------------- |
| `ConnectionStrings__DefaultConnection`                                | API    | PostgreSQL bağlantısı         |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key`, `Jwt__AccessTokenMinutes` | API    | JWT ayarları                  |
| `Supabase__Url`, `Supabase__AnonKey`                                  | API    | Opsiyonel Supabase servisleri |
| `API_BASE_URL`                                                        | Mobil  | API temel adresi              |
| `SUPABASE_URL`, `SUPABASE_KEY`                                        | Mobil  | Kullanılıyorsa                |


POST /api/auth/register        # Doktor/Hasta kayıt
POST /api/auth/login           # JWT al
GET  /api/users/me             # Profil

# Doktor
GET  /api/patients
POST /api/programs             # Program atama
GET  /api/programs/{id}        # Detay
GET  /api/reports/weekly       # Haftalık özet

# Hasta
GET  /api/plan/today
POST /api/meals/{scheduledId}/log   # Öğün durum/nota/foto kaydı
POST /api/questions                  # Doktora soru
Veri Modeli (özet)

AppUser (roller: Doctor, Patient)

PatientProfile, DoctorProfile

Program → ProgramDay → ScheduledMeal

MealLog (status, note, photoUrl, completedAt)

Question (hasta → doktor)

Media (opsiyonel)
Tüm varlıklarda yumuşak silme için Status alanı tercih edilir.

Yol Haritası

 Bildirim/hatırlatmaların OS seviyesinde planlanması

 Program kopyalama/şablonlama

 Gelişmiş offline senaryolar

 Çoklu dil desteği (tr/en)

 Testler (API ve MVVM unit tests)

 Basit analitik/raporlama ekranları

 # .github/workflows/api-ci.yml
name: API CI
on:
  push:
    paths: ['api/**']
  pull_request:
    paths: ['api/**']
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore api/src/Estediet.Api
      - run: dotnet build api/src/Estediet.Api -c Release --no-restore
