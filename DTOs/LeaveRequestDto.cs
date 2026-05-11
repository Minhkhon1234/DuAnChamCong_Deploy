using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace DUANCHAMCONG.DTOs
{
    public class LeaveRequestSubmitDto
    {
        [Required]
        public DateTime LeaveDate { get; set; }

        [Required]
        public string Reason { get; set; } = null!;

        public IFormFile? Image { get; set; }
    }

    public class LeaveRequestResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; } = null!;
        public DateTime LeaveDate { get; set; }
        public string Reason { get; set; } = null!;
        public string? ImagePath { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
