using DUANCHAMCONG.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet("summary")]
        public IActionResult GetSummary()
        {
            var today = DateTime.Today;
            
            var allUsers = _context.Users.Where(u => u.Role == "User").ToList();
            var totalUsersCount = allUsers.Count;
            
            var attendancesToday = _context.Attendances
                .Include(a => a.User)
                .Where(a => a.CheckInTime.Date == today && a.User.Role == "User" && !a.Status.Contains("InvalidLocation"))
                .ToList();
                
            var presentList = attendancesToday.Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)a.CheckInTime, a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();
            
            var onTimeList = attendancesToday.Where(a => a.Status != null && a.Status.Contains("OnTime"))
                .Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)a.CheckInTime, a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();
                
            var lateList = attendancesToday.Where(a => a.Status != null && a.Status.Contains("Late"))
                .Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)a.CheckInTime, a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();

            var presentUserIds = attendancesToday.Select(a => a.UserId).Distinct().ToList();
            var absentList = allUsers.Where(u => !presentUserIds.Contains(u.Id))
                .Select(u => new { u.FullName, u.Email, Time = (DateTime?)null, Status = "Absent", SchoolName = (string?)null, SelectedShifts = (string?)null, EarlyLeaveReason = (string?)null }).ToList();

            var totalList = allUsers.Select(u => new { u.FullName, u.Email, Time = (DateTime?)null, Status = "User", SchoolName = (string?)null, SelectedShifts = (string?)null, EarlyLeaveReason = (string?)null }).ToList();

            var invalidAttendances = _context.Attendances
                .Include(a => a.User)
                .Where(a => a.CheckInTime.Date == today && a.User.Role == "User" && a.Status.Contains("InvalidLocation"))
                .ToList();
                
            var invalidList = invalidAttendances.Select(a => new { a.User.FullName, a.User.Email, Time = (DateTime?)a.CheckInTime, a.Status, a.SchoolName, a.SelectedShifts, a.EarlyLeaveReason }).ToList();

            return Ok(new
            {
                Summary = new {
                    TotalUsers = totalUsersCount,
                    TotalPresentToday = presentList.Count,
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
            var targetMonth = month ?? DateTime.Today.Month;
            var targetYear = year ?? DateTime.Today.Year;

            // Get attendances for the target month that are not InvalidLocation and have CheckOutTime
            var attendances = _context.Attendances
                .Where(a => a.CheckInTime.Year == targetYear && 
                            a.CheckInTime.Month == targetMonth && 
                            (a.Status == null || !a.Status.Contains("InvalidLocation")) &&
                            a.CheckOutTime != null)
                .ToList();

            var allUsers = _context.Users.Where(u => u.Role == "User").ToList();

            var result = allUsers.Select(u => {
                var userAtts = attendances.Where(a => a.UserId == u.Id).ToList();
                var totalHours = userAtts.Sum(a => (a.CheckOutTime!.Value - a.CheckInTime).TotalHours);
                
                // Get distinct days they checked in
                var totalDays = userAtts.Select(a => a.CheckInTime.Date).Distinct().Count();

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
            var targetMonth = month ?? DateTime.Today.Month;
            var targetYear = year ?? DateTime.Today.Year;

            var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
            var firstDay = new DateTime(targetYear, targetMonth, 1);
            var lastDay = firstDay.AddMonths(1).AddTicks(-1);

            var attendances = _context.Attendances
                .Where(a => a.CheckInTime >= firstDay && a.CheckInTime <= lastDay)
                .ToList();

            var users = _context.Users.Where(u => u.Role == "User").ToList();

            var result = users.Select(u => new {
                u.FullName,
                u.Email,
                DailyStatus = Enumerable.Range(1, daysInMonth).Select(day => {
                    var dayAtts = attendances.Where(a => a.UserId == u.Id && a.CheckInTime.Day == day).ToList();
                    if (!dayAtts.Any()) return "Absent";
                    
                    // Ưu tiên hiển thị trạng thái quan trọng nhất
                    if (dayAtts.Any(a => a.Status.Contains("InvalidLocation"))) return "Invalid";
                    if (dayAtts.Any(a => a.Status.Contains("Late"))) return "Late";
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
