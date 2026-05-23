using DUANCHAMCONG.Data;
using DUANCHAMCONG.Models;
using DUANCHAMCONG.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DUANCHAMCONG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatbotController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly OpenStreetMapService _osmService;
        private readonly IConfiguration _config;

        public ChatbotController(ApplicationDbContext context, OpenStreetMapService osmService, IConfiguration config)
        {
            _context = context;
            _osmService = osmService;
            _config = config;
        }

        [HttpGet("schools")]
        public IActionResult GetSchools()
        {
            var schools = new List<object>();
            var schoolConfigs = _config.GetSection("Schools").GetChildren();
            
            foreach (var school in schoolConfigs)
            {
                schools.Add(new { 
                    Id = school["Id"], 
                    Name = school["Name"] 
                });
            }

            return Ok(schools);
        }

        public class LocationQueryDto
        {
            public int SchoolId { get; set; }
            public string SchoolName { get; set; } = string.Empty;
        }

        [HttpPost("location")]
        public async Task<IActionResult> GetLocation([FromBody] LocationQueryDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);

            // Find school in config
            var schoolConfigs = _config.GetSection("Schools").GetChildren();
            var school = schoolConfigs.FirstOrDefault(s => s["Id"] == dto.SchoolId.ToString());

            string userMessage = $"Cho tôi xem địa chỉ của {dto.SchoolName}";
            string aiResponse = "";

            if (school == null)
            {
                aiResponse = "Rất tiếc, tôi không tìm thấy thông tin cơ sở này trong hệ thống.";
            }
            else
            {
                double lat = double.Parse(school["Latitude"] ?? "0");
                double lon = double.Parse(school["Longitude"] ?? "0");

                string address = await _osmService.GetAddressFromCoordinatesAsync(lat, lon);
                string mapLink = $"https://www.google.com/maps/search/?api=1&query={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                aiResponse = $"📍 **Địa chỉ của cơ sở {dto.SchoolName}:**\n{address}\n\n🗺️ [Bấm vào đây để chỉ đường trên Google Maps]({mapLink})";
            }

            // Save to DB
            var chat = new ChatHistory
            {
                UserId = userId,
                Message = userMessage,
                Response = aiResponse,
                CreatedAt = DateTime.UtcNow
            };
            _context.ChatHistories.Add(chat);
            await _context.SaveChangesAsync();

            return Ok(new { response = aiResponse });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var history = await _context.ChatHistories
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Take(20)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Ok(history);
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearHistory()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var history = _context.ChatHistories.Where(c => c.UserId == userId);
            _context.ChatHistories.RemoveRange(history);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa lịch sử trò chuyện." });
        }

        [HttpPost("greeting")]
        public async Task<IActionResult> GetGreeting()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);

            string greeting = $"👋 Xin chào {user?.FullName}! Vui lòng bấm vào các gợi ý bên dưới để tra cứu địa chỉ cơ sở.";

            return Ok(new { response = greeting });
        }
    }
}
