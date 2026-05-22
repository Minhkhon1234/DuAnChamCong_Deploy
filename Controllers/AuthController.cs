using DUANCHAMCONG.Data;
using DUANCHAMCONG.DTOs;
using DUANCHAMCONG.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace DUANCHAMCONG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // REGISTER
        [HttpPost("register")]
        public IActionResult Register(RegisterDto dto)
        {
            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "User",
                IsActive = false // Đánh dấu là chưa kích hoạt, chờ Admin duyệt
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok("Đăng ký thành công. Tài khoản của bạn đang chờ Admin duyệt.");
        }

        // LOGIN
        [HttpPost("login")]
        public IActionResult Login(LoginDto dto)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == dto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
                return Unauthorized("Sai tài khoản hoặc mật khẩu");

            if (!user.IsActive)
                return Unauthorized("Tài khoản của bạn chưa được duyệt, hoặc đã bị vô hiệu hóa.");

            var token = GenerateToken(user);

            return Ok(new
            {
                token = token,
                role = user.Role,
                fullName = user.FullName
            });
        }
        private string GenerateToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        
        [Authorize(Roles = "Admin")]
        [HttpGet("admin")]
        public IActionResult Admin()
        {
            return Ok("Bạn là admin!");
        }
    
        [Authorize(Roles = "Leader")]
        [HttpGet("leader")]
        public IActionResult Leader()
        {
            return Ok("Bạn là leader!");
        }    
        
        [Authorize(Roles = "User")]
        [HttpGet("user")]   
        public IActionResult UserRole()
        {
            return Ok("Bạn là user!");
        }
    }
}