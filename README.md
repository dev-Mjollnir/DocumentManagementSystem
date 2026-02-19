# Document Management System
**.NET 10 · Clean Architecture · EF Core Code First · Docker**

---

## Problemi Yorumlama

### Problemi Nasıl Tanımlıyorum

Kullanıcılar dokümanları bulamıyor ve bu yüzden aynı dosyayı tekrar yüklüyor. Tekrar yükleme arama sonuçlarını daha da karmaşık hale getiriyor. Sorun kendi kendini besleyen bir kısır döngüye giriyor.

Baktığımızda "arama çalışmıyor" gibi görünse de asıl sorun **sistemin kullanıcıya geri bildirim vermemesi**. Kullanıcı sonuç bulamadığında ne yapacağını bilemiyor; sistemin dokümanı görmediğini mi, yoksa gerçekten olmadığını mı anlayamıyor.

---

### Gerçek Kök Neden Ne Olabilir?

Tek bir kök neden yok. Birden fazla sorun aynı anda tetiklenmiş olabilir:

| # | Olası Kök Neden | Belirti |
|---|---|---|
| 1 | Arama yalnızca tam dosya adı eşleşmesiyle çalışıyor | `"sözleşme"` yazınca `"Sozlesme_Final_v3.pdf"` çıkmıyor |
| 2 | Dokümanlar kategori/tag olmadan yükleniyor | Filtreleme yapılamıyor, tüm sonuçlar karışık görünüyor |
| 3 | Duplicate yükleme kontrolü yok | Aynı dosyanın birden fazla kopyası indekste birikim yapıyor |
| 4 | Boş sonuçta kullanıcıya yönlendirme yok | Kullanıcı çaresiz kalıp tekrar yüklüyor |

---

### Bu Talep Yanlış Bir Varsayıma Dayanıyor Olabilir mi?

Evet, iki konuda dikkatli olmak gerekiyor:

**"Arama motoru değiştirilmeli"** varsayımı erken sonuca atlamak olur. Elasticsearch veya benzeri bir çözüm gerçekten daha iyi arama sağlar; ancak 3 ay içinde ek altyapı yatırımı yapılmayacağı kısıtı göz önünde bulundurulduğunda bu bir çözüm değil, yeni bir problemdir. Mevcut altyapının yetenekleri önce sonuna kadar kullanılmalı.

**"Kullanıcılar aramayı bilmiyor"** varsayımı da yanıltıcı olabilir. Kullanıcı eğitimi kısa vadede çalışır gibi görünse de asıl sorun sistemin toleranssız olması — kullanıcının tam dosya adını bilmesini beklemek kötü UX tasarımıdır.

---

### Hangi Bilgiler Eksik?

Çözüme başlamadan önce şu soruların yanıtlanması kararları doğrudan etkiler:

| Soru | Neden Önemli | Varsayımım |
|---|---|---|
| Mevcut arama nasıl çalışıyor? `LIKE` mi, başka bir şey mi? | Çözümün başlangıç noktasını belirliyor | `LIKE` tabanlı veya tam eşleşme olduğunu varsaydım |
| Dokümanlar yüklenirken metadata giriliyor mu? | Filtrelemenin ne kadar işe yarayacağını etkiliyor | Minimum metadata var |
| 8.000 DAU içinde kaçı aynı anda aktif? | Cache boyutu ve DB yükünü etkiliyor | ~500 eş zamanlı kullanıcı |
| Duplicate'in iş tanımı ne? Hash mi, isim mi, içerik mi? | Detection stratejisini belirliyor | SHA-256 içerik hash'i seçtim |
| Şikayetler belirli bir doküman tipine mi odaklı? | Sorunun kapsamını daraltır | Tüm tipleri etkiliyor varsaydım |
| Son 3 ayda sisteme ne kadar yeni doküman eklendi? | Büyüme hızı ölçekleme kararını etkiliyor | Bilinmiyor |

---


## Karar ve Tasarım Yaklaşımı

### Hangi Yaklaşımı Seçtim ve Neden

**Computed column + `EF.Functions.Like` + `IMemoryCache` + SHA-256 duplicate detection.**

Kısıtlar netti: mevcut veritabanı değiştirilemez, 3 ay içinde ek altyapı yatırımı yok, 400ms response süresi korunmalı. Bu çerçevede "en iyi teknik çözüm" değil, "kısıtlar içinde en bilinçli çözüm" arandı.

