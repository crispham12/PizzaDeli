using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzaDeli.Models;

public class ContactRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    // Tùy chọn — không Required
    [StringLength(100)]
    public string? OrderCode { get; set; }

    [Required]
    [StringLength(50)]
    public string IssueType { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string? Address { get; set; }

    // Waiting | Processing | Resolved
    [StringLength(20)]
    public string Status { get; set; } = "Waiting";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Khóa ngoại trỏ ngược xuống tin nhắn chi tiết
    public virtual ICollection<ContactMessage> MessagesList { get; set; } = new List<ContactMessage>();

    // FK nullable -> User (custom model, không phải IdentityUser)
    public string? UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }
}