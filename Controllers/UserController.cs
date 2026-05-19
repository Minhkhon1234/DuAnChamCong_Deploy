using DUANCHAMCONG.Data;
using DUANCHAMCONG.DTOs;
using DUANCHAMCONG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DUANCHAMCONG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================= GET ALL USERS =================
        [HttpGet]
        public IActionResult GetAllUsers()
        {
            var users = _context.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    RequestViewDetails = u.RequestViewDetails,
                    CanViewDetails = u.CanViewDetails
                })
                .ToList();

            return Ok(users);
        }

        // ================= GET USER BY ID =================
        [HttpGet("{id}")]
        public IActionResult GetUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
                return NotFound("Không tìm thấy người dùng.");

            return Ok(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                RequestViewDetails = user.RequestViewDetails,
                CanViewDetails = user.CanViewDetails
            });
        }

        // ================= CREATE USER =================
        [HttpPost]
        public IActionResult CreateUser([FromBody] CreateUserDto dto)
        {
            if (_context.Users.Any(u => u.Email == dto.Email))
                return BadRequest("Email đã tồn tại trong hệ thống.");

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                IsActive = true
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new { message = "Thêm người dùng thành công." });
        }

        // ================= UPDATE USER =================
        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
                return NotFound("Không tìm thấy người dùng.");

            // Kiểm tra trùng Email với người khác
            if (_context.Users.Any(u => u.Email == dto.Email && u.Id != id))
                return BadRequest("Email này đã được sử dụng bởi người dùng khác.");

            user.FullName = dto.FullName;
            user.Email = dto.Email;
            user.Role = dto.Role;

            if (!string.IsNullOrEmpty(dto.Password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            _context.SaveChanges();

            return Ok(new { message = "Cập nhật người dùng thành công." });
        }

        // ================= DELETE (SOFT DELETE) USER =================
        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
                return NotFound("Không tìm thấy người dùng.");

            // Soft delete
            user.IsActive = false;
            _context.SaveChanges();

            return Ok(new { message = "Đã vô hiệu hóa (xóa) người dùng thành công." });
        }
        
        // ================= RESTORE USER =================
        [HttpPost("restore/{id}")]
        public IActionResult RestoreUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
                return NotFound("Không tìm thấy người dùng.");

            user.IsActive = true;
            _context.SaveChanges();

            return Ok(new { message = "Khôi phục tài khoản thành công." });
        }

        // ================= APPROVE/REVOKE DETAIL VIEW =================
        [HttpPost("approve-detail/{id}")]
        public IActionResult ApproveDetailView(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null) return NotFound("Không tìm thấy người dùng.");

            user.CanViewDetails = true;
            user.RequestViewDetails = false; // Reset trạng thái yêu cầu sau khi duyệt
            _context.SaveChanges();

            return Ok(new { message = "Đã cho phép người dùng xem bảng công chi tiết tháng này." });
        }

        [HttpPost("revoke-detail/{id}")]
        public IActionResult RevokeDetailView(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null) return NotFound("Không tìm thấy người dùng.");

            user.CanViewDetails = false;
            user.RequestViewDetails = false; // Reset request status too
            _context.SaveChanges();

            return Ok(new { message = "Đã khóa quyền xem bảng công chi tiết." });
        }
    }
}
