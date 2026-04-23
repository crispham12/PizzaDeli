using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Services;
using PizzaDeli.Models;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// ---------------- MVC ----------------
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------- Session ----------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromMinutes(15);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
    opt.Cookie.SameSite = SameSiteMode.Lax;
});

// ---------------- HttpClient ----------------
builder.Services.AddHttpClient("API");

// ---------------- Services ----------------
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<VoucherService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddSingleton<AiIntegratorService>();

// ---------------- Database ----------------
var conn =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION");

if (string.IsNullOrEmpty(conn))
    throw new Exception("Missing DB connection string");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(conn));

var app = builder.Build();

// ---------------- Middleware ----------------
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

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---------------- DB Init (SAFE VERSION) ----------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        // ⚠️ chỉ dùng khi chắc chắn DB OK
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB Migration failed: " + ex.Message);
    }

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

app.Run();