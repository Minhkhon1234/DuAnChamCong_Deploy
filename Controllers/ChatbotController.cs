using DUANCHAMCONG.Data;
using DUANCHAMCONG.Models;
using DUANCHAMCONG.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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
        private readonly GeminiService _geminiService;
        private readonly LocalChatbotService _localService;

        public ChatbotController(ApplicationDbContext context, GeminiService geminiService, LocalChatbotService localService)
        {
            _context = context;
            _geminiService = geminiService;
            _localService = localService;
        }

        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] string userMessage)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var role = User.FindFirstValue(ClaimTypes.Role) ?? "User";

            // Gather Context
            string context = $"Bạn là một trợ lý ảo của hệ thống quản lý nhân sự DUANCHAMCONG. Người đang hỏi là {user.FullName}, vai trò {role}.\n";
            
            if (role == "Admin" || role == "Leader")
            {
                var lateUsers = await _context.Attendances
                    .Where(a => a.Status.Contains("Late") && a.CheckInTime.Month == DateTime.Now.Month)
                    .GroupBy(a => a.User.FullName)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToListAsync();

                context += "Dữ liệu nhân viên đi muộn trong tháng này:\n";
                foreach (var item in lateUsers)
                {
                    context += $"- {item.Name}: {item.Count} lần\n";
                }
            }
            else
            {
                var schools = await _context.Users.Select(u => new { u.FullName, u.Id }).Take(1).ToListAsync(); // Dummy, actually we need school configs
                // Assuming we have schools in a real scenario, let's use what we have or just mock
                context += "Dữ liệu các cơ sở làm việc:\n" +
                           "- Cơ sở 1: 123 Đường ABC, Quận 1. Tọa độ: 10.7769, 106.7009\n" +
                           "- Cơ sở 2: 456 Đường XYZ, Quận 7. Tọa độ: 10.7289, 106.7082\n";
            }

            string finalPrompt = $"{context}\nCâu hỏi của người dùng: \"{userMessage}\"\nTrả lời ngắn gọn, lịch sự, chuyên nghiệp bằng tiếng Việt.";

            // TRY LOCAL SERVICE FIRST
            string aiResponse = await _localService.ProcessQuery(userMessage, role, user.FullName);

            // IF LOCAL SERVICE RETURNS FALLBACK, TRY GEMINI (IF CONFIGURED)
            if (aiResponse.StartsWith("Xin lỗi, tôi chưa hiểu"))
            {
                var geminiResponse = await _geminiService.GenerateResponse(finalPrompt);
                if (!geminiResponse.Contains("Lỗi"))
                {
                    aiResponse = geminiResponse;
                }
            }

            // Save to DB
            var chat = new ChatHistory
            {
                UserId = userId,
                Message = userMessage,
                Response = aiResponse,
                CreatedAt = DateTime.Now
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
    }
}
