using DUANCHAMCONG.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DUANCHAMCONG.Helpers;

namespace DUANCHAMCONG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Leader")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

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
                        // totalHours += (end - start).TotalHours;
                        var duration = end - start;
                        if (duration.TotalHours < 0)
                        {
                            duration = duration.Add(TimeSpan.FromHours(24)); // Handle overnight shifts
                        }
                        totalHours += duration.TotalHours;
                    }
                }
                catch { } // Ignore format errors
            }
            return totalHours;
        }

        [HttpGet("summary")]
        public IActionResult GetSummary()
        {
            var vietnamNow = TimeHelper.VietnamNow();
            var today = vietnamNow.Date;

            var startUtc = today.AddHours(-7);
            var endUtc = startUtc.AddDays(1);
            
            var allUsers = _context.Users.AsNoTracking().Where(u => u.Role == "User").ToList();
            var totalUsersCount = allUsers.Count;
            
            var attendancesToday = _context.Attendances
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.CheckInTime >= startUtc && a.CheckInTime < endUtc && a.User.Role == "User" && (a.Status == null || !a.Status.Contains(AttendanceStatus.InvalidLocation)))
                .ToList();
                
            var presentList = attendancesToday.Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)TimeHelper.ToVietnamTime(a.CheckInTime), a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();
            
            var onTimeList = attendancesToday.Where(a => a.Status != null && a.Status.Contains(AttendanceStatus.OnTime))
                .Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)TimeHelper.ToVietnamTime(a.CheckInTime), a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();
                
            var lateList = attendancesToday.Where(a => a.Status != null && a.Status.Contains(AttendanceStatus.Late))
                .Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)TimeHelper.ToVietnamTime(a.CheckInTime), a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();

            var presentUserIds = attendancesToday.Select(a => a.UserId).Distinct().ToList();
            var absentList = allUsers.Where(u => !presentUserIds.Contains(u.Id))
                .Select(u => new { u.FullName, u.Email, Time = (DateTime?)null, Status = "Absent", SchoolName = (string?)null, SelectedShifts = (string?)null, EarlyLeaveReason = (string?)null }).ToList();

            var totalList = allUsers.Select(u => new { u.FullName, u.Email, Time = (DateTime?)null, Status = "User", SchoolName = (string?)null, SelectedShifts = (string?)null, EarlyLeaveReason = (string?)null }).ToList();

            var invalidAttendances = _context.Attendances
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.CheckInTime >= startUtc && a.CheckInTime < endUtc && a.User.Role == "User" && a.Status != null && a.Status.Contains(AttendanceStatus.InvalidLocation))
                .ToList();
                
            var invalidList = invalidAttendances.Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)TimeHelper.ToVietnamTime(a.CheckInTime), a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();

            return Ok(new
            {
                Summary = new {
                    TotalUsers = totalUsersCount,
                    TotalPresentToday = presentUserIds.Count,
                    TotalOnTimeToday = onTimeList.Count,
                    TotalLateToday = lateList.Count,
                    TotalAbsentToday = absentList.Count,
                    TotalInvalidToday = invalidList.Count
                },
                Details = new {
                    TotalUsers = totalList,
                    Present = presentList,
                    OnTime = onTimeList,
                    Late = lateList,
                    Absent = absentList,
                    Invalid = invalidList
                }
            });
        }

        [HttpGet("monthly")]
        public IActionResult GetMonthlyStats([FromQuery] int? month, [FromQuery] int? year)
        {
            // var targetMonth = month ?? DateTime.UtcNow.Month;
            // var targetYear = year ?? DateTime.UtcNow.Year;
            var now = TimeHelper.VietnamNow();

            var targetMonth = month ?? now.Month;
            var targetYear = year ?? now.Year;

            // var attendances = _context.Attendances
            //     .Where(a => a.CheckInTime.Year == targetYear && 
            //                 a.CheckInTime.Month == targetMonth && 
            //                 (a.Status == null || a.Status == null || !a.Status.Contains(AttendanceStatus.InvalidLocation)) &&
            //                 a.CheckOutTime != null)
            //     .ToList();
            // var startMonth = DateTime.SpecifyKind(new DateTime(targetYear, targetMonth, 1), DateTimeKind.Utc);
            // var vnStart = new DateTime(targetYear, targetMonth, 1);
            // var startMonth = vnStart.AddHours(-7);
            // var endMonth = startMonth.AddMonths(1);
            var vnStart = new DateTime(targetYear, targetMonth, 1);
            var vnEnd = vnStart.AddMonths(1);

            var startMonth = TimeHelper.VietnamToUtc(vnStart);
            var endMonth = TimeHelper.VietnamToUtc(vnEnd);
            var attendances = _context.Attendances
                .AsNoTracking()
                .Where(a => a.CheckInTime >= startMonth && 
                            a.CheckInTime < endMonth && 
                            (a.Status == null || !a.Status.Contains(AttendanceStatus.InvalidLocation)) &&
                            a.CheckOutTime != null)
                .ToList();

            var allUsers = _context.Users.AsNoTracking().Where(u => u.Role == "User").ToList();

            var result = allUsers.Select(u => {
                var userAtts = attendances.Where(a => a.UserId == u.Id).ToList();
                
                // Thay vì tính CheckOut - CheckIn, tính tổng giờ của các ca đã đăng ký
                var totalHours = userAtts.Sum(a => CalculateShiftHours(a.SelectedShifts));
                
                // Get distinct days they checked in
                var totalDays = userAtts.Select(a => TimeHelper.ToVietnamTime(a.CheckInTime).Date).Distinct().Count();

                return new {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    TotalDays = totalDays,
                    TotalHours = Math.Round(totalHours, 2)
                };
            }).Where(x => x.TotalDays > 0).OrderByDescending(x => x.TotalHours).ToList();

            return Ok(result);
        }

        [HttpGet("monthly-grid")]
        public IActionResult GetMonthlyAttendanceGrid([FromQuery] int? month, [FromQuery] int? year)
        {
            // var targetMonth = month ?? DateTime.UtcNow.Month;
            // var targetYear = year ?? DateTime.UtcNow.Year;
            var now = TimeHelper.VietnamNow();

            var targetMonth = month ?? now.Month;
            var targetYear = year ?? now.Year;

            var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
            // var firstDay = new DateTime(targetYear, targetMonth, 1);
            // var firstDay = DateTime.SpecifyKind(new DateTime(targetYear, targetMonth, 1), DateTimeKind.Utc);
            // var lastDay = firstDay.AddMonths(1).AddTicks(-1);
            // var lastDay = DateTime.SpecifyKind(firstDay.AddMonths(1).AddTicks(-1), DateTimeKind.Utc);
            // var endMonth = firstDay.AddMonths(1);
            var vnStart = new DateTime(targetYear, targetMonth, 1);
            var vnEnd = vnStart.AddMonths(1);

            var firstDay = TimeHelper.VietnamToUtc(vnStart);
            var endMonth = TimeHelper.VietnamToUtc(vnEnd);

            var attendances = _context.Attendances
                .AsNoTracking()
                .Where(a => a.CheckInTime >= firstDay && a.CheckInTime < endMonth)
                .ToList();

            var users = _context.Users.AsNoTracking().Where(u => u.Role == "User").ToList();

            var result = users.Select(u => new {
                u.FullName,
                u.Email,
                DailyStatus = Enumerable.Range(1, daysInMonth).Select(day => {
                    var attendanceLookup = attendances
                        .GroupBy(a => new
                            {
                                a.UserId,
                                Date = TimeHelper.ToVietnamTime(a.CheckInTime).Date
                            })
                    .ToDictionary(g => (g.Key.UserId, g.Key.Date), g => g.ToList());
                    var dayAtts = attendances.Where(a =>
                    {
                        var vnTime = TimeHelper.ToVietnamTime(a.CheckInTime);
                        return a.UserId == u.Id && vnTime.Year == targetYear && vnTime.Month == targetMonth && vnTime.Day == day;
                    }).ToList();
                    if (!dayAtts.Any()) return "Absent";
                    
                    // Ưu tiên hiển thị trạng thái quan trọng nhất
                    if (dayAtts.Any(a => a.Status != null && a.Status.Contains(AttendanceStatus.InvalidLocation))) return "Invalid";
                    if (dayAtts.Any(a => a.Status != null && a.Status.Contains(AttendanceStatus.ForgetCheckOut))) return "ForgetCheckOut";
                    if (dayAtts.Any(a => a.Status != null && a.Status.Contains(AttendanceStatus.Late))) return "Late";
                    return "OnTime";
                }).ToList()
            }).ToList();

            return Ok(new {
                DaysInMonth = daysInMonth,
                UserAttendance = result
            });
        }
    }
}
