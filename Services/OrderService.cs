using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Đơn hàng - Customer đặt hàng, Staff/Admin quản lý</summary>
public class OrderService
{
    private readonly ApplicationDbContext _db;
    private readonly CartService _cart;
    private readonly VoucherService _voucher;

    public OrderService(ApplicationDbContext db, CartService cart, VoucherService voucher)
    {
        _db      = db;
        _cart    = cart;
        _voucher = voucher;
    }

    // ---- Customer: Đặt hàng từ giỏ hàng ----
    public async Task<Order?> PlaceOrderAsync(string userId, string shippingAddress,
                                              string paymentMethod, string? voucherCode)
    {
        var cartItems = await _cart.GetCartAsync(userId);
        if (!cartItems.Any()) return null;

        var totalAmount = cartItems.Sum(c => c.Quantity * c.Product!.Price);
        decimal discount = 0;

        // Áp dụng Voucher nếu có
        Voucher? voucher = null;
        if (!string.IsNullOrEmpty(voucherCode))
        {
            voucher = await _voucher.ValidateAsync(voucherCode, totalAmount);
            if (voucher != null)
                discount = voucher.DiscountAmount ?? 0;
        }

        var order = new Order
        {
            Id              = Guid.NewGuid().ToString("N"),
            UserId          = userId,
            ShippingAddress = shippingAddress,
            PaymentMethod   = paymentMethod,
            TotalAmount     = totalAmount,
            DiscountAmount  = discount,
            FinalAmount     = totalAmount - discount,
            VoucherId       = voucher?.Id,
            Status          = "Pending",
            OrderDate       = DateTime.Now
        };

        // Chi tiết đơn hàng
        order.OrderDetails = cartItems.Select(c => new OrderDetail
        {
            OrderId   = order.Id,
            ProductId = c.ProductId,
            Quantity  = c.Quantity,
            UnitPrice = c.Product!.Price
        }).ToList();

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Xóa giỏ hàng sau khi đặt thành công
        await _cart.ClearCartAsync(userId);

        return order;
    }

    // ---- Customer: Xem lịch sử đơn hàng của mình ----
    public async Task<List<Order>> GetByUserAsync(string userId)
        => await _db.Orders
                    .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                    .Include(o => o.Voucher)
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

    // ---- Staff/Admin: Lấy tất cả đơn hàng ----
    public async Task<List<Order>> GetAllAsync()
        => await _db.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderDetails)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

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

    // ---- Admin: Thống kê tổng đơn hàng ----
    public async Task<int> CountTotalAsync() => await _db.Orders.CountAsync();

    // ---- Admin: Doanh thu theo ngày ----
    public async Task<decimal> GetRevenueByDateAsync(DateTime date)
        => await _db.Orders
                    .Where(o => o.OrderDate.Date == date.Date && o.Status == "Completed")
                    .SumAsync(o => o.FinalAmount);
}
