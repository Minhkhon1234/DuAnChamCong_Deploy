using System.ComponentModel.DataAnnotations;

namespace DUANCHAMCONG.DTOs
{
    public class CheckInDto
    {
        [Required(ErrorMessage = "Vĩ độ không được để trống")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Kinh độ không được để trống")]
        public double Longitude { get; set; }

        [Required(ErrorMessage = "Trường học không được để trống")]
        public int SchoolId { get; set; }

        public List<string>? SelectedShifts { get; set; }
        public string? DeviceId { get; set; }
        public double? Accuracy { get; set; }
    }
}