using Microsoft.AspNetCore.Mvc;

namespace PizzaDeli.Controllers;

/// <summary>Nhân viên: Quản lý đơn hàng, Xử lý giao hàng, Hỗ trợ KH, Quản lý bình luận</summary>
public class StaffController : BaseController
{
    private IActionResult? Guard() => RequireRole("Staff", "Admin");

    // ---- Dashboard ----
    public IActionResult Dashboard()
    {
        var g = Guard(); if (g != null) return g;
        return View();
    }

    // ---- Quản lý đơn hàng ----
    public IActionResult Orders()               { var g = Guard(); if (g != null) return g; return View(); }
    public IActionResult OrderDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    [HttpPost]
    public IActionResult UpdateOrderStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API cập nhật trạng thái đơn hàng
        TempData["Success"] = $"Đã cập nhật đơn hàng #{id} → {status}";
        return RedirectToAction("Orders");
    }

    // ---- Xử lý giao hàng ----
    public IActionResult Deliveries()           { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult UpdateDeliveryStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        TempData["Success"] = $"Đã cập nhật giao hàng #{id} → {status}";
        return RedirectToAction("Deliveries");
    }

    // ---- Hỗ trợ khách hàng ----
    public IActionResult Customers()            { var g = Guard(); if (g != null) return g; return View(); }
    public IActionResult CustomerDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    // ---- Quản lý bình luận ----
    public IActionResult Comments()             { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult HideComment(string id)
    {
        var g = Guard(); if (g != null) return g;
        TempData["Success"] = "Đã ẩn bình luận vi phạm.";
        return RedirectToAction("Comments");
    }

    [HttpPost]
    public IActionResult ReplyComment(string id, string reply)
    {
        var g = Guard(); if (g != null) return g;
        TempData["Success"] = "Đã trả lời bình luận.";
        return RedirectToAction("Comments");
    }
}
