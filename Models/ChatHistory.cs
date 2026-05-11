using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUANCHAMCONG.Models
{
    public class ChatHistory
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Required]
        public string Message { get; set; } = null!;

        [Required]
        public string Response { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
