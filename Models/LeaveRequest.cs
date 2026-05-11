using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUANCHAMCONG.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Required]
        public DateTime LeaveDate { get; set; }

        [Required]
        public string Reason { get; set; } = null!;

        public string? ImagePath { get; set; }

        [Required]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
