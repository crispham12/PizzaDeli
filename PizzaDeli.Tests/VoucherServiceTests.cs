using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;
using PizzaDeli.Services;
using Xunit;

namespace PizzaDeli.Tests;

public class VoucherServiceTests
{
    // Tạo DB ảo trong RAM riêng biệt cho mỗi test (tránh conflict dữ liệu)
    private ApplicationDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    // ---------------------------------------------------------------
    // TC_001: Mã giảm giá hợp lệ, đơn hàng thỏa mãn điều kiện
    // ---------------------------------------------------------------
    [Fact]
    public async Task ValidateAsync_MaHopLeVaDuGiaTri_TraVeVoucher()
    {
        using var db = CreateDb("TC_001");
        db.Vouchers.Add(new Voucher
        {
            Code         = "GIAM100K",
            DiscountAmount = 100000,
            MinOrderValue  = 300000,
            IsActive       = true,
            ExpiryDate     = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var service = new VoucherService(db);
        var result  = await service.ValidateAsync("GIAM100K", 500000);

        Assert.NotNull(result);
        Assert.Equal(100000, result.DiscountAmount);
    }

    // ---------------------------------------------------------------
    // TC_002: Đơn hàng không đủ giá trị tối thiểu
    // ---------------------------------------------------------------
    [Fact]
    public async Task ValidateAsync_KhongDuGiaTri_TraVeNull()
    {
        using var db = CreateDb("TC_002");
        db.Vouchers.Add(new Voucher
        {
            Code          = "GIAM200K",
            DiscountAmount = 200000,
            MinOrderValue  = 500000,
            IsActive       = true,
            ExpiryDate     = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var service = new VoucherService(db);
        var result  = await service.ValidateAsync("GIAM200K", 200000); // Chỉ 200k < 500k

        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // TC_003: Mã giảm giá đã hết hạn
    // ---------------------------------------------------------------
    [Fact]
    public async Task ValidateAsync_HetHan_TraVeNull()
    {
        using var db = CreateDb("TC_003");
        db.Vouchers.Add(new Voucher
        {
            Code          = "HETHAN",
            DiscountAmount = 50000,
            MinOrderValue  = 0,
            IsActive       = true,
            ExpiryDate     = DateTime.UtcNow.AddDays(-1) // Hết hạn hôm qua
        });
        await db.SaveChangesAsync();

        var service = new VoucherService(db);
        var result  = await service.ValidateAsync("HETHAN", 500000);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // TC_004: Mã không tồn tại trong hệ thống
    // ---------------------------------------------------------------
    [Fact]
    public async Task ValidateAsync_MaKhongTonTai_TraVeNull()
    {
        using var db = CreateDb("TC_004");

        var service = new VoucherService(db);
        var result  = await service.ValidateAsync("MARAC123", 500000);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // TC_005: Tạo voucher mới thành công
    // ---------------------------------------------------------------
    [Fact]
    public async Task CreateAsync_TaoMoiHopLe_LuuVaoDb()
    {
        using var db = CreateDb("TC_005");
        var service = new VoucherService(db);

        var voucher = new Voucher
        {
            Code           = "NEWVOUCHER",
            DiscountAmount = 50000,
            MinOrderValue  = 200000,
            IsActive       = true
        };

        var result = await service.CreateAsync(voucher);

        Assert.NotNull(result);
        Assert.Equal(1, await db.Vouchers.CountAsync());
    }

    // ---------------------------------------------------------------
    // TC_006: Cập nhật (Update) voucher thành công
    // ---------------------------------------------------------------
    [Fact]
    public async Task UpdateAsync_SuaVoucher_ThanhCong()
    {
        using var db = CreateDb("TC_006");
        db.Vouchers.Add(new Voucher { Id = 1, Code = "OLD", DiscountAmount = 30000, IsActive = true });
        await db.SaveChangesAsync();

        var service = new VoucherService(db);
        var updated = await service.UpdateAsync(new Voucher { Id = 1, Code = "NEWCODE", DiscountAmount = 80000, IsActive = true });

        Assert.True(updated);
        var v = await db.Vouchers.FindAsync(1);
        Assert.Equal("NEWCODE", v!.Code);
    }

    // ---------------------------------------------------------------
    // TC_007: Xóa (vô hiệu hóa) voucher thành công
    // ---------------------------------------------------------------
    [Fact]
    public async Task DeleteAsync_VoHieuHoa_ThanhCong()
    {
        using var db = CreateDb("TC_007");
        db.Vouchers.Add(new Voucher { Id = 1, Code = "DEL", IsActive = true });
        await db.SaveChangesAsync();

        var service = new VoucherService(db);
        var deleted  = await service.DeleteAsync(1);

        Assert.True(deleted);
        var v = await db.Vouchers.FindAsync(1);
        Assert.False(v!.IsActive); // Phải bị tắt, không phải xóa hẳn
    }

    // ---------------------------------------------------------------
    // TC_008: Mã giảm giá bị vô hiệu hóa (IsActive = false)
    // ---------------------------------------------------------------
    [Fact]
    public async Task ValidateAsync_MaBiTat_TraVeNull()
    {
        using var db = CreateDb("TC_008");
        db.Vouchers.Add(new Voucher
        {
            Code          = "DISABLED",
            DiscountAmount = 100000,
            IsActive       = false, // Đã bị tắt
            ExpiryDate     = DateTime.UtcNow.AddDays(10)
        });
        await db.SaveChangesAsync();

        var service = new VoucherService(db);
        var result  = await service.ValidateAsync("DISABLED", 500000);

        Assert.Null(result);
    }
}
