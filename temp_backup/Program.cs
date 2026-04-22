using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Services;
using PizzaDeli.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- MVC ----
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Session ----
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(8);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
    opt.Cookie.SameSite = SameSiteMode.Lax;
});

// ---- HttpClient for API ----
builder.Services.AddHttpClient("API");
builder.Services.AddScoped<AuthService>();

// Đăng ký các Service xử lý nghiệp vụ (Tuân thủ mô hình Controller -> Service -> DbContext)
builder.Services.AddScoped<PizzaDeli.Services.CategoryService>();
builder.Services.AddScoped<PizzaDeli.Services.ProductService>();
builder.Services.AddScoped<PizzaDeli.Services.AuthService>();
builder.Services.AddScoped<PizzaDeli.Services.UserService>();
builder.Services.AddScoped<PizzaDeli.Services.VoucherService>();
builder.Services.AddScoped<PizzaDeli.Services.OrderService>();
builder.Services.AddScoped<PizzaDeli.Services.ReviewService>();
builder.Services.AddScoped<PizzaDeli.Services.DashboardService>();
builder.Services.AddScoped<PizzaDeli.Services.AdminService>();
builder.Services.AddSingleton<PizzaDeli.Services.AiIntegratorService>();
builder.Services.AddScoped<AiIntegratorService>();

// ---- Database ----
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();
// ---- Middleware pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();          // must be BEFORE Authorization & routing
app.UseAuthorization();

// Route MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---- Database Seeding ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // Đảm bảo database đã được tạo

    if (!db.Users.Any(u => u.Role == UserRole.Admin))
    {
        db.Users.Add(new User
        {
            Id = Guid.NewGuid().ToString("N"),
            FullName = "Admin Quản Trị",
            Email = "admin@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123123"),
            Role = UserRole.Admin,
            Phone = "0999999999",
            IsActive = true,
            CreatedAt = DateTime.Now
        });
    }

    if (!db.Users.Any(u => u.Role == UserRole.Staff))
    {
        db.Users.Add(new User
        {
            Id = Guid.NewGuid().ToString("N"),
            FullName = "Nhân viên",
            Email = "staff@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123123"),
            Role = UserRole.Staff,
            Phone = "0888888888",
            IsActive = true,
            CreatedAt = DateTime.Now
        });
    }
    
    db.SaveChanges();
}

// ⚠️ Quan trọng: chỉ set port khi deploy (Render)
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.Run();
