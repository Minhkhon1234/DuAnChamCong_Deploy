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

        public async Task<string> GetAddressFromCoordinatesAsync(double latitude, double longitude)
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
                // Kiểm tra lại Cache sau khi có Khóa (phòng trường hợp Thread khác đã lấy xong)
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
