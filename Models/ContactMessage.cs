using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzaDeli.Models;

public class ContactMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ContactRequestId { get; set; }

    [ForeignKey("ContactRequestId")]
    public ContactRequest? ContactRequest { get; set; }

    // "Customer" hoặc "Staff"
    [Required]
    [StringLength(20)]
    public string Sender { get; set; } = string.Empty; 

    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
