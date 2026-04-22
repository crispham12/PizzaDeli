using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Services;
using PizzaDeli.Models;

namespace PizzaDeli.Controllers;

/// <summary>Nhân viên: Quản lý đơn hàng, Xử lý giao hàng, Hỗ trợ KH, Quản lý bình luận</summary>
public class StaffController : BaseController
{
    private readonly PizzaDeli.Services.OrderService _orderService;
    private readonly PizzaDeli.Services.ReviewService _reviewService;

    public StaffController(PizzaDeli.Services.OrderService orderService, PizzaDeli.Services.ReviewService reviewService)
    {
        _orderService = orderService;
        _reviewService = reviewService;
    }

    private IActionResult? Guard() => RequireRole("Staff", "Admin");


    // ---- Quản lý đơn hàng ----
    public async Task<IActionResult> Orders(int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        var stats = await _orderService.GetStaffOrderStatsAsync();
        ViewBag.TotalToday = stats.TotalToday;
        ViewBag.PendingOrders = stats.Pending;
        ViewBag.DeliveringOrders = stats.Delivering;

        int pageSize = 15;
        var (orders, totalPages) = await _orderService.GetStaffOrdersPagedAsync(page, pageSize);

        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;

        return View(orders);
    }
    
    public async Task<IActionResult> OrderDetail(string id) 
    { 
        var g = Guard(); if (g != null) return g; 
        
        var order = await _orderService.GetByIdAsync(id);

        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        
        var order = await _orderService.GetByIdAsync(id);
        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction("Orders");
        }

        var normalizedCurrent = NormalizeStatus(order.Status);
        var normalizedTarget = NormalizeStatus(status);

        bool canUpdate = (normalizedCurrent, normalizedTarget) switch
        {
            // Staff quản lý đơn chỉ đi theo chiều tiến trong bếp
            ("Pending", "Confirmed") => true,
            ("Confirmed", "Processing") => true,
            // Cho phép hủy ở bước đầu
            ("Pending", "Cancelled") => true,
            ("Confirmed", "Cancelled") => true,
            _ => false
        };

        if (!canUpdate)
        {
            TempData["ErrorMessage"] = "Không thể cập nhật trạng thái này trong mục Quản lý đơn hàng.";
            return RedirectToAction("Orders");
        }

        await _orderService.UpdateStatusAsync(id, normalizedTarget);
        TempData["SuccessMessage"] = $"Đã cập nhật đơn hàng #{id.Substring(0, 8).ToUpper()} → {normalizedTarget}";
        return RedirectToAction("Orders");
    }

    // ---- Xử lý giao hàng ----
    public async Task<IActionResult> Deliveries(string filter = "all")
    {
        var g = Guard(); if (g != null) return g;

        var result = await _orderService.GetStaffDeliveriesAsync(filter);
        
        ViewBag.ActiveDeliveries = result.ActiveDeliveries;
        ViewBag.PendingPickup = result.PendingPickup;
        ViewBag.CompletedToday = result.CompletedToday;
        ViewBag.CurrentFilter = filter;

        return View(result.Orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDeliveryStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        var order = await _orderService.GetByIdAsync(id);
        if (order == null)
        {
            TempData["Error"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction("Deliveries");
        }

        // Luồng giao hàng chặt chỉ áp dụng cho COD
        var payment = (order.PaymentMethod ?? string.Empty).Trim().ToLowerInvariant();
        var isCod = payment == "cod";
        var normalizedTarget = NormalizeStatus(status);
        if (!isCod)
        {
            // Đơn bank giữ logic cũ: cho phép cập nhật trực tiếp trạng thái
            await _orderService.UpdateStatusAsync(id, normalizedTarget);
            TempData["Success"] = $"Đã cập nhật giao hàng #{id.Substring(0, 8).ToUpper()} → {normalizedTarget}";
            return RedirectToAction("Deliveries");
        }

        var normalizedCurrent = NormalizeStatus(order.Status);

        bool canUpdate = (normalizedCurrent, normalizedTarget) switch
        {
            ("Confirmed", "Shipping") => true,
            ("Processing", "Shipping") => true,
            ("Shipping", "Completed") => true,
            ("Confirmed", "Cancelled") => true,
            ("Processing", "Cancelled") => true,
            ("Shipping", "Cancelled") => true,
            _ => false
        };

        if (!canUpdate)
        {
            TempData["Error"] = "Không thể cập nhật trạng thái giao hàng theo yêu cầu.";
            return RedirectToAction("Deliveries");
        }

        await _orderService.UpdateStatusAsync(id, normalizedTarget);
        TempData["Success"] = $"Đã cập nhật giao hàng #{id.Substring(0, 8).ToUpper()} → {normalizedTarget}";
        return RedirectToAction("Deliveries");
    }

    private static string NormalizeStatus(string? status)
    {
        var s = (status ?? string.Empty).Trim();
        return s.ToLowerInvariant() switch
        {
            "pending" or "chờ xử lý" => "Pending",
            "confirmed" or "đã xác nhận" => "Confirmed",
            "processing" or "đang chế biến" => "Processing",
            "shipping" or "đang giao hàng" => "Shipping",
            "completed" or "đã hoàn thành" => "Completed",
            "cancelled" or "đã hủy" => "Cancelled",
            _ => s
        };
    }



    // ---- Quản lý bình luận ----
    public async Task<IActionResult> Comments(int rating = 0)
    {
        var g = Guard(); if (g != null) return g;
        
        var result = await _reviewService.GetStaffReviewsAsync(rating);

        ViewBag.TotalReviews = result.Total;
        ViewBag.AverageRating = result.AverageRating;
        ViewBag.PendingReplies = result.PendingReplies;
        ViewBag.CurrentRatingFilter = rating;
        
        return View(result.Reviews);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleVisibility(int id)
    {
        var g = Guard(); if (g != null) return g;
        var result = await _reviewService.ToggleVisibilityAsync(id);
        if (result) {
            TempData["Success"] = "Cập nhật trạng thái hiển thị bình luận thành công.";
        }
        return RedirectToAction("Comments");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var g = Guard(); if (g != null) return g;
        var result = await _reviewService.DeleteAsync(id);
        if (result) {
            TempData["Success"] = "Đã xóa vĩnh viễn bình luận.";
        }
        return RedirectToAction("Comments");
    }

    [HttpPost]
    public async Task<IActionResult> ReplyComment(int id, string reply)
    {
        var g = Guard(); if (g != null) return g;
        var result = await _reviewService.ReplyAsync(id, reply);
        if (result) {
            TempData["Success"] = "Đã phản hồi bình luận thành công.";
        }
        return RedirectToAction("Comments");
    }
}
