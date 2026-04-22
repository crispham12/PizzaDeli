using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Đơn hàng - Customer đặt hàng, Staff/Admin quản lý</summary>
public class OrderService
{
    private readonly ApplicationDbContext _db;
    private readonly VoucherService _voucher;

    public OrderService(ApplicationDbContext db, VoucherService voucher)
    {
        _db      = db;
        _voucher = voucher;
    }

    // ---- Customer: Xem lịch sử đơn hàng của mình ----
    public async Task<List<Order>> GetByUserAsync(string userId)
        => await _db.Orders
                    .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                    .Include(o => o.Voucher)
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

    public async Task<Order?> GetLatestOrderAsync(string userId)
    {
        return await _db.Orders
            .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CancelOrderAsync(string orderId, string userId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        if (order == null || order.Status != "Pending") return false;
        
        order.Status = "Cancelled";
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Staff/Admin: Lấy tất cả đơn hàng ----
    public async Task<List<Order>> GetAllAsync()
        => await _db.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderDetails)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

    public async Task<(List<Order> Orders, int TotalPages)> GetStaffOrdersPagedAsync(int page, int pageSize)
    {
        var query = _db.Orders.Include(o => o.User).OrderByDescending(o => o.OrderDate);
        var orders = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return (orders, totalPages);
    }

    /// <summary>
    /// Logic: Thống kê đơn hàng cho màn hình Staff/Bếp
    /// Cách hoạt động: Gom nhóm và đếm số lượng đơn hàng trong ngày, số đơn chờ xử lý và số đơn đang giao.
    /// </summary>
    public async Task<(int TotalToday, int Pending, int Delivering)> GetStaffOrderStatsAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        
        int totalToday = await _db.Orders.CountAsync(o => o.OrderDate >= today && o.OrderDate < tomorrow);
        int pending = await _db.Orders.CountAsync(o => o.Status == "Pending" || o.Status == "Confirmed");
        int delivering = await _db.Orders.CountAsync(o => o.Status == "Shipping");
        
        return (totalToday, pending, delivering);
    }

    public async Task<(List<Order> Orders, int ActiveDeliveries, int PendingPickup, int CompletedToday)> GetStaffDeliveriesAsync(string filter)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        int activeDeliveries = await _db.Orders.CountAsync(o => o.Status == "Shipping");
        int pendingPickup = await _db.Orders.CountAsync(o => (o.Status == "Confirmed" || o.Status == "Processing") && o.OrderDate >= today && o.OrderDate < tomorrow);
        int completedToday = await _db.Orders.CountAsync(o => o.Status == "Completed" && o.OrderDate >= today && o.OrderDate < tomorrow);

        var query = _db.Orders.Include(o => o.User).Include(o => o.OrderDetails).ThenInclude(od => od.Product).AsQueryable();

        if (filter == "active")
            query = query.Where(o => o.Status == "Shipping");
        else if (filter == "pending")
            query = query.Where(o => o.Status == "Confirmed" || o.Status == "Processing");
        else if (filter == "completed")
            query = query.Where(o => o.Status == "Completed" && o.OrderDate >= today && o.OrderDate < tomorrow);
        else
            query = query.Where(o => o.Status == "Shipping" || o.Status == "Confirmed" || o.Status == "Processing" || (o.Status == "Completed" && o.OrderDate >= today && o.OrderDate < tomorrow));

        var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

