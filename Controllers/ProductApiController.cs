using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Services;

[ApiController]
[Route("api/[controller]")]
public class ProductApiController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductApiController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _productService.GetAllAsync();
        return Ok(products);
    }
}