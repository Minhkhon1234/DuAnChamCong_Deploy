using DUANCHAMCONG.Data;
using DUANCHAMCONG.DTOs;
using DUANCHAMCONG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;

namespace DUANCHAMCONG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AttendanceController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }
        // ================= GIỜ VIỆT NAM =================
        private DateTime VietnamNow =>
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
            );

        private double CalculateShiftHours(string? selectedShifts)
        {
            if (string.IsNullOrEmpty(selectedShifts)) return 0;

            double totalHours = 0;
            var shifts = selectedShifts.Split(", ");
            foreach (var shift in shifts)
            {
                try
                {
                    var parts = shift.Split("-");
                    if (parts.Length == 2)
                    {
                        var start = TimeSpan.Parse(parts[0].Trim());
                        var end = TimeSpan.Parse(parts[1].Trim());
                        totalHours += (end - start).TotalHours;
                    }
                }
                catch { } // Ignore format errors
            }
            return totalHours;
        }

        // ================= GET SCHOOLS =================
        [HttpGet("schools")]
        public IActionResult GetSchools()
        {
            var schools = _config.GetSection("Schools").Get<List<SchoolConfig>>();
            return Ok(schools ?? new List<SchoolConfig>());
        }

        // ================= CHECK-IN =================
        [HttpPost("checkin")]
        public IActionResult CheckIn(CheckInDto dto)
        {
            // var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); - Đổi sang 4 dòng dưới
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
                return Unauthorized();
            var userId = int.Parse(userIdStr);

            var today = DateTime.UtcNow.Date;

            var schools = _config.GetSection("Schools").Get<List<SchoolConfig>>();
            var school = schools?.FirstOrDefault(s => s.Id == dto.SchoolId);

            if (school == null)
            {
                return BadRequest("Cơ sở trường học không hợp lệ.");
            }

            var openShift = _context.Attendances
                .Any(x => x.UserId == userId && x.CheckInTime.Date == today && x.CheckOutTime == null && !x.Status.Contains("InvalidLocation"));

            if (openShift)
                return BadRequest("Bạn đang có một ca làm việc chưa kết thúc. Vui lòng Check-out ca cũ trước khi Check-in mới.");

            // 1. Kiểm tra chất lượng GPS
            if (dto.Accuracy.HasValue && dto.Accuracy.Value > 100)
            {
                return BadRequest($"Tín hiệu GPS quá yếu (Sai số: {Math.Round(dto.Accuracy.Value)}m). Vui lòng ra khu vực thoáng hoặc bật định vị độ chính xác cao.");
            }

            // 2. Chống Chấm công hộ (Kiểm tra DeviceId)
            if (!string.IsNullOrEmpty(dto.DeviceId))
            {
                var otherUserUsedThisDevice = _context.Attendances
                    .Any(x => x.DeviceId == dto.DeviceId && x.UserId != userId && x.CheckInTime.Date == today);
                
                if (otherUserUsedThisDevice)
                {
                    return BadRequest("THIẾT BỊ NÀY ĐÃ ĐƯỢC SỬ DỤNG CHO TÀI KHOẢN KHÁC TRONG HÔM NAY. Không được phép chấm công hộ.");
                }
            }

            // 3. Chống Fake GPS (Teleportation check)
            var lastRecordToday = _context.Attendances
                .Where(x => x.UserId == userId && x.CheckInTime.Date == today && !x.Status.Contains("InvalidLocation"))
                .OrderByDescending(x => x.CheckOutTime ?? x.CheckInTime)
                .FirstOrDefault();

            if (lastRecordToday != null)
            {
                var lastTime = lastRecordToday.CheckOutTime ?? lastRecordToday.CheckInTime;
                var timeDiffHours = (DateTime.UtcNow - lastTime).TotalHours;

                if (timeDiffHours > 0 && timeDiffHours < 24) // Nếu < 24 tiếng để tránh lỗi logic
                {
                    if (lastRecordToday.Latitude.HasValue && lastRecordToday.Longitude.HasValue)
                    {
                        var distanceKm = CalculateDistance(lastRecordToday.Latitude.Value, lastRecordToday.Longitude.Value, dto.Latitude, dto.Longitude) / 1000.0;
                        
                        // Vận tốc (km/h)
                        var speedKmH = distanceKm / timeDiffHours;

                        if (speedKmH > 100) // Nếu tốc độ lớn hơn 100km/h
                        {
                            return BadRequest($"Phát hiện di chuyển bất thường! Tốc độ trung bình {Math.Round(speedKmH)} km/h. Nghi ngờ sử dụng Fake GPS.");
                        }
                    }
                }
            }

            if (school.Latitude == null || school.Longitude == null || school.Radius == null)
            {
                return BadRequest("Cơ sở trường học này chưa được cấu hình tọa độ GPS.");
            }

            var companyLat = school.Latitude.Value;
            var companyLon = school.Longitude.Value;
            var allowedRadius = school.Radius.Value;

            var distance = CalculateDistance(dto.Latitude, dto.Longitude, companyLat, companyLon);
            string statusText;
            if (distance > allowedRadius)
            {
                statusText = "InvalidLocation";
            }
            else 
            {
                // 👉 Tính trạng thái Đúng giờ/Đi muộn dựa trên ca làm việc đầu tiên
                if (dto.SelectedShifts != null && dto.SelectedShifts.Any())
                {
                    try 
                    {
                        // Lấy giờ bắt đầu của ca sớm nhất (ví dụ: "08:00 - 09:15" -> lấy 08:00)
                        var earliestShiftTime = dto.SelectedShifts
                            .Select(s => s.Split('-')[0].Trim())
                            .Select(t => TimeSpan.Parse(t))
                            .OrderBy(t => t)
                            .First();

                        var currentTime = VietnamNow.TimeOfDay;
                        
                        // 👉 Nhân viên có thể check-in sớm bao nhiêu cũng được
                        // 👉 Trạng thái: Muộn (Late) nếu sau giờ bắt đầu ca, Đúng giờ (OnTime) nếu trước hoặc bằng giờ bắt đầu ca
                        statusText = currentTime > earliestShiftTime ? "Late" : "OnTime";
                    }
                    catch
                    {
                        // Fallback nếu có lỗi parse (không nên xảy ra)
                        statusText = VietnamNow.TimeOfDay > new TimeSpan(8, 30, 0) ? "Late" : "OnTime";
                    }
                }
                else
                {
                    // Fallback nếu không có ca (mặc định 8:30)
                    statusText = VietnamNow.TimeOfDay > new TimeSpan(8, 30, 0) ? "Late" : "OnTime";
                }
            }

            var attendance = new Attendance
            {
                UserId = userId,
                CheckInTime = DateTime.UtcNow,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Status = statusText,
                SchoolName = school.Name,
                SelectedShifts = dto.SelectedShifts != null ? string.Join(", ", dto.SelectedShifts) : null,
                DeviceId = dto.DeviceId,
                Accuracy = dto.Accuracy
            };

            _context.Attendances.Add(attendance);
            _context.SaveChanges();

            return Ok(new
            {
                message = "Check-in thành công",
                time = attendance.CheckInTime,
                status = attendance.Status
            });
        }

        // ================= CHECK-OUT =================
        [HttpPost("checkout")]
        public IActionResult CheckOut([FromBody] CheckOutDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
                return Unauthorized();
            var userId = int.Parse(userIdStr);

            var today = DateTime.UtcNow.Date;

            var schools = _config.GetSection("Schools").Get<List<SchoolConfig>>();
            var school = schools?.FirstOrDefault(s => s.Id == dto.SchoolId);

            if (school == null)
            {
                return BadRequest("Cơ sở trường học không hợp lệ.");
            }

            var attendance = _context.Attendances
                .FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.CheckInTime.Date == today &&
                    x.SchoolName == school.Name &&
                    x.CheckOutTime == null && 
                    !x.Status.Contains("InvalidLocation"));

            if (attendance == null)
                return BadRequest("Bạn không có ca làm việc nào đang mở tại cơ sở này hôm nay.");

            attendance.CheckOutTime = DateTime.UtcNow;

            // 👉 Tính giờ làm (Theo thời gian ca đã đăng ký)
            var totalHours = CalculateShiftHours(attendance.SelectedShifts);

            // 👉 Tính giờ kết thúc của ca cuối cùng đã chọn
            if (!string.IsNullOrEmpty(attendance.SelectedShifts))
            {
                try 
                {
                    // Lấy giờ kết thúc của ca muộn nhất (ví dụ: "08:00 - 09:15, 09:30 - 10:45" -> lấy 10:45)
                    var shifts = attendance.SelectedShifts.Split(", ");

                    var lastShiftEndTime = shifts
                        .Select(s => s.Split('-')[1].Trim())
                        .Select(t => TimeSpan.Parse(t))
                        .OrderByDescending(t => t)
                        .First();

                    if (VietnamNow.TimeOfDay < lastShiftEndTime)
                    {
                        if (!attendance.Status.Contains("EarlyLeave"))
                        {
                            attendance.Status += " - EarlyLeave";
                        }
                        attendance.EarlyLeaveReason = dto.Reason;
                    }
                }
                catch { /* Bỏ qua nếu lỗi parse */ }
            }
            else 
            {
                // Fallback mốc 17:00 nếu không có ca
                if (VietnamNow.TimeOfDay < new TimeSpan(17, 0, 0))
                {
                    if (!attendance.Status.Contains("EarlyLeave"))
                    {
                        attendance.Status += " - EarlyLeave";
                    }
                    attendance.EarlyLeaveReason = dto.Reason;
                }
            }

            _context.SaveChanges();

            return Ok(new
            {
                message = "Check-out thành công",
                checkOutTime = attendance.CheckOutTime,
                totalHours = Math.Round(totalHours, 2)
            });
        }

        // ================= HISTORY & SUMMARY =================
        [HttpGet("history")]
        public IActionResult GetHistory()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
                return Unauthorized();
            var userId = int.Parse(userIdStr);

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            var today = DateTime.UtcNow.Date;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddTicks(-1); //var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(1).AddTicks(-1);

            // Chỉ lấy dữ liệu trong tháng hiện tại
            var currentMonthAttendances = _context.Attendances
                .Where(x => x.UserId == userId && x.CheckInTime >= firstDayOfMonth && x.CheckInTime <= lastDayOfMonth)
                .ToList();

            var summary = new
            {
                TotalShifts = currentMonthAttendances.Count,
                OnTimeCount = currentMonthAttendances.Count(x => x.Status != null && x.Status.Contains("OnTime")),
                LateCount = currentMonthAttendances.Count(x => x.Status != null && x.Status.Contains("Late"))
            };

            // Luôn tìm ca đang mở hôm nay để UI xử lý nút Check-out
            var openRecord = currentMonthAttendances.FirstOrDefault(x => 
                x.CheckInTime.Date == today && 
                x.CheckOutTime == null && 
                !x.Status.Contains("InvalidLocation"));

            var details = new List<object>();
            // Chỉ trả về chi tiết nếu được Admin duyệt
            if (user.CanViewDetails)
            {
                details = currentMonthAttendances
                    .OrderByDescending(x => x.CheckInTime)
                    .Select(x => (object)new
                    {
                        CheckInTime = x.CheckInTime.ToLocalTime(),
                        CheckOutTime = x.CheckOutTime?.ToLocalTime(),  
                        x.Status,
                        x.SchoolName,
                        x.Latitude,
                        x.Longitude,
                        x.SelectedShifts,
                        x.EarlyLeaveReason,
                        TotalHours = CalculateShiftHours(x.SelectedShifts)
                    })
                    .ToList();
            }

            return Ok(new
            {
                Summary = summary,
                Details = details,
                TodayOpenRecord = openRecord != null ? new {
                    CheckInTime = openRecord.CheckInTime.ToLocalTime(),
                    openRecord.Status,
                    openRecord.SchoolName,
                    openRecord.SelectedShifts
                } : null,
                CanViewDetails = user.CanViewDetails,
                RequestViewDetails = user.RequestViewDetails
            });
        }

        // ================= REQUEST DETAIL VIEW =================
        [HttpPost("request-details")]
        public IActionResult RequestDetails()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr))
                return Unauthorized();
            var userId = int.Parse(userIdStr);

            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            user.RequestViewDetails = true;
            _context.SaveChanges();

            return Ok(new { message = "Yêu cầu xem chi tiết đã được gửi. Vui lòng chờ Admin phê duyệt." });
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var r = 6371e3; // Bán kính trái đất (mét)
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return r * c; // Trả về khoảng cách tính bằng mét
        }
    }
}