        return (orders, activeDeliveries, pendingPickup, completedToday);
    }

    // ---- Xem chi tiết 1 đơn hàng ----
    public async Task<Order?> GetByIdAsync(string id)
        => await _db.Orders
                    .Include(o => o.User)
                    .Include(o => o.Voucher)
                    .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(o => o.Id == id);

    // ---- Staff/Admin: Cập nhật trạng thái đơn hàng ----
    public async Task<bool> UpdateStatusAsync(string orderId, string newStatus)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return false;
        order.Status = newStatus;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Logic: Xử lý Đặt Hàng (Checkout)
    /// Cách hoạt động:
    /// 1. Áp dụng mã giảm giá (tính theo phần trăm hoặc số tiền cố định).
    /// 2. Trừ tồn kho (Inventory Deduction) cho Custom Pizza dựa trên ComponentName.
    /// 3. Khởi tạo sản phẩm ảo (Custom Product) nếu có ID dạng CTM.
    /// 4. Lưu Order và OrderDetails vào database.
    /// </summary>
    public async Task<Order> PlaceOrderAsync(string userId, string paymentMethod, string? voucherCode, IEnumerable<PizzaDeli.Controllers.CartItemDto> items, string? shippingAddress)
    {
        decimal totalAmount = items.Sum(i => i.Price * i.Quantity);
        decimal discountAmount = 0;
        int? voucherId = null;

        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode && v.IsActive);
            if (voucher != null)
            {
                voucherId = voucher.Id;
                if (voucher.DiscountAmount.HasValue && voucher.DiscountAmount > 0)
                    discountAmount = voucher.DiscountAmount.Value;
                else if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent > 0)
                    discountAmount = totalAmount * (voucher.DiscountPercent.Value / 100m);
            }
        }

        decimal finalAmount = Math.Max(0, totalAmount - discountAmount);

        var order = new Order
        {
            UserId        = userId,
            OrderDate     = DateTime.Now,
            TotalAmount   = totalAmount,
            DiscountAmount= discountAmount,
            FinalAmount   = finalAmount,
            PaymentMethod = paymentMethod ?? "COD",
            Status        = "Pending",
            ShippingAddress = shippingAddress ?? "Chưa cập nhật địa chỉ",
            VoucherId     = voucherId
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(); // Lưu để lấy order.Id

        var allComponents = await _db.PizzaComponents.ToListAsync();
        
        foreach (var item in items)
        {
            foreach (var component in allComponents)
            {
                if (item.Name.Contains(component.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (component.Stock >= item.Quantity)
                        component.Stock -= item.Quantity;
                    else
                        component.Stock = 0;
                }
            }

            var realId = item.Id.Contains("-") ? item.Id.Split('-')[0] : item.Id;
            var productExists = await _db.Products.AnyAsync(p => p.Id == realId);

            if (realId == "CUSTOM")
            {
                var pizzaCategoryId = await _db.Categories
                    .Where(c => c.Name.ToLower() == "pizza")
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();

                var customProduct = new Product
                {
                    Id = "CTM" + Guid.NewGuid().ToString("N").Substring(0, 7).ToUpper(),
                    Name = item.Name.Length > 100 ? item.Name.Substring(0, 97) + "..." : item.Name,
                    Description = item.Name,
                    Price = item.Price,
                    CategoryId = pizzaCategoryId,
                    ImageUrl = item.Image ?? "/images/pizza/custom-pizza.png",
                    IsAvailable = false,
                    CreatedAt = DateTime.Now
                };
                _db.Products.Add(customProduct);
                await _db.SaveChangesAsync();

                realId = customProduct.Id;
                productExists = true;
            }

            if (!productExists) continue;

            _db.OrderDetails.Add(new OrderDetail
            {
                OrderId   = order.Id,
                ProductId = realId,
                Quantity  = item.Quantity,
                UnitPrice = item.Price
            });
        }
        await _db.SaveChangesAsync();
        return order;
    }

    // ---- Admin: Thống kê tổng đơn hàng ----
    public async Task<int> CountTotalAsync() => await _db.Orders.CountAsync();

    // ---- Admin: Doanh thu theo ngày ----
    public async Task<decimal> GetRevenueByDateAsync(DateTime date)
        => await _db.Orders
                    .Where(o => o.OrderDate.Date == date.Date && o.Status == "Completed")
                    .SumAsync(o => o.FinalAmount);

    // ---- Sepay Webhook ----
    public async Task<Order?> GetByShortIdAsync(string shortId)
    {
        var upperShortId = shortId.ToUpper();
        return await _db.Orders.FirstOrDefaultAsync(o => o.Id.ToUpper().StartsWith(upperShortId));
    }

    /// <summary>
    /// Logic: Tự động đối soát thanh toán (SePay Webhook)
    /// Cách hoạt động: Được Webhook gọi đến khi nhận tiền. Tự động chuyển trạng thái đơn hàng sang Confirmed nếu đúng số tiền.
    /// </summary>
    public async Task<bool> ConfirmPaymentAsync(string orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return false;
        
        if (order.Status == "Pending" || order.Status == "Chờ xử lý")
        {
            order.Status = "Confirmed";
            await _db.SaveChangesAsync();
            return true;
        }
        return false;
    }
}