`SearchVector` computed column, SQL Server'ın native özelliği — yeni bir servis kurmadan, mevcut tablo üzerine stored, indexed bir kolon ekleyerek arama kalitesini artırıyor. `IMemoryCache` ile popüler sorgular cache'leniyor, 400ms hedefi korunuyor. SHA-256 hash ile duplicate tespiti, kısır döngünün kaynağını kesiyor.

---

### Bilerek Kabul Ettiğim Riskler

| Risk | Neden Kabul Ettim |
|---|---|
| `LIKE '%term%'` leading wildcard — index kullanılamaz | 8.000 DAU bu ölçekte kabul edilebilir; büyüyünce `IDocumentSearchService` interface'i üzerinden geçiş hazır |
| `IMemoryCache` multi-instance'da tutarsızlaşır | Şu an tek instance yeterli; Redis'e geçiş 1 satır DI değişikliği — kod buna hazır tasarlandı |
| Uploads container içinde — container yeniden oluşunca kaybolur | Dev ortamı için pragmatik karar; production öncesi `IFileStorageService` → Blob Storage geçişi şart |
| Türkçe karakter normalizasyonu uygulama katmanında | SQL Server collation'ına güvenmek yerine .NET tarafında kontrol altına alındı; tutarlı ama kusursuz değil |

---

### Yapmamayı Tercih Ettiğim Şeyler ve Neden

**Elasticsearch eklemedim.**
En sık akla gelen çözüm ama kısıt ihlali. Yeni altyapı = yeni ops yükü, yeni maliyet, yeni hata noktası. 3 aylık süreçte devreye almak riskli; üstelik mevcut altyapı bu ölçek için henüz yetersiz değil.

**Redis cache eklemedim.**
Tek instance için overkill. `IMemoryCache` şu anki yük için fazlasıyla yeterli. "İleride lazım olur" gerekçesiyle şimdiden eklemek gereksiz bağımlılık yaratır. Geçiş hazır, ama ihtiyaç doğmadan yapılmadı.

**Event sourcing / CQRS eklemedim.**
Bu ölçekte ve bu zaman diliminde overengineering. Mimari sadeliği korumak bilinçli bir karar.

**Kullanıcı arayüzü geliştirmedim.**
Backend API düzeltildi; mevcut frontend bu API'yi tüketebilir.

---

### MVP Kapsamını Nasıl Belirledim

Şikayetlerin kök nedenine geri döndüm ve her birini doğrudan karşılayan minimum değişiklik setini belirledim:

| Şikayet | MVP Çözümü | Kapsam Dışı Bırakılan |
|---|---|---|
| "Dokümanı bulamıyorum" | Computed column + LIKE search, kategori/tarih filtresi | ML sıralama, fuzzy matching |
| "Arama sonuçları karışık" | Boş sonuçta `suggestions` listesi, anlamlı geri bildirim | UI yeniden tasarımı |
| "Aynı dokümanı tekrar yüklüyorum" | SHA-256 duplicate detection, 409 + mevcut dosya linki | Versiyon yönetimi, diff görünümü |
| 400ms sınırı | `IMemoryCache` ile sorgu cache'leme | Redis |

MVP kriteri şuydu: **kullanıcı şikayetlerinin kökünü kesen, mevcut kısıtlar içinde çalışan, minimum değişiklik seti.** Bunun ötesine geçen her şey teknik borç listesine alındı.

---

## Mimari

```
DmsSearch.Domain          → Entity, Repository/Service interface'leri (sıfır bağımlılık)
DmsSearch.Application     → Use case handler'ları, DTO'lar, Result<T>
DmsSearch.Infrastructure  → EF Core, LIKE search, dosya depolama, migration'lar
DmsSearch.Api             → Controller, Presentation
DmsSearch.Tests           → xUnit + Moq, Application layer unit testleri
```

**Bağımlılık yönü:** `Api → Application → Domain ← Infrastructure`

---

## Mimari Diyagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Client                                 │
└───────────────────────┬─────────────────┬───────────────────────┘
                        │ GET /documents  │ POST /upload
                        ▼                 ▼
┌─────────────────────────────────────────────────────────────────┐
│  DmsSearch.Api                                                  │
│  ┌─────────────────────────┐   ┌─────────────────────────────┐  │
│  │   DocumentsController   │   │      UploadController       │  │
│  └────────────┬────────────┘   └──────────────┬──────────────┘  │
└───────────────┼────────────────────────────────┼────────────────┘
                │                                │
                ▼                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  DmsSearch.Application                                          │
