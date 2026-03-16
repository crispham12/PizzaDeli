using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Models;

namespace PizzaDeli.Controllers;

/// <summary>
/// Base controller: cung cấp helper đọc session & check role
/// </summary>
public abstract class BaseController : Controller
{
    protected string? CurrentToken   => HttpContext.Session.GetString(SessionKeys.Token);
    protected string? CurrentRole    => HttpContext.Session.GetString(SessionKeys.UserRole);
    protected string? CurrentName    => HttpContext.Session.GetString(SessionKeys.UserName);
    protected string? CurrentEmail   => HttpContext.Session.GetString(SessionKeys.UserEmail);
    protected string? CurrentUserId  => HttpContext.Session.GetString(SessionKeys.UserId);
    protected bool IsLoggedIn        => !string.IsNullOrEmpty(CurrentToken);

    protected void SetSession(string token, UserInfo user)
    {
        HttpContext.Session.SetString(SessionKeys.Token,     token);
        HttpContext.Session.SetString(SessionKeys.UserId,    user.Id);
        HttpContext.Session.SetString(SessionKeys.UserName,  user.FullName);
        HttpContext.Session.SetString(SessionKeys.UserEmail, user.Email);
        HttpContext.Session.SetString(SessionKeys.UserRole,  user.Role);
    }

    protected void ClearSession() => HttpContext.Session.Clear();

    /// <summary>Nếu chưa login → redirect về trang login</summary>
    protected IActionResult? RequireLogin()
    {
        if (!IsLoggedIn)
            return RedirectToAction("Login", "Account");
        return null;
    }

    /// <summary>Nếu không đúng role → redirect về Dashboard tương ứng</summary>
    protected IActionResult? RequireRole(params string[] roles)
    {
        var check = RequireLogin();
        if (check != null) return check;
        if (!roles.Contains(CurrentRole))
            return RedirectToRoleDashboard();
        return null;
    }

    protected IActionResult RedirectToRoleDashboard() => CurrentRole switch
    {
        "Admin"    => RedirectToAction("Dashboard", "Admin"),
        "Staff"    => RedirectToAction("Dashboard", "Staff"),
        _          => RedirectToAction("Index",     "Home")
    };
}
