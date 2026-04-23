using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzaDeli.Models;

public class Review
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [MaxLength(50)]
    public string ProductId { get; set; } = string.Empty;

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    public string? Comment { get; set; }
    
    [MaxLength(1000)]
    public string? AdminReply { get; set; }

    public bool IsHidden { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
