using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Data;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;

namespace PizzaDeli.Controllers;

/// <summary>Customer: Xem menu, Đặt hàng, Giỏ hàng, Thông tin cá nhân, Voucher, Bình luận</summary>
public class CustomerController : BaseController
{
    private readonly ApplicationDbContext _context;

    public CustomerController(ApplicationDbContext context)
    {
        _context = context;
    }

    private IActionResult? Guard() => RequireLogin();

    // ---- Thông tin cá nhân ----
    public IActionResult Profile()
    {
        var g = Guard(); if (g != null) return g;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Profile(string fullName, string phone)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API cập nhật thông tin
        TempData["Success"] = "Cập nhật thông tin thành công!";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(string oldPassword, string newPassword)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API đổi mật khẩu
        TempData["Success"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("Profile");
    }

    // ---- Giỏ hàng ----
    public IActionResult Cart()                 { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult AddToCart(string productId, int qty = 1)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API thêm vào giỏ
        return Json(new { success = true, message = "Đã thêm vào giỏ hàng!" });
    }

    [HttpPost]
    public IActionResult RemoveFromCart(string cartItemId)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API xóa khỏi giỏ
        TempData["Success"] = "Đã xóa món ăn khỏi giỏ hàng.";
        return RedirectToAction("Cart");
    }

    // ---- Đặt hàng ----
    public IActionResult Checkout()             { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PlaceOrder(string paymentMethod, string? voucherCode, string? cartJson, string? shippingAddress)
    {
        if (!IsLoggedIn)
            return Json(new { success = false, message = "Vui lòng đăng nhập để đặt hàng." });

        // Parse giỏ hàng từ JSON gửi lên
        if (string.IsNullOrWhiteSpace(cartJson))
        {
            return Json(new { success = false, message = "Giỏ hàng trống, không thể đặt hàng." });
        }

        List<CartItemDto>? items;
        try
        {
            items = System.Text.Json.JsonSerializer.Deserialize<List<CartItemDto>>(cartJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return Json(new { success = false, message = "Dữ liệu giỏ hàng không hợp lệ." });
        }

        if (items == null || items.Count == 0)
        {
            return Json(new { success = false, message = "Giỏ hàng trống, không thể đặt hàng." });
        }

        // Tính tổng tiền
        decimal totalAmount = 0;
        foreach (var item in items)
            totalAmount += item.Price * item.Quantity;

        // Tính giảm giá từ voucher (nếu có)
        decimal discountAmount = 0;
        int? voucherId = null;
        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            var voucher = _context.Vouchers.FirstOrDefault(v => v.Code == voucherCode && v.IsActive);
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

        // Tạo đơn hàng
        var order = new PizzaDeli.Models.Order
        {
            UserId        = CurrentUserId!,
            OrderDate     = DateTime.Now,
            TotalAmount   = totalAmount,
            DiscountAmount= discountAmount,
            FinalAmount   = finalAmount,
            PaymentMethod = paymentMethod ?? "COD",
            Status        = "Pending",
            ShippingAddress = shippingAddress ?? "Chưa cập nhật địa chỉ",
            VoucherId     = voucherId
        };

        _context.Orders.Add(order);
        _context.SaveChanges(); // Lưu để lấy order.Id

        // Thêm chi tiết đơn hàng
        foreach (var item in items)
        {
            // Kiểm tra sản phẩm tồn tại trong DB
            var productExists = _context.Products.Any(p => p.Id == item.Id);
            if (!productExists) continue;

            _context.OrderDetails.Add(new PizzaDeli.Models.OrderDetail
            {
                OrderId   = order.Id,
                ProductId = item.Id,
                Quantity  = item.Quantity,
                UnitPrice = item.Price
            });
        }

        _context.SaveChanges();

        return Json(new { success = true, orderId = order.Id.Substring(0, 8).ToUpper() });
    }

    // ---- Lịch sử đơn hàng ----
    public IActionResult Orders()
    {
        var g = Guard(); if (g != null) return g;
        var uid = CurrentUserId;
        var orders = _context.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
            .Where(o => o.UserId == uid)
            .OrderByDescending(o => o.OrderDate)
            .ToList();
        return View(orders);
    }
    public IActionResult OrderDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelOrder(string id)
    {
        var g = Guard(); if (g != null) return g;
        var order = _context.Orders.FirstOrDefault(o => o.Id == id && o.UserId == CurrentUserId);
        if (order == null || order.Status != "Pending")
        {
            TempData["Error"] = "Không thể hủy đơn hàng này.";
            return RedirectToAction("Orders");
        }
        order.Status = "Cancelled";
        _context.SaveChanges();
        TempData["Success"] = "Đã hủy đơn hàng thành công.";
        return RedirectToAction("Orders");
    }

    // ---- Voucher ----
    public IActionResult Vouchers()             { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult ApplyVoucher(string code, decimal currentSubtotal)
    {
        var g = Guard(); if (g != null) return g;
        
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { success = false, message = "Vui lòng nhập mã." });

        var voucher = _context.Vouchers.FirstOrDefault(v => v.Code == code && v.IsActive);
        if (voucher == null)
            return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã bị khóa." });

        // Check date
        if (voucher.StartDate.HasValue && voucher.StartDate > DateTime.Now)
            return Json(new { success = false, message = "Mã này chưa đến thời gian áp dụng." });

        if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate < DateTime.Now)
            return Json(new { success = false, message = "Mã giảm giá đã quá hạn." });

        // Check MinOrderValue
        if (voucher.MinOrderValue > currentSubtotal)
            return Json(new { success = false, message = $"Đơn hàng phải từ {voucher.MinOrderValue:N0} ₫ để dùng mã này." });

        // Tính toán discount
        decimal discountAmount = 0;
        if (voucher.DiscountAmount.HasValue && voucher.DiscountAmount > 0)
        {
            discountAmount = voucher.DiscountAmount.Value;
        }
        else if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent > 0)
        {
            // Trừ theo phần trăm
            discountAmount = currentSubtotal * (voucher.DiscountPercent.Value / 100);
        }

        return Json(new { success = true, discount = discountAmount, message = "Áp dụng mã giảm giá thành công!" });
    }

    // ---- Bình luận ----
    [HttpPost]
    public IActionResult AddComment(string productId, string content, int rating)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API thêm bình luận
        return Json(new { success = true });
    }
    
    [HttpGet]
    public IActionResult ProductReviews()
    {
        var g = Guard(); if (g != null) return g;
        
        var uid = CurrentUserId;
        var orders = _context.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
            .Where(o => o.UserId == uid)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View(orders);
    }

    [HttpGet]
    public IActionResult ReviewDetail(string id)
    {
        var product = _context.Products.Find(id);
        if (product == null) return NotFound();
        return View(product);
    }
}

// DTO để parse JSON giỏ hàng từ client
public class CartItemDto
{
    public string Id        { get; set; } = string.Empty;
    public string Name      { get; set; } = string.Empty;
    public decimal Price    { get; set; }
    public int Quantity     { get; set; } = 1;
    public string? Image    { get; set; }
}
