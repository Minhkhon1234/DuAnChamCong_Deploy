using DUANCHAMCONG.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DUANCHAMCONG.Services
{
    public class LocalChatbotService
    {
        private readonly ApplicationDbContext _context;

        public LocalChatbotService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> ProcessQuery(string userMessage, string role, string userName)
        {
            userMessage = userMessage.ToLower();

            // 1. ADMIN/LEADER INTENTS: Lateness Report
            if (role == "Admin" || role == "Leader")
            {
                if (userMessage.Contains("đi muộn") || userMessage.Contains("trễ") || userMessage.Contains("nhắc nhở"))
                {
                    var lateUsers = await _context.Attendances
                        .Where(a => a.Status.Contains("Late") && a.CheckInTime.Month == DateTime.Now.Month)
                        .GroupBy(a => a.User.FullName)
                        .Select(g => new { Name = g.Key, Count = g.Count() })
                        .OrderByDescending(g => g.Count)
                        .ToListAsync();

                    if (!lateUsers.Any()) return "Trong tháng này chưa có ghi nhận nhân viên nào đi muộn. Thật tuyệt vời!";

                    string response = "Dưới đây là danh sách nhân viên đi muộn trong tháng này:\n";
                    foreach (var item in lateUsers)
                    {
                        response += $"- {item.Name}: {item.Count} lần\n";
                    }
                    response += "\nBạn nên nhắc nhở những nhân sự này để đảm bảo tiến độ công việc.";
                    return response;
                }
            }

            // 2. USER INTENTS: Location Info
            if (userMessage.Contains("địa chỉ") || userMessage.Contains("cơ sở") || userMessage.Contains("vị trí") || userMessage.Contains("ở đâu"))
            {
                // In a real app, we would query the SchoolConfigs table. 
                // Since we use appsettings for schools, we can hardcode or read them.
                return "Hệ thống hiện có các cơ sở sau:\n" +
                       "1. Ngôi Sao Hoàng Mai: 20.9716, 105.8436\n" +
                       "2. HSRL: 21.0242, 105.7721\n" +
                       "3. Everest: 21.0464, 105.7869\n" +
                       "4. Nguyễn Bỉnh Khiêm: 21.0181, 105.8443\n" +
                       "Vui lòng đến đúng vị trí để thực hiện chấm công hợp lệ!";
            }

            // 3. COMMON INTENTS: Hello, Status
            if (userMessage.Contains("chào") || userMessage.Contains("hi") || userMessage.Contains("hello"))
            {
                return $"Xin chào {userName}! Tôi là trợ lý ảo của DUANCHAMCONG. Tôi có thể giúp gì cho bạn?";
            }

            if (userMessage.Contains("xin nghỉ"))
            {
                return "Để xin nghỉ, bạn vui lòng nhấn vào thẻ 'Xin nghỉ' trên Dashboard, nhập lý do và đính kèm ảnh minh chứng nhé.";
            }

            // 4. FALLBACK
            return "Xin lỗi, tôi chưa hiểu ý của bạn. Bạn có thể hỏi về 'danh sách đi muộn' (nếu là Admin) hoặc 'vị trí các cơ sở' (nếu là Nhân viên) được không?";
        }
    }
}
