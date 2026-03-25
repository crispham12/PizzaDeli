using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;
using System.IO;

namespace PizzaDeli.Controllers;

/// <summary>Admin: Thống kê, Quản lý sản phẩm/đơn hàng/danh mục/tài khoản/nhân viên/khuyến mãi</summary>
public class AdminController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AdminController(ApplicationDbContext db, IWebHostEnvironment webHostEnvironment)
    {
        _db = db;
        _webHostEnvironment = webHostEnvironment;
    }

    // Guard tất cả action
    private IActionResult? Guard() => RequireRole("Admin");

    // ---- Dashboard / Thống kê ----
    public async Task<IActionResult> Dashboard()
    {
        var g = Guard(); if (g != null) return g;

        var today = DateTime.Today;
        var thisMonth = new DateTime(today.Year, today.Month, 1);
        var now = DateTime.Now;

        // Stat cards
        ViewBag.TotalOrders   = await _db.Orders.CountAsync();
        ViewBag.RevenueToday  = await _db.Orders
            .Where(o => o.OrderDate.Date == today && o.Status == "Completed")
            .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
        ViewBag.TotalProducts = await _db.Products.CountAsync();
        ViewBag.TotalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer);

        // Tổng quan - Đơn hàng tháng này
        ViewBag.OrdersThisMonth = await _db.Orders
            .CountAsync(o => o.OrderDate >= thisMonth);

        // Doanh thu tháng này
        ViewBag.RevenueThisMonth = await _db.Orders
            .Where(o => o.OrderDate >= thisMonth && o.Status == "Completed")
            .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;

        // Trạng thái đơn hàng
        ViewBag.OrdersPending   = await _db.Orders.CountAsync(o => o.Status == "Pending");
        ViewBag.OrdersDelivering = await _db.Orders.CountAsync(o => o.Status == "Delivering");
        ViewBag.OrdersCompleted = await _db.Orders.CountAsync(o => o.Status == "Completed");

        // Sản phẩm theo danh mục
        ViewBag.Categories = await _db.Categories
            .Where(c => c.IsActive)
            .Select(c => new { c.Name, Count = c.Products.Count })
            .Take(3)
            .ToListAsync();

        // Nhân viên theo role  
        ViewBag.AdminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin);
        ViewBag.StaffCount = await _db.Users.CountAsync(u => u.Role == UserRole.Staff);

        // Voucher đang chạy
        ViewBag.ActiveVouchers = await _db.Vouchers
            .Where(v => v.IsActive && (v.ExpiryDate == null || v.ExpiryDate >= now))
            .OrderByDescending(v => v.Id)
            .ToListAsync();
        // Giữ lại ActiveVoucher để tương thích ngược (lấy cái đầu tiên)
        ViewBag.ActiveVoucher = (ViewBag.ActiveVouchers as IEnumerable<PizzaDeli.Models.Voucher>)?.FirstOrDefault();

        return View();
    }

    // ---- Thống kê ----
    public async Task<IActionResult> Statistics()
    {
        var g = Guard(); if (g != null) return g;

        var today = DateTime.Today;
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd   = thisMonthStart; // exclusive

        // Hàm tính % thay đổi
        static decimal CalcPct(decimal current, decimal prev)
            => prev == 0 ? (current > 0 ? 100m : 0m) : Math.Round((current - prev) / prev * 100, 1);

        // ---- Doanh thu ----
        decimal revThis = await _db.Orders
            .Where(o => o.Status == "Completed" && o.OrderDate >= thisMonthStart)
            .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
        decimal revLast = await _db.Orders
            .Where(o => o.Status == "Completed" && o.OrderDate >= lastMonthStart && o.OrderDate < lastMonthEnd)
            .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
        decimal revTotal = await _db.Orders
            .Where(o => o.Status == "Completed")
            .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
        ViewBag.StatsRevenue    = revTotal;
        ViewBag.StatRevenuePct  = CalcPct(revThis, revLast);

        // ---- Đơn hàng ----
        int ordThis  = await _db.Orders.CountAsync(o => o.OrderDate >= thisMonthStart);
        int ordLast  = await _db.Orders.CountAsync(o => o.OrderDate >= lastMonthStart && o.OrderDate < lastMonthEnd);
        int ordTotal = await _db.Orders.CountAsync();
        ViewBag.StatsOrders    = ordTotal;
        ViewBag.StatOrdersPct  = CalcPct(ordThis, ordLast);

        // ---- AOV (Giá trị TB đơn) ----
        decimal aovThis = ordThis > 0
            ? (await _db.Orders.Where(o => o.Status == "Completed" && o.OrderDate >= thisMonthStart).SumAsync(o => (decimal?)o.FinalAmount) ?? 0) / ordThis
            : 0;
        decimal aovLast = ordLast > 0
            ? revLast / ordLast
            : 0;
        ViewBag.StatsAOV    = ordTotal > 0 ? (revTotal / ordTotal) : 0;
        ViewBag.StatAOVPct  = CalcPct(aovThis, aovLast);

        // ---- Khách hàng ----
        int cusThis  = await _db.Users.CountAsync(u => u.Role == UserRole.Customer && u.CreatedAt >= thisMonthStart);
        int cusLast  = await _db.Users.CountAsync(u => u.Role == UserRole.Customer && u.CreatedAt >= lastMonthStart && u.CreatedAt < lastMonthEnd);
        int cusTotal = await _db.Users.CountAsync(u => u.Role == UserRole.Customer);
        ViewBag.StatsCustomers   = cusTotal;
        ViewBag.StatCustomersPct = CalcPct(cusThis, cusLast);

        // Doanh thu 7 ngày qua (cho line chart)
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();
        var revenueRaw = await _db.Orders
            .Where(o => o.Status == "Completed" && o.OrderDate.Date >= today.AddDays(-6))
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(o => o.FinalAmount) })
            .ToListAsync();
        var revenueByDay = last7Days.Select(d => new {
            Label = d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "Thứ " + ((int)d.DayOfWeek + 1),
            Value = revenueRaw.FirstOrDefault(r => r.Date == d)?.Total ?? 0
        }).ToList();
        ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(revenueByDay.Select(r => r.Label).ToList());
        ViewBag.ChartValues = System.Text.Json.JsonSerializer.Serialize(revenueByDay.Select(r => r.Value).ToList());

        // Doanh thu theo danh mục (donut chart)
        var catRevenue = await _db.OrderDetails
            .Where(od => od.Order!.Status == "Completed")
            .GroupBy(od => od.Product!.Category!.Name)
            .Select(g => new { Name = g.Key ?? "Khác", Total = g.Sum(od => od.UnitPrice * od.Quantity) })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToListAsync();
        ViewBag.DonutLabels = System.Text.Json.JsonSerializer.Serialize(catRevenue.Select(c => c.Name).ToList());
        ViewBag.DonutValues = System.Text.Json.JsonSerializer.Serialize(catRevenue.Select(c => c.Total).ToList());

        // Sản phẩm bán chạy
        ViewBag.TopProducts = await _db.OrderDetails
            .Where(od => od.Order!.Status == "Completed")
            .GroupBy(od => new { 
                od.ProductId, 
                ProductName = od.Product!.Name, 
                od.Product.ImageUrl, 
                CategoryName = od.Product.Category!.Name 
            })
            .Select(g => new {
                g.Key.ProductId,
                Name     = g.Key.ProductName,
                g.Key.ImageUrl,
                Category = g.Key.CategoryName,
                Sold     = g.Sum(x => x.Quantity),
                Revenue  = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.Sold)
            .Take(5)
            .ToListAsync();

        return View();
    }


    // ---- Quản lý sản phẩm (CRUD) ----
    
    // 1. Xem danh sách sản phẩm
    public async Task<IActionResult> Products(string searchQuery = "", int? categoryId = null, int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        var query = _db.Products
            .Include(p => p.Category)
            .Where(p => p.Category != null && p.Category.IsActive)  // Ẩn sản phẩm thuộc danh mục inactive
            .AsQueryable();

        // Lọc theo từ khóa
        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
            ViewBag.SearchQuery = searchQuery;
        }

        // Lọc theo danh mục
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
            ViewBag.CurrentCategory = categoryId.Value;
        }

        // Phân trang đơn giản
        int pageSize = 10;
        int totalProducts = await query.CountAsync();
        var products = await query.OrderByDescending(p => p.CreatedAt)
                                  .Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();

        ViewBag.TotalProducts = totalProducts;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);
        // Chỉ lấy danh mục active cho tabs lọc
        ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).ToListAsync();

        return View(products);
    }

    // 1.1 Xem danh sách Topping (dùng bảng Topping mới)
    public async Task<IActionResult> Toppings(string searchQuery = "", string status = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        var query = _db.Toppings.AsQueryable();

        // Lọc theo từ khóa
        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
            ViewBag.SearchQuery = searchQuery;
        }

        // Lọc theo trạng thái
        if (status == "active") query = query.Where(p => p.IsAvailable);
        else if (status == "inactive") query = query.Where(p => !p.IsAvailable);
        ViewBag.CurrentStatus = status;

        // Phân trang đơn giản
        int pageSize = 10;
        int totalProducts = await query.CountAsync();
        var products = await query.OrderByDescending(p => p.CreatedAt)
                                  .Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();

        ViewBag.TotalProducts = totalProducts;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

        return View(products);
    }

    // 2. Tải Form tạo sản phẩm
    public async Task<IActionResult> ProductCreate()  
    { 
        var g = Guard(); if (g != null) return g; 
        ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).ToListAsync(); // Chỉ danh mục active
        ViewBag.Toppings = await _db.Toppings.Where(t => t.IsAvailable).ToListAsync();
        return View(new Product()); 
    }

    // 3. Xử lý tạo sản phẩm (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductCreate(Product model, IFormFile? uploadImage)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            // Xử lý upload ảnh nếu có
            if (uploadImage != null && uploadImage.Length > 0)
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadDir);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                string filePath = Path.Combine(uploadDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(fileStream);
                }
                model.ImageUrl = "/images/products/" + fileName;
            }

            model.CreatedAt = DateTime.Now;
            if (string.IsNullOrEmpty(model.Id)) model.Id = Guid.NewGuid().ToString("N");

            // Xử lý chọn topping thêm cho Product
            if (model.SelectedToppings != null && model.SelectedToppings.Any())
            {
                foreach (var tId in model.SelectedToppings)
                {
                    model.ProductToppings.Add(new ProductTopping { ProductId = model.Id, ToppingId = tId });
                }
            }
            
            _db.Products.Add(model);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }

        ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Toppings = await _db.Toppings.Where(t => t.IsAvailable).ToListAsync();
        return View(model);
    }

    // 4. Tải Form sửa sản phẩm
    public async Task<IActionResult> ProductEdit(string id) 
    { 
        var g = Guard(); if (g != null) return g; 

        var product = await _db.Products.Include(p => p.ProductToppings).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        // Load danh sách topping đã chọn
        product.SelectedToppings = product.ProductToppings.Select(pt => pt.ToppingId).ToList();

        ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).ToListAsync(); // Chỉ danh mục active
        ViewBag.Toppings = await _db.Toppings.Where(t => t.IsAvailable).ToListAsync();
        return View(product); 
    }

    // 5. Xử lý sửa sản phẩm (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductEdit(Product model, IFormFile? uploadImage)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            var p = await _db.Products.FindAsync(model.Id);
            if (p == null) return NotFound();

            p.Name = model.Name;
            p.Price = model.Price;
            p.CategoryId = model.CategoryId;
            p.Description = model.Description;
            p.IsAvailable = model.IsAvailable;

            if (uploadImage != null && uploadImage.Length > 0)
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadDir);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                string filePath = Path.Combine(uploadDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(fileStream);
                }
                p.ImageUrl = "/images/products/" + fileName;
            }

            _db.Products.Update(p);

            // Cập nhật quan hệ Toppings
            var existingTops = await _db.ProductToppings.Where(pt => pt.ProductId == p.Id).ToListAsync();
            _db.ProductToppings.RemoveRange(existingTops);

            if (model.SelectedToppings != null && model.SelectedToppings.Any())
            {
                foreach (var tId in model.SelectedToppings)
                {
                    _db.ProductToppings.Add(new ProductTopping { ProductId = p.Id, ToppingId = tId });
                }
            }

            await _db.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Đã cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }
        
        ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Toppings = await _db.Toppings.Where(t => t.IsAvailable).ToListAsync();
        return View(model);
    }

    // 6. Xóa sản phẩm
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductDelete(string id)
    {
        var g = Guard(); if (g != null) return g;

        var product = await _db.Products.FindAsync(id);
        if (product != null)
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa sản phẩm!";
        }
        return RedirectToAction(nameof(Products));
    }

    // ==========================================
    // ---- Quản lý Topping (CRUD) ----
    // ==========================================

    public async Task<IActionResult> ToppingCreate()  
    { 
        var g = Guard(); if (g != null) return g; 
        return View(new Topping()); 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToppingCreate(Topping model, IFormFile? uploadImage)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            if (uploadImage != null && uploadImage.Length > 0)
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadDir);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                string filePath = Path.Combine(uploadDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(fileStream);
                }
                model.ImageUrl = "/images/products/" + fileName;
            }

            model.CreatedAt = DateTime.Now;
            if (string.IsNullOrEmpty(model.Id)) model.Id = Guid.NewGuid().ToString("N");
            
            _db.Toppings.Add(model);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm topping thành công!";
            return RedirectToAction(nameof(Toppings));
        }

        return View(model);
    }

    public async Task<IActionResult> ToppingEdit(string id) 
    { 
        var g = Guard(); if (g != null) return g; 

        var product = await _db.Toppings.FindAsync(id);
        if (product == null) return NotFound();

        return View(product); 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToppingEdit(Topping model, IFormFile? uploadImage)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            var p = await _db.Toppings.FindAsync(model.Id);
            if (p == null) return NotFound();

            p.Name = model.Name;
            p.Price = model.Price;
            p.IsAvailable = model.IsAvailable;
            p.UpdatedAt = DateTime.UtcNow;
            
            if (uploadImage != null && uploadImage.Length > 0)
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadDir);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                string filePath = Path.Combine(uploadDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(fileStream);
                }
                p.ImageUrl = "/images/products/" + fileName;
            }

            _db.Toppings.Update(p);
            await _db.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Đã cập nhật topping thành công!";
            return RedirectToAction(nameof(Toppings));
        }
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToppingDelete(string id)
    {
        var g = Guard(); if (g != null) return g;

        var product = await _db.Toppings.FindAsync(id);
        if (product != null)
        {
            _db.Toppings.Remove(product);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa topping thành công!";
        }
        return RedirectToAction(nameof(Toppings));
    }

    // ---- Quản lý danh mục (CRUD) ----
    public async Task<IActionResult> Categories(string searchQuery = "", string status = "all")
    {
        var g = Guard(); if (g != null) return g;

        var query = _db.Categories.Include(c => c.Products).AsQueryable();

        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(c => c.Name.Contains(searchQuery));
            ViewBag.SearchQuery = searchQuery;
        }

        if (status == "active")
        {
            query = query.Where(c => c.IsActive);
        }
        else if (status == "inactive")
        {
            query = query.Where(c => !c.IsActive);
        }

        ViewBag.CurrentStatus = status;

        var categories = await query.ToListAsync();
        return View(categories);
    }

    public IActionResult CategoryCreate()  { var g = Guard(); if (g != null) return g; return View(new Category()); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryCreate(Category model)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            _db.Categories.Add(model);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã thêm danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        return View(model);
    }

    public async Task<IActionResult> CategoryEdit(int id) 
    { 
        var g = Guard(); if (g != null) return g; 
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        return View(cat); 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryEdit(Category model)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            var cat = await _db.Categories.FindAsync(model.Id);
            if (cat == null) return NotFound();

            cat.Name = model.Name;
            cat.Description = model.Description;
            cat.IsActive = model.IsActive;

            _db.Categories.Update(cat);
            await _db.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Đã cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryDelete(int id)
    {
        var g = Guard(); if (g != null) return g;

        var cat = await _db.Categories.FindAsync(id);
        if (cat != null)
        {
            _db.Categories.Remove(cat);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa danh mục!";
        }
        return RedirectToAction(nameof(Categories));
    }

    // ---- Quản lý đơn hàng ----
    public async Task<IActionResult> Orders(string searchQuery = "", string status = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        var query = _db.Orders.Include(o => o.User).AsQueryable();

        // Tìm kiếm theo mã đơn hoặc tên khách hàng
        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(o =>
                o.Id.Contains(searchQuery) ||
                (o.User != null && o.User.FullName.Contains(searchQuery)));
            ViewBag.SearchQuery = searchQuery;
        }

        // Lọc theo trạng thái
        if (status != "all")
        {
            query = query.Where(o => o.Status == status);
        }
        ViewBag.CurrentStatus = status;

        // Phân trang
        int pageSize = 10;
        int totalOrders = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.OrderDate)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToListAsync();

        ViewBag.TotalOrders = totalOrders;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);

        return View(orders);
    }
    public IActionResult OrderDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OrderUpdateStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        var order = await _db.Orders.FindAsync(id);
        if (order != null)
        {
            order.Status = status;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn #{id.Substring(0, 8).ToUpper()} → {status}.";
        }
        return RedirectToAction(nameof(Orders));
    }

    // ---- Quản lý tài khoản ----
    public async Task<IActionResult> Accounts(string searchQuery = "", string status = "all", string role = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        var query = _db.Users.AsQueryable();

        // Tìm kiếm theo tên, email
        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(u => u.FullName.Contains(searchQuery)
                                  || u.Email.Contains(searchQuery)
                                  || u.Id.Contains(searchQuery));
            ViewBag.SearchQuery = searchQuery;
        }

        // Lọc trạng thái
        if (status == "active")   query = query.Where(u => u.IsActive);
        if (status == "locked")   query = query.Where(u => !u.IsActive);
        ViewBag.CurrentStatus = status;

        // Lọc vai trò
        if (role != "all" && Enum.TryParse<UserRole>(role, out var parsedRole)) query = query.Where(u => u.Role == parsedRole);
        ViewBag.CurrentRole = role;

        int pageSize = 10;
        int total = await query.CountAsync();
        var users = await query.OrderByDescending(u => u.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

        ViewBag.TotalUsers  = total;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)pageSize);

        return View(users);
    }

    // POST: Toggle khóa/mở tài khoản
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AccountToggleLock(string id)
    {
        var g = Guard(); if (g != null) return g;
        var u = await _db.Users.FindAsync(id);
        if (u != null) { u.IsActive = !u.IsActive; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Accounts));
    }

    // ---- Quản lý nhân viên ----
    public async Task<IActionResult> Staff(string searchQuery = "", string status = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        var query = _db.Users.Where(u => u.Role != UserRole.Customer).AsQueryable();

        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(u => u.FullName.Contains(searchQuery) || u.Id.Contains(searchQuery));
            ViewBag.SearchQuery = searchQuery;
        }

        if (status == "active") query = query.Where(u => u.IsActive);
        if (status == "inactive") query = query.Where(u => !u.IsActive);
        ViewBag.CurrentStatus = status;

        int pageSize = 10;
        int totalStaff = await query.CountAsync();
        var staffList = await query.OrderByDescending(u => u.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

        ViewBag.TotalStaff = totalStaff;
        ViewBag.WorkingStaff = await _db.Users.CountAsync(u => u.Role != UserRole.Customer && u.IsActive);
        ViewBag.LeaveStaff = await _db.Users.CountAsync(u => u.Role != UserRole.Customer && !u.IsActive);
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalStaff / (double)pageSize);

        return View(staffList);
    }

    public IActionResult StaffCreate()     { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StaffCreate(User model, IFormFile? uploadImage)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            if (uploadImage != null && uploadImage.Length > 0)
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                Directory.CreateDirectory(uploadDir);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                string filePath = Path.Combine(uploadDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(fileStream);
                }
                model.Avatar = "/images/avatars/" + fileName;
            }

            model.PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

            _db.Users.Add(model);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã thêm nhân viên \"{model.FullName}\" thành công!";
            return RedirectToAction(nameof(Staff));
        }

        return View(model);
    }

    public async Task<IActionResult> StaffEdit(string id)
    {
        var g = Guard(); if (g != null) return g;

        var user = await _db.Users.FindAsync(id);
        if (user == null || user.Role == UserRole.Customer) return NotFound();

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StaffEdit(string id, string role)
    {
        var g = Guard(); if (g != null) return g;

        var user = await _db.Users.FindAsync(id);
        if (user == null || user.Role == UserRole.Customer) return NotFound();

        if (Enum.TryParse<UserRole>(role, out var parsedRole) && (parsedRole == UserRole.Admin || parsedRole == UserRole.Staff))
        {
            user.Role = parsedRole;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật chức vụ nhân viên \"{user.FullName}\" thành {parsedRole}!";
            return RedirectToAction(nameof(Staff));
        }

        ModelState.AddModelError("Role", "Chức vụ không hợp lệ.");
        return View(user);
    }

    // ---- Quản lý khuyến mãi ----

    public async Task<IActionResult> Promotions(string searchQuery = "", string status = "all", int page = 1)
    { 
        var g = Guard(); if (g != null) return g; 

        var query = _db.Vouchers.AsQueryable();

        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(v => v.Code.Contains(searchQuery) || v.Name.Contains(searchQuery));
            ViewBag.SearchQuery = searchQuery;
        }

        var now = DateTime.Now;
        if (status == "active")
            query = query.Where(v => v.IsActive && (v.ExpiryDate == null || v.ExpiryDate >= now));
        else if (status == "expired")
            query = query.Where(v => !v.IsActive || (v.ExpiryDate.HasValue && v.ExpiryDate < now));

        ViewBag.CurrentStatus = status;

        int pageSize = 10;
        int total = await query.CountAsync();
        var vouchers = await query.OrderByDescending(v => v.Id)
                                  .Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();

        ViewBag.TotalVouchers = total;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return View(vouchers); 
    }

    // GET: Tạo voucher
    public IActionResult VoucherCreate()
    {
        var g = Guard(); if (g != null) return g;
        return View(new Voucher());
    }

    // POST: Tạo voucher
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoucherCreate(Voucher model, string discountType)
    {
        var g = Guard(); if (g != null) return g;

        // Xử lý loại giảm giá
        if (discountType == "percent")
        {
            model.DiscountAmount = null;
        }
        else
        {
            model.DiscountPercent = null;
        }

        // Tạo mảng uppercase cho Code
        model.Code = (model.Code ?? "").Trim().ToUpper();
        model.Name = model.Name ?? "";
        
        // Đọc IsActive trực tiếp từ form
        model.IsActive = Request.Form["IsActiveCb"] == "true";

        _db.Vouchers.Add(model);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã tạo voucher \"{model.Code}\" thành công!";
        return RedirectToAction(nameof(Promotions));
    }

    // GET: Sửa voucher
    public async Task<IActionResult> VoucherEdit(int id)
    {
        var g = Guard(); if (g != null) return g;

        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher == null) return NotFound();

        return View(voucher);
    }

    // POST: Sửa voucher
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoucherEdit(Voucher model, string discountType)
    {
        var g = Guard(); if (g != null) return g;

        var oldVoucher = await _db.Vouchers.FindAsync(model.Id);
        if (oldVoucher == null) return NotFound();

        // Gán từng field thủ công để tránh bị lọc bởi ModelState
        oldVoucher.Code = (model.Code ?? "").Trim().ToUpper();
        oldVoucher.Name = model.Name ?? "";

        // Xử lý loại giảm giá
        if (discountType == "percent")
        {
            oldVoucher.DiscountPercent = model.DiscountPercent;
            oldVoucher.DiscountAmount  = null;
        }
        else
        {
            oldVoucher.DiscountAmount  = model.DiscountAmount;
            oldVoucher.DiscountPercent = null;
        }

        oldVoucher.MinOrderValue = model.MinOrderValue;
        oldVoucher.StartDate     = model.StartDate;
        oldVoucher.ExpiryDate    = model.ExpiryDate;
        oldVoucher.IsActive      = Request.Form["IsActiveCb"] == "true";
        oldVoucher.MaxUses       = model.MaxUses;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật voucher \"{oldVoucher.Code}\" thành công!";
        return RedirectToAction(nameof(Promotions));
    }

    // POST: Xóa voucher
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoucherDelete(int id)
    {
        var g = Guard(); if (g != null) return g;
        var v = await _db.Vouchers.FindAsync(id);
        if (v != null)
        {
            // Gỡ FK trước khi xóa: null hóa VoucherId ở các đơn hàng liên quan
            var relatedOrders = await _db.Orders.Where(o => o.VoucherId == id).ToListAsync();
            foreach (var order in relatedOrders)
                order.VoucherId = null;

            _db.Vouchers.Remove(v);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa voucher!";
        }
        return RedirectToAction(nameof(Promotions));
    }
}
