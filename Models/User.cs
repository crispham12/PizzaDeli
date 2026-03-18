using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PizzaDeli.Models;

public class User
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Avatar { get; set; }

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Customer"; // Admin, Staff, Customer

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