│  ┌─────────────────────────┐   ┌─────────────────────────────┐  │
│  │  SearchDocumentsHandler │   │   UploadDocumentHandler     │  │
│  │  IMemoryCache · Result  │   │   SHA-256 · duplicate check │  │
│  └────────────┬────────────┘   └──────────────┬──────────────┘  │
└───────────────┼────────────────────────────────┼────────────────┘
                │                                │
                ▼                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  DmsSearch.Infrastructure                                       │
│  ┌──────────────────┐  ┌──────────────────┐  ┌───────────────┐  │
│  │ LikeSearchService│  │DocumentRepository│  │ FileStorage   │  │
│  │ EF.Functions.Like│  │ EF Core          │  │ CryptoStream  │  │
│  └────────┬─────────┘  └────────┬─────────┘  └──────┬────────┘  │
└───────────┼─────────────────────┼────────────────────┼──────────┘
            │                     │                    │
            └─────────────────────┼────────────────────┘
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│  SQL Server                                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Documents                                                │  │
│  │  ├── Id · FileName · Category · Tags · UploadedBy        │  │
│  │  ├── UploadedAt · FileSizeBytes · StoragePath             │  │
│  │  ├── FileHash ◄── IX_Documents_FileHash (filtered) [NEW] │  │
│  │  └── SearchVector ◄── computed · stored · indexed  [NEW] │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Arama Yaklaşımı

SQL Server Full-Text Search yerine **persisted computed column + `EF.Functions.Like`** kullanıldı.

```sql
SearchVector = LOWER(ISNULL(FileName,'') + ' ' + ISNULL(Category,'') + ' ' + ISNULL(Tags,''))
```

SQL Server bu kolonu her insert/update'te otomatik hesaplar. Arama, her terimi bu kolon üzerinde `LIKE '%term%'` ile sorgular (AND mantığı).

**Neden FTS değil:** `mssql-server-fts` paketi resmi SQL Server Docker image'ına kurulamıyor. Bu yaklaşım Docker dahil her ortamda çalışır, ek altyapı gerektirmez.

**Bilinen kısıt:** `LIKE '%term%'` leading wildcard nedeniyle b-tree index'ini kullanamaz, büyük hacimlerde full scan yapar. Mevcut ölçek için kabul edilebilir.

---

## Kurulum

### Docker ile (önerilen)

```bash
docker compose up --build
# API  → http://localhost:3000
# Swagger → http://localhost:3000 (root)
```

> İlk başlatmada SQL Server ~30-45 sn hazırlanır. `healthcheck` API'nin erken bağlanmasını engeller.

### Local

```bash
# appsettings.json içinde ConnectionStrings:Default değerini düzenle

cd DmsSearch.Api
dotnet ef database update --project ../DmsSearch.Infrastructure
dotnet run
```

---

## API

| Method | Endpoint | Açıklama |
|---|---|---|
| `GET` | `/api/documents` | Tüm dokümanlar (sayfalama) |
| `GET` | `/api/documents?q=sözleşme&category=Fatura&from=2024-01-01` | Arama + filtreleme |
| `POST` | `/api/upload` | Dosya yükle (`multipart/form-data`) |

**Boş sonuç:** `200` + `suggestions` listesi — kullanıcıya yönlendirme, `404` değil

**Duplicate:** `409 Conflict` + mevcut dosya bilgisi + link

---

## Tasarım Kararları

| Karar | Seçim | Gerekçe                                                                                               |
|---|---|-------------------------------------------------------------------------------------------------------|
| Arama | Computed column + LIKE | FTS Docker'da çalışmıyor, bu yaklaşım her ortamda çalışır. Olsaydı FTS tercih ederdim.                |
| Duplicate detection | SHA-256 hash (single-pass) | `CryptoStream` ile diske yazarken hash hesaplanır, double-read yok                                    |
| Cache | `IMemoryCache` | 8k DAU tek instance için yeterli olarkak görüyorum; `IDistributedCache` geçişi 1 satır DI değişikliği |
| Error handling | `Result<T>` | Duplicate beklenen iş kuralı çıktısı, exception değil                                                 |
| ORM | EF Core (raw SQL yok) | LIKE sorguları LINQ ile yazıldı                                                                       |

---

## Teknik Değerlendirme

### 1. 6 ay sonra problem çıkarabilecek noktalar
- `LIKE '%term%'` doküman hacmi büyüdükçe yavaşlar — index full scan yapar
- `IMemoryCache` restart'ta temizlenir, deployment'ta kısa yavaşlama olur
- Uploads container içinde tutulduğundan container yeniden oluşturulursa dosyalar kaybolur — production için object storage gerekir(Azure Blob, S3)

