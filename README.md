# Book Recommendation API

Bu proje, ASP.NET Core Web API üzerinde Redis cache ve MSSQL kullanarak gerçekçi bir kitap öneri sistemi senaryosunu gösterir.

## Özellikler

- GET /api/books/{id}  
  Redis ile desteklenen tek kitap sorgusu  
- GET /api/books  
  Tüm kitapları listeleyen endpoint (cache’li)  
- POST /api/books  
  Yeni kitap ekler, ilgili cache’i temizler  
- PUT /api/books/{id}  
  Kitap günceller, hem tekil hem liste cache’ini temizler  
- DELETE /api/books/{id}  
  Kitap siler, cache temizleme  
- GET /api/books/search?title=&author=  
  Başlık ve/veya yazara göre filtreli arama (cache’li)  
- GET /api/books/popular?count=N  
  En çok okunan N kitabı döner (Redis Sorted Set)

## Gereksinimler

- .NET 8 SDK  
- Docker & Docker Compose  
- MSSQL ve Redis (Docker ile sağlanıyor)  
- (Opsiyonel) Node.js & autocannon – performans testi için

## Başlarken

1) Repoyu klonlayın ve dizine girin  
   ```bash
   git clone <repo-url>
   cd "Book Recommendation API/Book Recommendation API"
   ```

2) Docker Compose ile MSSQL ve Redis’i ayağa kaldırın  
   ```bash
   docker compose up -d
   ```

3) `appsettings.json`’ı güncelleyin  
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost,1433;Database=BookDb;User Id=sa;Password=Your_Password123;TrustServerCertificate=True;"
     },
     "RedisConnection": "localhost:6379",
     "Logging": { … },
     "AllowedHosts": "*"
   }
   ```

4) EF Core Migration’ları uygula  
   ```bash
   dotnet ef database update
   ```

5) API’yi çalıştır  
   ```bash
   dotnet run
   ```

6) Swagger UI (doc)  
   `https://localhost:7215/swagger/index.html` adresinden endpoint’leri deneyin.

## Performans Testi (autocannon)

```bash
npm install -g autocannon
# Cache’i temizle
curl -s -X DELETE https://localhost:7215/api/books/1
# Uncached
autocannon -c 50 -d 10 "https://localhost:7215/api/books/1"
# Cache’i ısıt
curl -s https://localhost:7215/api/books/1 > nul
# Cached
autocannon -c 50 -d 10 "https://localhost:7215/api/books/1"
```

## Lisans

MIT © 2025
