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
    private readonly PizzaDeli.Services.AiIntegratorService _aiService;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext db, PizzaDeli.Services.AiIntegratorService aiService)
    {
        _logger = logger;
        _db = db;
        _aiService = aiService;
    }

    public IActionResult Index()
    {
        // Lấy tất cả danh mục active, kèm sản phẩm available
        var categories = _db.Categories
            .Where(c => c.IsActive)
            .Include(c => c.Products.Where(p => p.IsAvailable))
                .ThenInclude(p => p.ProductToppings)
                .ThenInclude(pt => pt.Topping)
            .Include(c => c.Products.Where(p => p.IsAvailable))
                .ThenInclude(p => p.Reviews)
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
            .Include(c => c.Products.Where(p => p.IsAvailable))
                .ThenInclude(p => p.Reviews)
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

    public IActionResult Custom()
    {
        var components = _db.PizzaComponents
            .Where(c => c.IsAvailable)
            .OrderBy(c => c.Name)
            .ToList();
        return View(components);
    }

    public IActionResult Privacy() => View();

    public IActionResult Scan() => View();
    
    public IActionResult Support() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Support(string FullName, string IssueType, string Message)
    {
        TempData["SuccessMessage"] = "Yêu cầu hỗ trợ của bạn đã được gửi. Chúng tôi sẽ phản hồi sớm nhất.";
        return RedirectToAction("Support");
    }

    [HttpPost]
    public async Task<IActionResult> SearchByImage(IFormFile imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
            return BadRequest("Vui lòng tải lên một tệp ảnh hợp lệ.");

        try
        {
            // 1. Gửi ảnh sang Python AI Service để lấy Embedding
            using var stream = imageFile.OpenReadStream();
            var queryEmbedding = await _aiService.GetImageEmbeddingAsync(stream, imageFile.FileName);

            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductToppings)
                    .ThenInclude(pt => pt.Topping)
                .Where(p => p.IsAvailable && !string.IsNullOrEmpty(p.ImageEmbedding))
                .ToListAsync();

            if (!products.Any())
                return Ok(new { success = true, info = "Hệ thống chưa có dữ liệu vector sản phẩm. Vui lòng chạy Sync API trước." });

            // 3. Tính độ tương đồng bằng Cosine Similarity
            var results = products.Select(p => {
                var dbVec = System.Text.Json.JsonSerializer.Deserialize<List<float>>(p.ImageEmbedding);
                double score = _aiService.CalculateSimilarity(queryEmbedding, dbVec);
                return new { Product = p, Score = score };
            })
            .Where(x => x.Score >= 0.80)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Select(x => new {
                id = x.Product.Id,
                name = x.Product.Name,
                price = x.Product.Price,
                imageUrl = x.Product.ImageUrl,
                score = x.Score,
                cat = x.Product.Category?.Name ?? "",
                desc = x.Product.Description ?? "",
                toppings = System.Text.Json.JsonSerializer.Serialize(
                    x.Product.ProductToppings
                        .Where(pt => pt.Topping != null && pt.Topping.IsAvailable)
                        .Select(pt => new { name = pt.Topping.Name, price = pt.Topping.Price })
                )
            })
            .ToList();

            return Ok(new { success = true, data = results });
        }
        catch (Exception ex)
        {
            _logger.LogError($"[AI Search Error] {ex.Message}");
            return StatusCode(500, new { success = false, message = "Lỗi xử lý AI Service: " + ex.Message });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
