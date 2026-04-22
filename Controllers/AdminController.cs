using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;
using PizzaDeli.Services;
using System.IO;

namespace PizzaDeli.Controllers;

/// <summary>Admin: Thống kê, Quản lý sản phẩm/đơn hàng/danh mục/tài khoản/nhân viên/khuyến mãi</summary>
public class AdminController : BaseController
{
    private readonly DashboardService _dashboard;
    private readonly AdminService _admin;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly AiIntegratorService _aiService;

    public AdminController(DashboardService dashboard, AdminService admin, IWebHostEnvironment webHostEnvironment, AiIntegratorService aiService)
    {
        _dashboard = dashboard;
        _admin = admin;
        _webHostEnvironment = webHostEnvironment;
        _aiService = aiService;
    }

    // Guard tất cả action
    private IActionResult? Guard() => RequireRole("Admin");

    // ---- Dashboard / Thống kê ----
    public async Task<IActionResult> Dashboard()
    {
        var g = Guard(); if (g != null) return g;

        var stats = await _dashboard.GetDashboardStatsAsync();
        
        ViewBag.TotalOrders = stats.totalOrders;
        ViewBag.RevenueToday = stats.revenueToday;
        ViewBag.TotalProducts = stats.totalProducts;
        ViewBag.TotalCustomers = stats.totalCustomers;
        ViewBag.OrdersThisMonth = stats.ordersThisMonth;
        ViewBag.RevenueThisMonth = stats.revenueThisMonth;
        ViewBag.OrdersPending = stats.ordersPending;
        ViewBag.OrdersDelivering = stats.ordersDelivering;
        ViewBag.OrdersCompleted = stats.ordersCompleted;
        ViewBag.Categories = stats.categories;
        ViewBag.AdminCount = stats.adminCount;
        ViewBag.StaffCount = stats.staffCount;
        ViewBag.ActiveVouchers = stats.activeVouchers;
        ViewBag.ActiveVoucher = stats.activeVoucher;

        return View();
    }

    // ---- Thống kê ----
    public async Task<IActionResult> Statistics()
    {
        var g = Guard(); if (g != null) return g;

        var stats = await _dashboard.GetStatisticsAsync();

        ViewBag.StatsRevenue = stats.StatsRevenue;
        ViewBag.StatRevenuePct = stats.StatRevenuePct;
        ViewBag.StatsOrders = stats.StatsOrders;
        ViewBag.StatOrdersPct = stats.StatOrdersPct;
        ViewBag.StatsAOV = stats.StatsAOV;
        ViewBag.StatAOVPct = stats.StatAOVPct;
        ViewBag.StatsCustomers = stats.StatsCustomers;
        ViewBag.StatCustomersPct = stats.StatCustomersPct;
        ViewBag.ChartLabels = stats.ChartLabels;
        ViewBag.ChartValues = stats.ChartValues;
        ViewBag.DonutLabels = stats.DonutLabels;
        ViewBag.DonutValues = stats.DonutValues;
        ViewBag.TopProducts = stats.TopProducts;

        return View();
    }


    // ---- Quản lý sản phẩm (CRUD) ----
    
    // 1. Xem danh sách sản phẩm
    public async Task<IActionResult> Products(string searchQuery = "", int? categoryId = null, int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        int pageSize = 10;
        var (products, totalProducts, categories) = await _admin.GetProductsPagedAsync(searchQuery, categoryId, page, pageSize);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        if (categoryId.HasValue) ViewBag.CurrentCategory = categoryId.Value;

        ViewBag.TotalProducts = totalProducts;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);
        ViewBag.Categories = categories;

