using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Models;
using PizzaDeli.Services;
using System.Text.RegularExpressions;

namespace PizzaDeli.Controllers;

public class AccountController : BaseController
{
    private readonly AuthService _auth;
    private readonly UserService _userService;

    public AccountController(AuthService auth, UserService userService)
    {
        _auth = auth;
        _userService = userService;
    }

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
            "Staff" => RedirectToAction("Orders", "Staff"),
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
        // Fallback bind số điện thoại trong trường hợp client gửi key khác tên property
        if (string.IsNullOrWhiteSpace(req.Phone))
        {
            req.Phone = Request.Form["Phone"].FirstOrDefault()
                     ?? Request.Form["phone"].FirstOrDefault()
                     ?? Request.Form["PhoneNumber"].FirstOrDefault()
                     ?? string.Empty;
        }
        req.Phone = req.Phone?.Trim() ?? string.Empty;
        req.ConfirmPassword = string.IsNullOrWhiteSpace(req.ConfirmPassword)
            ? (Request.Form["ConfirmPassword"].FirstOrDefault() ?? string.Empty)
            : req.ConfirmPassword;

        if (string.IsNullOrWhiteSpace(req.FullName)
            || string.IsNullOrWhiteSpace(req.Email)
            || string.IsNullOrWhiteSpace(req.Password)
            || string.IsNullOrWhiteSpace(req.ConfirmPassword)
            || string.IsNullOrWhiteSpace(req.Phone))
        {
            ViewBag.Error = "Vui lòng nhập đầy đủ thông tin đăng ký.";
            return View(req);
        }

        if (req.Password != req.ConfirmPassword)
        {
            ViewBag.Error = "Mật khẩu xác nhận không khớp.";
            return View(req);
        }

        if (req.Password.Length < 6)
        {
            ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
            return View(req);
        }

        if (!Regex.IsMatch(req.Phone, "^0\\d{9}$"))
        {
            ViewBag.Error = "Số điện thoại không hợp lệ. Vui lòng nhập 10 số và bắt đầu bằng số 0.";
            return View(req);
        }

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

    // ==================== FORGOT PASSWORD ====================
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (IsLoggedIn) return RedirectToRoleDashboard();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendForgotPasswordOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Json(new { success = false, message = "Vui lòng nhập email!" });

        var user = await _userService.GetUserByEmailExactAsync(email);
        if (user == null)
            return Json(new { success = false, message = "Email không tồn tại trong hệ thống!" });

        Random rnd = new Random();
        string code = rnd.Next(100000, 999999).ToString();

        HttpContext.Session.SetString("FPWD_OTP", code);
        HttpContext.Session.SetString("FPWD_EMAIL", email.Trim().ToLower());
        HttpContext.Session.SetString("FPWD_OTP_VALIDATED", "false");

        try 
        {
            var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential("phamanhkhoa56789@gmail.com", "ejff yemk uhwd wdtv")
            };

            var mailMessage = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress("pizzadelidemo1@gmail.com", "PizzaDeli Security"),
                Subject = "PizzaDeli - Khôi phục Mật khẩu",
                Body = $"<h2>Yêu cầu khôi phục mật khẩu tài khoản {user.FullName}</h2><hr/>" +
                       $"<p>Chào bạn,</p>" +
                       $"<p>Đây là mã xác nhận (OTP) 6 chữ số để lấy lại mật khẩu của bạn:</p>" +
                       $"<h1 style='color: #16a34a; letter-spacing: 0.2em;'>{code}</h1>" +
                       $"<p>Vui lòng tuyệt đối không tiết lộ mã này cho bất kỳ ai.</p>",
                IsBodyHtml = true,
            };
            mailMessage.To.Add(user.Email);
            client.Send(mailMessage);

            return Json(new { success = true, message = "Mã xác nhận đã được gửi về email của bạn!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Gặp lỗi hệ thống gửi mail: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyForgotPasswordOtp(string email, string code)
    {
        var savedOtp = HttpContext.Session.GetString("FPWD_OTP");
        var savedEmail = HttpContext.Session.GetString("FPWD_EMAIL");

        if (string.IsNullOrEmpty(savedOtp) || string.IsNullOrEmpty(savedEmail))
            return Json(new { success = false, message = "Phiên làm việc không hợp lệ hoặc đã hết hạn!" });

        if (savedEmail != email.Trim().ToLower())
            return Json(new { success = false, message = "Thông tin email không khớp!" });

        if (savedOtp != code)
            return Json(new { success = false, message = "Mã xác nhận không hợp lệ!" });

        HttpContext.Session.SetString("FPWD_OTP_VALIDATED", "true");
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string email, string newPassword)
    {
        var savedEmail = HttpContext.Session.GetString("FPWD_EMAIL");
        var isValidated = HttpContext.Session.GetString("FPWD_OTP_VALIDATED");

        if (isValidated != "true" || string.IsNullOrEmpty(savedEmail) || savedEmail != email.Trim().ToLower())
        {
            TempData["Error"] = "Bạn chưa hoàn tất xác minh mã OTP hợp lệ!";
            return RedirectToAction("ForgotPassword");
        }

        bool ok = await _userService.ResetPasswordByEmailAsync(savedEmail, newPassword);
        if (!ok)
        {
            TempData["Error"] = "Tài khoản không tồn tại!";
            return RedirectToAction("ForgotPassword");
        }

        HttpContext.Session.Remove("FPWD_OTP");
        HttpContext.Session.Remove("FPWD_EMAIL");
        HttpContext.Session.Remove("FPWD_OTP_VALIDATED");

        TempData["Success"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập.";
        return RedirectToAction("Login");
    }

    // ==================== ACCESS DENIED ====================
    public IActionResult AccessDenied() => View();
}
