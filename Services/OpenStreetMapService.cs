using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DUANCHAMCONG.Services
{
    public class OpenStreetMapService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public OpenStreetMapService(HttpClient httpClient, IMemoryCache cache, IConfiguration config)
        {
            _httpClient = httpClient;
            // Nominatim requires a valid User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DUANCHAMCONG_App/1.0 (Contact: admin@hungstemroboticslab.com)");
            _cache = cache;
            _config = config;
        }

        public Task<string> GetAddressFromCoordinatesAsync(double latitude, double longitude)
        {
            // STATIC FALLBACK FOR KNOWN SCHOOLS
            // Trả về ngay lập tức địa chỉ tĩnh cho các cơ sở đã được cấu hình sẵn trong appsettings.json
            
            if (Math.Abs(latitude - 21.028228) < 0.0001 && Math.Abs(longitude - 105.803425) < 0.0001) 
                return Task.FromResult("Đại học Giao Thông Vận Tải, Láng Thượng, Đống Đa, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 21.024269) < 0.0001 && Math.Abs(longitude - 105.772156) < 0.0001) 
                return Task.FromResult("HSRL, Nam Từ Liêm, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 21.050583) < 0.0001 && Math.Abs(longitude - 105.792873) < 0.0001) 
                return Task.FromResult("Trường Everest, KĐT Nghĩa Đô, Cầu Giấy, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 21.041863) < 0.0001 && Math.Abs(longitude - 105.78762) < 0.0001) 
                return Task.FromResult("Trường Nguyễn Bỉnh Khiêm, Cầu Giấy, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 21.028901) < 0.0001 && Math.Abs(longitude - 105.821685) < 0.0001) 
                return Task.FromResult("Cơ sở Giảng Võ 1, Ba Đình, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 21.028333) < 0.0001 && Math.Abs(longitude - 105.821749) < 0.0001) 
                return Task.FromResult("Cơ sở Giảng Võ 2, Ba Đình, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 20.978744) < 0.0001 && Math.Abs(longitude - 105.793484) < 0.0001) 
                return Task.FromResult("Trường Ban Mai, KĐT Văn Quán, Hà Đông, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 21.030942) < 0.0001 && Math.Abs(longitude - 105.767573) < 0.0001) 
                return Task.FromResult("Trường Đoàn Thị Điểm CS1, Nam Từ Liêm, Hà Nội, Việt Nam");
                
            if (Math.Abs(latitude - 21.071436) < 0.0001 && Math.Abs(longitude - 105.778073) < 0.0001) 
                return Task.FromResult("Trường Đoàn Thị Điểm CS2, Bắc Từ Liêm, Hà Nội, Việt Nam");

            // Nếu không khớp với tọa độ hardcode, fallback về API BigDataCloud
            return FetchAddressFromApiAsync(latitude, longitude);
        }

        private async Task<string> FetchAddressFromApiAsync(double latitude, double longitude)
        {
            string cacheKey = $"address_{latitude}_{longitude}";

            // 1. Kiểm tra Cache
            if (_cache.TryGetValue(cacheKey, out string? cachedAddress))
            {
                return cachedAddress ?? string.Empty;
            }

            // 2. Chờ lấy Khóa (Lock) nếu Cache trống
            await _semaphore.WaitAsync();
            try
            {
                // Kiểm tra lại Cache sau khi có Khóa
                if (_cache.TryGetValue(cacheKey, out cachedAddress))
                {
                    return cachedAddress ?? string.Empty;
                }

                // 3. Gọi API BigDataCloud (Không yêu cầu API Key, Không bị chặn IP Datacenter)
                string url = FormattableString.Invariant($"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={latitude}&longitude={longitude}&localityLanguage=vi");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(jsonString);
                    var root = jsonDoc.RootElement;
                    
                    var addressParts = new System.Collections.Generic.List<string>();

                    if (root.TryGetProperty("locality", out JsonElement locality) && locality.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(locality.GetString()))
                        addressParts.Add(locality.GetString()!);
                        
                    if (root.TryGetProperty("city", out JsonElement city) && city.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(city.GetString()))
                        addressParts.Add(city.GetString()!);
                        
                    if (root.TryGetProperty("principalSubdivision", out JsonElement subdivision) && subdivision.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(subdivision.GetString()) && !addressParts.Contains(subdivision.GetString()!))
                        addressParts.Add(subdivision.GetString()!);
                        
                    if (root.TryGetProperty("countryName", out JsonElement country) && country.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(country.GetString()))
                        addressParts.Add(country.GetString()!);
                        
                    string address = addressParts.Count > 0 ? string.Join(", ", addressParts) : "Không có địa chỉ chi tiết.";
                        
                    // 4. Lưu vào Cache (Hạn sử dụng 24 giờ)
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(24));
                    _cache.Set(cacheKey, address, cacheOptions);

                    return address;
                }
                
                // Nếu API lỗi, trả về thông báo lỗi
                return "Không thể lấy địa chỉ thực tế từ tọa độ này.";
            }
            catch (Exception)
            {
                return "Lỗi khi kết nối đến dịch vụ bản đồ.";
            }
            finally
            {
                // 5. Giải phóng Khóa
                _semaphore.Release();
            }
        }
    }
}
