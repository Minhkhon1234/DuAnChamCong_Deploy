using DUANCHAMCONG.Data;
using DUANCHAMCONG.DTOs;
using DUANCHAMCONG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DUANCHAMCONG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LeaveRequestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LeaveRequestController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ================= SUBMIT LEAVE REQUEST =================
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitRequest([FromForm] LeaveRequestSubmitDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);

            string? imagePath = null;
            if (dto.Image != null && dto.Image.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "leaverequests");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.Image.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.Image.CopyToAsync(stream);
                }
                imagePath = "/uploads/leaverequests/" + fileName;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(dto.Image.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("Chỉ cho phép tải lên các file ảnh (.jpg, .jpeg, .png).");
            }

             if (dto.Image.Length > 5 * 1024 * 1024) // 5MB
            {
                return BadRequest("Kích thước file ảnh không được vượt quá 5MB.");
            }

            var request = new LeaveRequest
            {
                UserId = userId,
                LeaveDate = DateTime.SpecifyKind(dto.LeaveDate, DateTimeKind.Utc),
                Reason = dto.Reason,
                ImagePath = imagePath,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.LeaveRequests.Add(request);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Gửi yêu cầu xin nghỉ thành công!" });
        }

        // ================= GET MY REQUESTS (USER) =================
        [HttpGet("my")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var requests = await _context.LeaveRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new LeaveRequestResponseDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserFullName = r.User.FullName,
                    LeaveDate = r.LeaveDate.ToLocalTime(),
                    Reason = r.Reason,
                    ImagePath = r.ImagePath,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt.ToLocalTime()
                })
                .ToListAsync();

            return Ok(requests);
        }

        // ================= GET ALL REQUESTS (ADMIN/LEADER) =================
        [HttpGet("admin")]
        [Authorize(Roles = "Leader")]
        public async Task<IActionResult> GetAllRequests()
        {
            var requests = await _context.LeaveRequests
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new LeaveRequestResponseDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserFullName = r.User.FullName,
                    LeaveDate = r.LeaveDate.ToLocalTime(),
                    Reason = r.Reason,
                    ImagePath = r.ImagePath,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt.ToLocalTime()
                })
                .ToListAsync();

            return Ok(requests);
        }

        // ================= HANDLE REQUEST (ADMIN/LEADER) =================
        [HttpPost("handle/{id}")]
        [Authorize(Roles = "Leader")]
        public async Task<IActionResult> HandleRequest(int id, [FromBody] string status)
        {
            if (status != "Approved" && status != "Rejected")
                return BadRequest("Trạng thái không hợp lệ.");

            var request = await _context.LeaveRequests.FindAsync(id);
            if (request == null) return NotFound("Không tìm thấy yêu cầu.");

            request.Status = status;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã { (status == "Approved" ? "Duyệt" : "Từ chối") } yêu cầu xin nghỉ." });
        }
    }
}
