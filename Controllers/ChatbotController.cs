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
                context += "\nLưu ý quan trọng: Khi liệt kê danh sách nhân viên đi muộn hoặc báo cáo, bạn BẮT BUỘC phải trình bày dưới dạng gạch đầu dòng (bullet points) và mỗi người một dòng riêng biệt, tuyệt đối không được viết liền nhau để dễ nhìn.\n";
            }
            else
            {
                var schools = await _context.Users.Select(u => new { u.FullName, u.Id }).Take(1).ToListAsync(); // Dummy, actually we need school configs
                // Assuming we have schools in a real scenario, let's use what we have or just mock
                context += "Dữ liệu các cơ sở làm việc:\n" +
                           "- Cơ sở Ngôi Sao Hoàng Mai: Tọa độ 20.9716, 105.8436\n" +
                           "- Cơ sở Đoàn Thị Điểm: Tọa độ 21.0375, 105.7686\n";

                var userLateCount = await _context.Attendances
                    .Where(a => a.UserId == userId && a.Status.Contains("Late") && a.CheckInTime.Month == DateTime.Now.Month)
                    .CountAsync();

                context += $"\nTrong tháng này, nhân viên này đã đi muộn {userLateCount} lần.\n";
                context += "Quy tắc trả lời bắt buộc (hãy chèn thêm vào câu trả lời của bạn):\n";
                
                if (userLateCount == 0) {
                    context += "- Hãy dành một lời khen ngợi vì nhân viên này luôn giữ vững kỷ luật, chưa đi muộn lần nào trong tháng.\n";
                } else if (userLateCount >= 1 && userLateCount < 3) {
                    context += "- Hãy nhắc nhở nhẹ nhàng nhân viên này về việc đã đi muộn vài lần.\n";
                } else if (userLateCount >= 3 && userLateCount < 5) {
                    context += "- Hãy phê bình nghiêm khắc vì nhân viên này đã đi muộn nhiều lần.\n";
                } else {
                    context += "- Hãy đưa ra lời cảnh báo gay gắt vì nhân viên này đã đi muộn quá nhiều (từ 5 lần trở lên).\n";
                }
            }

            string finalPrompt = $"{context}\nCâu hỏi của người dùng: \"{userMessage}\"\nTrả lời ngắn gọn, lịch sự, chuyên nghiệp bằng tiếng Việt. Tuân thủ tuyệt đối các quy tắc định dạng đã yêu cầu.";

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
