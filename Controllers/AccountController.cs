using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Models;
using PizzaDeli.Services;

namespace PizzaDeli.Controllers;

public class AccountController : BaseController
{
    private readonly AuthService _auth;
    public AccountController(AuthService auth) => _auth = auth;

    // ==================== LOGIN ====================
    [HttpGet]
    public IActionResult Login()
    {
        if (IsLoggedIn) return RedirectToRoleDashboard();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest req, string? returnUrl)
    {
        if (!ModelState.IsValid) return View(req);

        var (ok, token, user, error) = await _auth.LoginAsync(req.Email, req.Password);

        if (!ok || token is null || user is null)
        {
            ViewBag.Error = error ?? "Đăng nhập thất bại";
            return View(req);
        }

        SetSession(token, user);

        // Phân luồng theo role
        return user.Role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Staff" => RedirectToAction("Dashboard", "Staff"),
            _       => string.IsNullOrEmpty(returnUrl)
                          ? RedirectToAction("Index", "Home")
                          : Redirect(returnUrl)
        };
    }

    // ==================== REGISTER ====================
    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn) return RedirectToRoleDashboard();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (!ModelState.IsValid) return View(req);

        var (ok, error) = await _auth.RegisterAsync(req);
        if (!ok)
        {
            ViewBag.Error = error;
            return View(req);
        }

        TempData["Success"] = "Đăng ký thành công! Vui lòng đăng nhập.";
        return RedirectToAction("Login");
    }

    // ==================== LOGOUT ====================
    public IActionResult Logout()
    {
        ClearSession();
        return RedirectToAction("Login");
    }

    // ==================== ACCESS DENIED ====================
    public IActionResult AccessDenied() => View();
}
