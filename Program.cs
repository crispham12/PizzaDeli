using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Services;
using PizzaDeli.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- MVC ----
builder.Services.AddControllersWithViews();

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

// ---- Application Services ----
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<VoucherService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<UserService>();

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

    if (!db.Users.Any(u => u.Role == "Admin"))
    {
        db.Users.Add(new User
        {
            Id = Guid.NewGuid().ToString("N"),
            FullName = "Admin Quản Trị",
            Email = "admin@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123123"),
            Role = "Admin",
            Phone = "0999999999",
            IsActive = true,
            CreatedAt = DateTime.Now
        });
    }

    if (!db.Users.Any(u => u.Role == "Staff"))
    {
        db.Users.Add(new User
        {
            Id = Guid.NewGuid().ToString("N"),
            FullName = "Nhân viên",
            Email = "staff@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123123"),
            Role = "Staff",
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
