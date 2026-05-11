using System.ComponentModel.DataAnnotations;

namespace DUANCHAMCONG.DTOs
{
    public class CheckOutDto
    {
        [Required(ErrorMessage = "Trường học không được để trống")]
        public int SchoolId { get; set; }
        public string? Reason { get; set; }
    }
}
