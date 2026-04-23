using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzaDeli.Models;

public class Order
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(50)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalAmount { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; } // COD, QR

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Processing, Delivering, Completed, Cancelled

    [Required]
    [MaxLength(255)]
    public string ShippingAddress { get; set; } = string.Empty;

    public int? VoucherId { get; set; }

    [ForeignKey("VoucherId")]
    public virtual Voucher? Voucher { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
