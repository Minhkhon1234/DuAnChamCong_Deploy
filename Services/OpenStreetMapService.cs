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
            if (_cache.TryGetValue(cacheKey, out string cachedAddress))
            {
                return cachedAddress;
            }

            // 2. Chờ lấy Khóa (Lock) nếu Cache trống
            await _semaphore.WaitAsync();
            try
            {
                // Kiểm tra lại Cache sau khi có Khóa (phòng trường hợp Thread khác đã lấy xong)
                if (_cache.TryGetValue(cacheKey, out cachedAddress))
                {
                    return cachedAddress;
                }

                // 3. Gọi API Nominatim OpenStreetMap
                // Rate limit: 1 request per second
                string url = $"https://nominatim.openstreetmap.org/reverse?lat={latitude}&lon={longitude}&format=jsonv2&accept-language=vi";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(jsonString);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("display_name", out JsonElement displayNameElement))
                    {
                        var address = displayNameElement.GetString();
                        
                        // 4. Lưu vào Cache (Hạn sử dụng 24 giờ)
                        var cacheOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromHours(24));
                        _cache.Set(cacheKey, address, cacheOptions);

                        // Đảm bảo delay an toàn chống ban IP theo policy của Nominatim
                        await Task.Delay(1500); 

                        return address;
                    }
                }
                
                // Nếu API lỗi, delay một chút để tránh spam lỗi
                await Task.Delay(2000);
                return "Không thể lấy địa chỉ thực tế từ tọa độ này.";
            }
            catch (Exception ex)
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
