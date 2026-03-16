using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzaDeli.Models;

public class CartItem
{
    [MaxLength(50)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [MaxLength(50)]
    public string ProductId { get; set; } = string.Empty;

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    public int Quantity { get; set; } = 1;
}
