namespace DUANCHAMCONG.DTOs
{
    public class UpdateProfileDto
    {
        public string Email { get; set; } = string.Empty;
        public string? NewPassword { get; set; }
        public string? CurrentPassword { get; set; }
    }
}
