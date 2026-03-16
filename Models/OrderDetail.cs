using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzaDeli.Models;

public class OrderDetail
{
    [MaxLength(50)]
    public string OrderId { get; set; } = string.Empty;

    [ForeignKey("OrderId")]
    public virtual Order? Order { get; set; }

    [MaxLength(50)]
    public string ProductId { get; set; } = string.Empty;

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }
}
