namespace DUANCHAMCONG.Models
{
    public class User
    {
        public int Id { get; set; }

        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Role { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public bool RequestViewDetails { get; set; } = false;
        public bool CanViewDetails { get; set; } = false;
    }
}
