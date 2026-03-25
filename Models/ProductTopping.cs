using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzaDeli.Models;

public class ProductTopping
{
    [MaxLength(50)]
    public string ProductId { get; set; } = string.Empty;

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    [MaxLength(50)]
    public string ToppingId { get; set; } = string.Empty;

    [ForeignKey("ToppingId")]
    public virtual Topping? Topping { get; set; }
}
