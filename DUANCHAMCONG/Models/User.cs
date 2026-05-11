namespace AttendanceSystem.Models
{
    public class User
    {
        public int Id { get; set; }

        public required string FullName { get; set; }

        public required string Email { get; set; }

        public required string Password { get; set; }

        // Admin / Employee
        public required string Role { get; set; }
    }
}
