namespace PizzaDeli.Models;
using System.ComponentModel.DataAnnotations;

// ---------- Request DTOs ----------
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;
    [Required]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string Password { get; set; } = string.Empty;
    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
    [Required]
    [RegularExpression("^0\\d{9}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string Phone { get; set; } = string.Empty;
}

// ---------- Response DTOs ----------
public class AuthResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }
    public UserInfo? User { get; set; }
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;   // "Admin" | "Staff" | "Customer"
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
}

// ---------- Session Helper ----------
public static class SessionKeys
{
    public const string Token    = "JWT_TOKEN";
    public const string UserId   = "USER_ID";
    public const string UserName = "USER_NAME";
    public const string UserRole = "USER_ROLE";
    public const string UserEmail = "USER_EMAIL";
}
