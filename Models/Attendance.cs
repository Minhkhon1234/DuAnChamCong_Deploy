using DUANCHAMCONG.Models;

namespace DUANCHAMCONG.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Status { get; set; } = "InvalidLocation";
        public string? SchoolName { get; set; }
        public string? SelectedShifts { get; set; }
        public string? EarlyLeaveReason { get; set; }
        
        // Anti-Spoofing fields
        public string? DeviceId { get; set; }
        public double? Accuracy { get; set; }
    }
} 