        return View(products);
    }

    // 1.1 Xem danh sách Topping (dùng bảng Topping mới)
    public async Task<IActionResult> Toppings(string searchQuery = "", string status = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        int pageSize = 10;
        var (products, totalProducts) = await _admin.GetToppingsPagedAsync(searchQuery, status, page, pageSize);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        ViewBag.CurrentStatus = status;
        ViewBag.TotalProducts = totalProducts;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

        return View(products);
    }

    // 1.2 Quản lý Custom Pizza Components
    public async Task<IActionResult> CustomPizza(string searchQuery = "", string status = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        int pageSize = 10;
        var (products, totalProducts) = await _admin.GetComponentsPagedAsync(searchQuery, status, page, pageSize);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        ViewBag.CurrentStatus = status;
        ViewBag.TotalProducts = totalProducts;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

        return View(products);
    }

    public IActionResult CustomPizzaCreate()  
    { 
        var g = Guard(); if (g != null) return g; 
        return View(new PizzaComponent()); 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CustomPizzaCreate(PizzaComponent model, IFormFile? uploadImage)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            if (uploadImage != null && uploadImage.Length > 0)
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "components");
                Directory.CreateDirectory(uploadDir);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                string filePath = Path.Combine(uploadDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(fileStream);
                }
                model.ImageUrl = "/images/components/" + fileName;
            }

            model.CreatedAt = DateTime.Now;
            if (string.IsNullOrEmpty(model.Id)) model.Id = Guid.NewGuid().ToString("N");
            _admin.Add(model);
            await _admin.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thành phần đã được tạo thành công!";
            return RedirectToAction(nameof(CustomPizza));
        }

        return View(model);
    }

    public async Task<IActionResult> CustomPizzaEdit(string id) 
    { 
        var g = Guard(); if (g != null) return g; 

        var comp = await _admin.FindAsync<PizzaComponent>(id);
        if (comp == null) return NotFound();

        return View(comp); 
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CustomPizzaEdit(PizzaComponent model, IFormFile? uploadImage)
    {
        var g = Guard(); if (g != null) return g;

        if (ModelState.IsValid)
        {
            var p = await _admin.FindAsync<PizzaComponent>(model.Id);
            if (p == null) return NotFound();

            p.Name = model.Name;
            p.Price = model.Price;
            p.ComponentType = model.ComponentType;
            p.IsAvailable = model.IsAvailable;
            p.Stock = model.Stock;
            p.UpdatedAt = DateTime.UtcNow;
            
            if (uploadImage != null && uploadImage.Length > 0)
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images", "components");
                Directory.CreateDirectory(uploadDir);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                string filePath = Path.Combine(uploadDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(fileStream);
                }
                p.ImageUrl = "/images/components/" + fileName;
            }

            _admin.Update(p);
            await _admin.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Cập nhật thành phần thành công!";
            return RedirectToAction(nameof(CustomPizza));
        }
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CustomPizzaDelete(string id)
    {
        var g = Guard(); if (g != null) return g;

        var comp = await _admin.FindAsync<PizzaComponent>(id);
        if (comp != null)
        {
            _admin.Remove(comp);
            await _admin.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa thành phần!";
        }
        return RedirectToAction(nameof(CustomPizza));
    }

    // 2. Tải Form tạo sản phẩm
    public async Task<IActionResult> ProductCreate()  
    { 
        var g = Guard(); if (g != null) return g; 
        ViewBag.Categories = await _admin.GetActiveCategoriesAsync(); // Chỉ danh mục active
        ViewBag.Toppings = await _admin.GetActiveToppingsAsync();
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

                // 🤖 Tự động tạo Image Embedding ngay khi upload ảnh mới
                try
                {
                    using var embStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    var embedding = await _aiService.GetImageEmbeddingAsync(embStream, fileName);
                    model.ImageEmbedding = System.Text.Json.JsonSerializer.Serialize(embedding);
                }
                catch (Exception aiEx)
                {
                    // Không chặn luồng chính nếu AI service chưa chạy
                    model.ImageEmbedding = null;
                    TempData["AiWarning"] = $"Ảnh đã lưu nhưng chưa tạo được AI vector: {aiEx.Message}";
                }
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
            
            _admin.Add(model);
            await _admin.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }

        ViewBag.Categories = await _admin.GetActiveCategoriesAsync();
        ViewBag.Toppings = await _admin.GetActiveToppingsAsync();
        return View(model);
    }

    // 4. Tải Form sửa sản phẩm
    public async Task<IActionResult> ProductEdit(string id) 
    { 
        var g = Guard(); if (g != null) return g; 

        var product = await _admin.GetProductWithToppingsAsync(id);
        if (product == null) return NotFound();

        // Load danh sách topping đã chọn
        product.SelectedToppings = product.ProductToppings.Select(pt => pt.ToppingId).ToList();

        ViewBag.Categories = await _admin.GetActiveCategoriesAsync(); // Chỉ danh mục active
        ViewBag.Toppings = await _admin.GetActiveToppingsAsync();
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
            var p = await _admin.FindAsync<Product>(model.Id);
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

                // 🤖 Tự động tạo lại Image Embedding khi đổi ảnh
                try
                {
                    using var embStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    var embedding = await _aiService.GetImageEmbeddingAsync(embStream, fileName);
                    p.ImageEmbedding = System.Text.Json.JsonSerializer.Serialize(embedding);
                }
                catch (Exception aiEx)
                {
                    p.ImageEmbedding = null;
                    TempData["AiWarning"] = $"Ảnh đã lưu nhưng chưa tạo được AI vector: {aiEx.Message}";
                }
            }

            _admin.Update(p);

            // Cập nhật quan hệ Toppings
            var existingTops = await _admin.GetProductToppingsAsync(p.Id);
            _admin.RemoveRange(existingTops);

            if (model.SelectedToppings != null && model.SelectedToppings.Any())
            {
                foreach (var tId in model.SelectedToppings)
                {
                    _admin.Add(new ProductTopping { ProductId = p.Id, ToppingId = tId });
                }
            }

            await _admin.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Đã cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }
        
        ViewBag.Categories = await _admin.GetActiveCategoriesAsync();
        ViewBag.Toppings = await _admin.GetActiveToppingsAsync();
        return View(model);
    }

    // 6. Xóa sản phẩm
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductDelete(string id)
    {
        var g = Guard(); if (g != null) return g;

        var product = await _admin.FindAsync<Product>(id);
        if (product != null)
        {
            _admin.Remove(product);
            await _admin.SaveChangesAsync();
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
            
            _admin.Add(model);
            await _admin.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm topping thành công!";
            return RedirectToAction(nameof(Toppings));
        }

        return View(model);
    }

    public async Task<IActionResult> ToppingEdit(string id) 
    { 
        var g = Guard(); if (g != null) return g; 

        var product = await _admin.FindAsync<Topping>(id);
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
            var p = await _admin.FindAsync<Topping>(model.Id);
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

            _admin.Update(p);
            await _admin.SaveChangesAsync();
            
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

        var product = await _admin.FindAsync<Topping>(id);
        if (product != null)
        {
            _admin.Remove(product);
            await _admin.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa topping thành công!";
        }
        return RedirectToAction(nameof(Toppings));
    }

    // ---- Quản lý danh mục (CRUD) ----
    public async Task<IActionResult> Categories(string searchQuery = "", string status = "all")
    {
        var g = Guard(); if (g != null) return g;

        var categories = await _admin.GetCategoriesAsync(searchQuery, status);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        ViewBag.CurrentStatus = status;

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
            _admin.Add(model);
            await _admin.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã thêm danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        return View(model);
    }

    public async Task<IActionResult> CategoryEdit(int id) 
    { 
        var g = Guard(); if (g != null) return g; 
        var cat = await _admin.FindAsync<Category>(id);
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
            var cat = await _admin.FindAsync<Category>(model.Id);
            if (cat == null) return NotFound();

            cat.Name = model.Name;
            cat.Description = model.Description;
            cat.IsActive = model.IsActive;

            _admin.Update(cat);
            await _admin.SaveChangesAsync();
            
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

        var cat = await _admin.GetCategoryWithProductsAsync(id);

        if (cat != null)
        {
            // Xóa tất cả dữ liệu liên quan của từng sản phẩm trong danh mục
            foreach (var product in cat.Products)
            {
                _admin.RemoveRange(product.ProductToppings);
                _admin.RemoveRange(product.Reviews);
                _admin.RemoveRange(product.OrderDetails);
            }

            // Xóa tất cả sản phẩm thuộc danh mục
            _admin.RemoveRange(cat.Products);

            // Xóa danh mục
            _admin.Remove(cat);
            await _admin.SaveChangesAsync();

            int deletedCount = cat.Products.Count;
            TempData["SuccessMessage"] = $"Đã xóa danh mục và {deletedCount} sản phẩm bên trong!";
        }
        return RedirectToAction(nameof(Categories));
    }

    // ---- Quản lý đơn hàng ----
    public async Task<IActionResult> Orders(string searchQuery = "", string status = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        int pageSize = 10;
        var (orders, totalOrders) = await _admin.GetOrdersPagedAsync(searchQuery, status, page, pageSize);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        ViewBag.CurrentStatus = status;

        ViewBag.TotalOrders = totalOrders;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);

        return View(orders);
    }
    public IActionResult OrderDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    [HttpGet]
    public IActionResult RedirectToDeliveryManagement(string id)
    {
        var g = Guard(); if (g != null) return g;
        TempData["Error"] = "Chỉ nhân viên giao hàng mới được cập nhật trạng thái giao hàng. Vui lòng xử lý tại trang Quản lý giao hàng.";
        return RedirectToAction("Deliveries", "Staff", new { filter = "pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OrderUpdateStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        var order = await _admin.FindAsync<Order>(id);
        if (order != null)
        {
            order.Status = status;
            await _admin.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn #{id.Substring(0, 8).ToUpper()} → {status}.";
        }
        return RedirectToAction(nameof(Orders));
    }

    // ---- Quản lý tài khoản ----
    public async Task<IActionResult> Accounts(string searchQuery = "", string status = "all", string role = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        int pageSize = 10;
        var (users, total) = await _admin.GetAccountsPagedAsync(searchQuery, status, role, page, pageSize);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentRole = role;

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
        var u = await _admin.FindAsync<User>(id);
        if (u != null) { u.IsActive = !u.IsActive; await _admin.SaveChangesAsync(); }
        return RedirectToAction(nameof(Accounts));
    }

    // ---- Quản lý nhân viên ----
    public async Task<IActionResult> Staff(string searchQuery = "", string status = "all", int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        int pageSize = 10;
        var (staffList, totalStaff, workingStaff, leaveStaff) = await _admin.GetStaffPagedAsync(searchQuery, status, page, pageSize);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        ViewBag.CurrentStatus = status;

        ViewBag.TotalStaff = totalStaff;
        ViewBag.WorkingStaff = workingStaff;
        ViewBag.LeaveStaff = leaveStaff;
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

            _admin.Add(model);
            await _admin.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã thêm nhân viên \"{model.FullName}\" thành công!";
            return RedirectToAction(nameof(Staff));
        }

        return View(model);
    }

    public async Task<IActionResult> StaffEdit(string id)
    {
        var g = Guard(); if (g != null) return g;

        var user = await _admin.FindAsync<User>(id);
        if (user == null || user.Role == UserRole.Customer) return NotFound();

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StaffEdit(string id, string role)
    {
        var g = Guard(); if (g != null) return g;

        var user = await _admin.FindAsync<User>(id);
        if (user == null || user.Role == UserRole.Customer) return NotFound();

        if (Enum.TryParse<UserRole>(role, out var parsedRole) && (parsedRole == UserRole.Admin || parsedRole == UserRole.Staff))
        {
            user.Role = parsedRole;
            await _admin.SaveChangesAsync();
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

        int pageSize = 10;
        var (vouchers, total) = await _admin.GetVouchersPagedAsync(searchQuery, status, page, pageSize);

        if (!string.IsNullOrEmpty(searchQuery)) ViewBag.SearchQuery = searchQuery;
        ViewBag.CurrentStatus = status;

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

        _admin.Add(model);
        await _admin.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã tạo voucher \"{model.Code}\" thành công!";
        return RedirectToAction(nameof(Promotions));
    }

    // GET: Sửa voucher
    public async Task<IActionResult> VoucherEdit(int id)
    {
        var g = Guard(); if (g != null) return g;

        var voucher = await _admin.FindAsync<Voucher>(id);
        if (voucher == null) return NotFound();

        return View(voucher);
    }

    // POST: Sửa voucher
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoucherEdit(Voucher model, string discountType)
    {
        var g = Guard(); if (g != null) return g;

        var oldVoucher = await _admin.FindAsync<Voucher>(model.Id);
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

        await _admin.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật voucher \"{oldVoucher.Code}\" thành công!";
        return RedirectToAction(nameof(Promotions));
    }

    // POST: Xóa voucher
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoucherDelete(int id)
    {
        var g = Guard(); if (g != null) return g;
        var v = await _admin.FindAsync<Voucher>(id);
        if (v != null)
        {
            // Gỡ FK trước khi xóa: null hóa VoucherId ở các đơn hàng liên quan
            var relatedOrders = await _admin.GetOrdersByVoucherIdAsync(id);
            foreach (var order in relatedOrders)
                order.VoucherId = null;

            _admin.Remove(v);
            await _admin.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa voucher!";
        }
        return RedirectToAction(nameof(Promotions));
    }

    // ============================================================
    // ---- AI: Đồng bộ Image Embedding cho toàn bộ sản phẩm ----
    // ============================================================

    /// <summary>
    /// POST: Sync embedding cho 1 sản phẩm cụ thể hoặc toàn bộ (id = null).
    /// Trả về JSON { success, synced, failed, errors }
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SyncEmbeddings(string? productId = null)
    {
        var g = Guard(); if (g != null) return Json(new { success = false, message = "Không có quyền." });

        // Lấy danh sách sản phẩm cần sync
        var products = await _admin.GetProductsForSyncAsync(productId);

        if (!products.Any())
            return Json(new { success = true, synced = 0, failed = 0, message = "Tất cả sản phẩm đã có vector AI." });

        int synced = 0, failed = 0;
        var errors = new List<string>();

        foreach (var p in products)
        {
            try
            {
                // Xử lý ImageUrl: có thể là đường dẫn tương đối /images/... hoặc URL đầy đủ
                string localPath;
                if (p.ImageUrl!.StartsWith("/"))
                {
                    localPath = Path.Combine(_webHostEnvironment.WebRootPath, p.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                }
                else
                {
                    errors.Add($"{p.Name}: ImageUrl không phải đường dẫn local ({p.ImageUrl})");
                    failed++;
                    continue;
                }

                if (!System.IO.File.Exists(localPath))
                {
                    errors.Add($"{p.Name}: File ảnh không tồn tại ({localPath})");
                    failed++;
                    continue;
                }

                using var stream = System.IO.File.OpenRead(localPath);
                var embedding = await _aiService.GetImageEmbeddingAsync(stream, Path.GetFileName(localPath));
                p.ImageEmbedding = System.Text.Json.JsonSerializer.Serialize(embedding);
                synced++;
            }
            catch (Exception ex)
            {
                errors.Add($"{p.Name}: {ex.Message}");
                failed++;
            }
        }

        await _admin.SaveChangesAsync();

        return Json(new
        {
            success = true,
            synced,
            failed,
            total = products.Count,
            errors,
            message = $"Đồng bộ xong: {synced} thành công, {failed} thất bại."
        });
    }
}
