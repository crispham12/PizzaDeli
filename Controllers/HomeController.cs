using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzaDeli.Models;
using PizzaDeli.Data;

namespace PizzaDeli.Controllers;

public class HomeController : BaseController
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _db;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public IActionResult Index()
    {
        // Lấy tất cả danh mục active, kèm sản phẩm available
        var categories = _db.Categories
            .Where(c => c.IsActive)
            .Include(c => c.Products.Where(p => p.IsAvailable))
                .ThenInclude(p => p.ProductToppings)
                .ThenInclude(pt => pt.Topping)
            .OrderBy(c => c.Id)
            .ToList();

        return View(categories);
    }

    public IActionResult Menu()
    {
        var categories = _db.Categories
            .Where(c => c.IsActive)
            .Include(c => c.Products.Where(p => p.IsAvailable))
                .ThenInclude(p => p.ProductToppings)
                .ThenInclude(pt => pt.Topping)
            .OrderBy(c => c.Id)
            .ToList();

        return View(categories);
    }

    public IActionResult Promotions()
    {
        var vouchers = _db.Vouchers
            .Where(v => v.IsActive)
            .OrderBy(v => v.ExpiryDate)
            .ToList();
        return View(vouchers);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
