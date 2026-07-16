# Ludora – tətbiq edilən düzəlişlər

## Təhlükəsizlik
- Games, Genres, Platforms və Companies üçün məlumat dəyişdirən endpointlər `Admin` roluna bağlandı.
- Silinmiş catalog məlumatlarını göstərən endpointlər də `Admin` roluna bağlandı.
- `Admin` rolu tətbiq başladıqda mövcud deyilsə yaradılır.
- JWT açarı `appsettings.json`-dan çıxarıldı. Açar user-secrets və ya environment variable ilə verilməlidir.
- Login və register endpointlərinə IP əsaslı rate limit əlavə edildi.
- Identity lockout aktiv edildi: 5 uğursuz cəhddən sonra 15 dəqiqə bloklama.
- JWT clock skew 1 dəqiqəyə endirildi.
- CORS origin-ləri konfiqurasiyaya çıxarıldı.

## Funksional düzəlişlər
- `UserGame` GET və dəyişiklik cavablarında local/IGDB oyunun adı, cover-i, release date-i və local slug-u doldurulur.
- `PostCount` artıq real post sayını qaytarır.
- Profil şəkli stream-i düzgün dispose edilir.
- Static file middleware əlavə edildi; şəkillər `wwwroot/uploads/profile-images` altında saxlanılır.
- Private profilin followers/following siyahısı yalnız profil sahibi və qəbul edilmiş follower tərəfindən görülür.
- Block edilmiş istifadəçilər bir-birinin followers/following siyahısına baxa bilmir.
- Silinmiş game-list üçün restore endpointi əlavə edildi: `PATCH /api/game-lists/{id}/restore`.
- Paralel unique constraint xətaları təhlükəsiz şəkildə `409 Conflict` kimi qaytarılır.
- Base entity tarixləri UTC ilə yaradılır.

## Repository təmizliyi
- Düzgün `.gitignore` əlavə edildi.
- `.git`, `.vs`, `bin`, `obj`, `*.user`, lokal upload faylları ZIP-dən çıxarıldı.
- İstifadə edilməyən AutoMapper package reference-ləri silindi.

## İşə salmazdan əvvəl
Development üçün API layihəsinin qovluğunda:

```bash
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Key" "EN_AZI_32_SIMVOLDAN_IBARET_GUCLU_RANDOM_ACAR"
```

Environment variable alternativi:

```text
JwtSettings__Key=EN_AZI_32_SIMVOLDAN_IBARET_GUCLU_RANDOM_ACAR
```

Mövcud istifadəçini admin etmək üçün database/Identity üzərindən həmin istifadəçiyə `Admin` rolu verilməlidir. Təhlükəsizlik səbəbilə layihəyə default admin parolu əlavə edilməyib.

## Qəsdən edilməyən geniş dəyişikliklər
- `GameService` və catalog servisləri davranış dəyişmədən sırf ölçüyə görə parçalanmadı.
- Bütün endpointlərin response formatı və mövcud route-ları geniş şəkildə dəyişdirilmədi.
- Pagination bütün endpointlərə məcburi əlavə edilmədi; bu frontend contract-ını geniş şəkildə dəyişdirərdi.
- Nullable PATCH modeli və slug strategiyası biznes qərarı tələb etdiyi üçün avtomatik dəyişdirilmədi.
