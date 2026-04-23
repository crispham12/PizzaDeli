using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;
using System.IO;

namespace PizzaDeli.Services;

public class AdminService
{
    private readonly ApplicationDbContext _db;
    public AdminService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Logic: Quản lý danh sách sản phẩm (Lấy danh sách phân trang)
    /// Cách hoạt động: Lọc sản phẩm theo chuỗi tìm kiếm và danh mục. Tính tổng số lượng để phân trang và lấy danh sách danh mục đang hoạt động.
    /// </summary>
    public async Task<(List<Product> Products, int TotalProducts, List<Category> Categories)> GetProductsPagedAsync(string searchQuery, int? categoryId, int page, int pageSize)
    {
        var query = _db.Products.Include(p => p.Category).Where(p => p.Category != null && p.Category.IsActive).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);

        int total = await query.CountAsync();
        var products = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var categories = await _db.Categories.Where(c => c.IsActive).ToListAsync();
        return (products, total, categories);
    }

    /// <summary>
    /// Logic: Quản lý danh sách Topping (Lấy danh sách phân trang)
    /// Cách hoạt động: Lọc topping theo chuỗi tìm kiếm (tên/ID) và trạng thái (active/inactive). Áp dụng Skip và Take để phân trang.
    /// </summary>
    public async Task<(List<Topping> Toppings, int Total)> GetToppingsPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Toppings.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
        if (status == "active") query = query.Where(p => p.IsAvailable);
        else if (status == "inactive") query = query.Where(p => !p.IsAvailable);

        int total = await query.CountAsync();
        var toppings = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (toppings, total);
    }

    /// <summary>
    /// Logic: Quản lý danh sách Thành phần Custom Pizza (Phân trang)
    /// Cách hoạt động: Lọc thành phần cấu tạo Pizza tự chọn theo từ khóa và trạng thái. Phục vụ cho giao diện quản lý kho nguyên liệu.
    /// </summary>
    public async Task<(List<PizzaComponent> Components, int Total)> GetComponentsPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.PizzaComponents.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
        if (status == "active") query = query.Where(p => p.IsAvailable);
        else if (status == "inactive") query = query.Where(p => !p.IsAvailable);

        int total = await query.CountAsync();
        var components = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (components, total);
    }

    /// <summary>
    /// Logic: Quản lý Đơn hàng cho Admin (Phân trang)
    /// Cách hoạt động: Join bảng Users để lấy tên khách hàng. Lọc đơn theo ID hoặc Tên khách, kết hợp lọc theo trạng thái đơn hàng (Pending, Completed...).
    /// </summary>
    public async Task<(List<Order> Orders, int Total)> GetOrdersPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Orders.Include(o => o.User).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(o => o.Id.Contains(searchQuery) || (o.User != null && o.User.FullName.Contains(searchQuery)));
        if (status != "all") query = query.Where(o => o.Status == status);

        int total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.OrderDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (orders, total);
    }

    /// <summary>
    /// Logic: Quản lý Tài khoản Khách hàng (Phân trang)
    /// Cách hoạt động: Tìm kiếm tài khoản theo Tên, Email, ID. Lọc theo trạng thái bị khóa hay đang hoạt động và vai trò (Role).
    /// </summary>
    public async Task<(List<User> Users, int Total)> GetAccountsPagedAsync(string searchQuery, string status, string role, int page, int pageSize)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(u => u.FullName.Contains(searchQuery) || u.Email.Contains(searchQuery) || u.Id.Contains(searchQuery));
        
        if (status == "active") query = query.Where(u => u.IsActive);
        if (status == "locked") query = query.Where(u => !u.IsActive);
        
        if (role != "all" && Enum.TryParse<UserRole>(role, out var parsedRole)) query = query.Where(u => u.Role == parsedRole);

        int total = await query.CountAsync();
        var users = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (users, total);
    }

    /// <summary>
    /// Logic: Quản lý Nhân sự (Phân trang & Thống kê cơ bản)
    /// Cách hoạt động: Loại trừ khách hàng (Role != Customer). Trả về danh sách nhân viên cùng với số lượng nhân viên đang làm việc và nghỉ phép.
    /// </summary>
    public async Task<(List<User> Staff, int Total, int Working, int Leave)> GetStaffPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Users.Where(u => u.Role != UserRole.Customer).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(u => u.FullName.Contains(searchQuery) || u.Id.Contains(searchQuery));
        
        if (status == "active") query = query.Where(u => u.IsActive);
        if (status == "inactive") query = query.Where(u => !u.IsActive);

        int total = await query.CountAsync();
        var staff = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        int working = await _db.Users.CountAsync(u => u.Role != UserRole.Customer && u.IsActive);
        int leave = await _db.Users.CountAsync(u => u.Role != UserRole.Customer && !u
        .IsActive);
        
        return (staff, total, working, leave);
    }

    /// <summary>
    /// Logic: Quản lý Mã giảm giá (Phân trang)
    /// Cách hoạt động: Kiểm tra ngày hết hạn so với thời gian thực (DateTime.UtcNow) để phân loại Voucher còn hạn (active) hay đã hết hạn (expired).
    /// </summary>
    public async Task<(List<Voucher> Vouchers, int Total)> GetVouchersPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Vouchers.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(v => v.Code.Contains(searchQuery) || v.Name.Contains(searchQuery));

        var now = DateTime.UtcNow;
        if (status == "active") query = query.Where(v => v.IsActive && (v.ExpiryDate == null || v.ExpiryDate >= now));
        else if (status == "expired") query = query.Where(v => !v.IsActive || (v.ExpiryDate.HasValue && v.ExpiryDate < now));

        int total = await query.CountAsync();
        var vouchers = await query.OrderByDescending(v => v.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (vouchers, total);
    }

    public async Task<List<Category>> GetCategoriesAsync(string searchQuery, string status)
    {
        var query = _db.Categories.Include(c => c.Products).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(c => c.Name.Contains(searchQuery));
        if (status == "active") query = query.Where(c => c.IsActive);
        else if (status == "inactive") query = query.Where(c => !c.IsActive);
        return await query.ToListAsync();
    }

    /// <summary>
    /// Logic: Lấy danh sách sản phẩm cần đồng bộ vector AI
    /// Cách hoạt động: Chỉ lấy các sản phẩm chưa có ImageEmbedding (vector). Nếu có productId cụ thể, chỉ lấy sản phẩm đó.
    /// </summary>
    public async Task<List<Product>> GetProductsForSyncAsync(string? productId)
    {
        var query = _db.Products.Where(p => p.IsAvailable && !string.IsNullOrEmpty(p.ImageUrl));
        if (!string.IsNullOrEmpty(productId)) query = query.Where(p => p.Id == productId);
        else query = query.Where(p => string.IsNullOrEmpty(p.ImageEmbedding));
        return await query.ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Logic: Thao tác Generic CRUD (Create, Read, Update, Delete)
    /// Cách hoạt động: Dùng Set<T>() của Entity Framework để thực hiện các thao tác thêm/xóa/sửa trên mọi bảng dữ liệu mà không cần Inject DbContext vào Controller.
    /// </summary>
    public async Task<T?> FindAsync<T>(params object[] keyValues) where T : class => await _db.Set<T>().FindAsync(keyValues);
    public void Add<T>(T entity) where T : class => _db.Set<T>().Add(entity);
    public void Update<T>(T entity) where T : class => _db.Set<T>().Update(entity);
    public void Remove<T>(T entity) where T : class => _db.Set<T>().Remove(entity);
    public void RemoveRange<T>(IEnumerable<T> entities) where T : class => _db.Set<T>().RemoveRange(entities);

    public async Task<Product?> GetProductWithToppingsAsync(string id)
        => await _db.Products.Include(p => p.ProductToppings).FirstOrDefaultAsync(p => p.Id == id);
        
    public async Task<Category?> GetCategoryWithProductsAsync(int id)
        => await _db.Categories.Include(c => c.Products).ThenInclude(p => p.ProductToppings)
                               .Include(c => c.Products).ThenInclude(p => p.Reviews)
                               .Include(c => c.Products).ThenInclude(p => p.OrderDetails)
                               .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<ProductTopping>> GetProductToppingsAsync(string productId)
        => await _db.ProductToppings.Where(pt => pt.ProductId == productId).ToListAsync();
        
    public async Task<List<Order>> GetOrdersByVoucherIdAsync(int voucherId)
        => await _db.Orders.Where(o => o.VoucherId == voucherId).ToListAsync();

    public async Task<List<Category>> GetActiveCategoriesAsync() => await _db.Categories.Where(c => c.IsActive).ToListAsync();
    public async Task<List<Topping>> GetActiveToppingsAsync() => await _db.Toppings.Where(t => t.IsAvailable).ToListAsync();
}