### 2. 10.000 kullanıcıda ilk kırılma noktası
`IMemoryCache` — multi-instance deployment'ta her instance kendi cache'ini tutar, tutarsız sonuçlar ve yüksek DB yükü oluşur. Çözüm: `IDistributedCache + Redis`, sadece DI kaydı değişir.

### 3. En zayıf teknik karar
`LIKE '%term%'` — fonksiyonel ama ölçeklenmiyor. Teknik borç olarak açıkça işaretlendi.

### 4. En rahatsız edici nokta
Dosyaların container içinde tutulması. Dev ortamı için pragmatik bir karar ama production'a taşımadan önce `IFileStorageService`'in blob storage implementasyonuyla değiştirilmesi şart.

---

## İletişim

### İş Birimine Açıklama

**Konu:** Doküman Yönetim Sistemi — Arama İyileştirmesi

Son dönemde sistemde doküman bulamama ve aynı dosyayı tekrar tekrar yükleme sorunlarının arttığını fark ettik. Bu şikayetlerin arkasında iki temel sorun yatıyor: mevcut arama yalnızca dosya adının tam olarak yazılmasıyla çalışıyor; bir de sistemde zamanla biriken tekrar yüklemeler arama sonuçlarını karmaşık hale getiriyor.

Artık `sözleşme` yazarak adı `Sozlesme_Final_v3.pdf` olan dosyayı bulabileceksiniz. Kategori ve tarih aralığına göre filtreleme de yapılabilecek. Arama sonucu bulunamazsa sistem size ne yapmanız gerektiğini söyleyecek.

Bir dosyayı yüklemeye çalıştığınızda sistem o dosyanın daha önce yüklenip yüklenmediğini otomatik olarak anlıyor. Eğer daha önce yüklendiyse sizi durdurup mevcut dosyanın bağlantısını veriyor — böylece sistemde aynı dosyanın birden fazla kopyası birikmiyor ve arama sonuçları temiz kalıyor.

Bu güncelleme herhangi bir kesinti olmadan devreye alındı. Ek bir maliyet ya da yeni bir sistem kurulumu söz konusu değil.

---


### Teknik Özet

**Ne yaptık:** Computed column (`SearchVector`) + `EF.Functions.Like` tabanlı arama, `IMemoryCache` ile sorgu cache'leme ve SHA-256 `CryptoStream` single-pass duplicate detection. Yeni altyapı yok, DB şeması kırılmadı, 400ms hedefi korunuyor.

**Bilinen teknik borçlar:**

1. **`LIKE '%term%'` ölçeklenmiyor.** Leading wildcard nedeniyle b-tree index'i kullanılamıyor. Doküman hacmi büyüdükçe full scan yapar. Migration path: `IDocumentSearchService` interface'i hazır, Elasticsearch veya Azure Cognitive Search implementasyonu eklenebilir.

2. **`IMemoryCache` multi-instance'da kırılır.** İkinci instance devreye girdiğinde cache tutarsızlaşır. `IDistributedCache + Redis`'e geçiş tek DI kaydı değişikliği — kod buna hazır.

3. **Uploads container içinde.** Docker volume permission sorunu nedeniyle dosyalar container'da tutuluyor. Container yeniden oluşturulursa kaybolur. Production öncesi `IFileStorageService` → Azure Blob / S3 implementasyonu şart.

4. **FTS Eksikliği.** `mssql-server-fts` resmi SQL Server Docker image'ına kurulamıyor. LIKE tabanlı yaklaşım bu kısıt içinde en pragmatik çözüm; arama kalitesi FTS'e kıyasla düşük.
5. **Dosya boyutu büyüdükçe üç ayrı sorun tetiklenir:**
    - `file.OpenReadStream()` tüm dosyayı belleğe alır — 100MB+ yüklemelerde uygulama bellek baskısına girer. Çözüm: `CopyToAsync` ile streaming pipeline, `IFormFile` yerine chunked upload olabilir.
    - `LIKE '%term%'` üzerindeki SearchVector kolonu dosya adı uzadıkça daha yavaş eşleşir. `FileName` için 500 karakter sınırı var ama `Tags` 1000 karakter — normalize edilmeden uzun tag'ler aramayı bozabilir.
    - Disk doluluk kontrolü yok. 100 kullanıcı eş zamanlı büyük dosya yüklerse `/app/uploads` dolabilir. Upload öncesi disk kullanımı kontrolü veya object storage şart.