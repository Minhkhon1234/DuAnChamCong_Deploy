using System.Security.Claims;
using DUANCHAMCONG.Data;
using DUANCHAMCONG.DTOs;
using DUANCHAMCONG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DUANCHAMCONG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetProfile()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound("Người dùng không tồn tại.");

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role
            });
        }

        [HttpPut]
        public IActionResult UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound("Người dùng không tồn tại.");

            // Kiểm tra email trùng (nếu đổi email)
            if (user.Email != dto.Email && _context.Users.Any(u => u.Email == dto.Email))
            {
                return BadRequest("Email này đã được sử dụng bởi người dùng khác.");
            }

            // Nếu đổi mật khẩu
            if (!string.IsNullOrEmpty(dto.NewPassword))
            {
                // Kiểm tra mật khẩu hiện tại
                if (string.IsNullOrEmpty(dto.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.Password))
                {
                    return BadRequest("Mật khẩu hiện tại không chính xác.");
                }
                user.Password = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            user.Email = dto.Email;
            _context.SaveChanges();

            return Ok(new { message = "Cập nhật thông tin thành công." });
        }
    }
}
