using Market.Dtos;
using Microsoft.AspNetCore.Mvc;
using Market.Implimitation.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Market.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly IProduct _product;

    public ProductController(IProduct product) => _product = product;

    [HttpGet]
    public async Task<ActionResult<ProductDto>> Get([FromQuery] ProductQueryDto query)
    {
        var res = await _product.GetProductsAsync(query);
        return Ok(res);
    }

    [Authorize(Roles = "Seller")]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] ProductCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var res = await _product.CreateProductAsync(dto);

        if (res == null)
            return BadRequest("Error creating product");

        return Ok(res);
    }

    [Authorize(Roles = "Seller,Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ProductDto>> Delete([FromRoute] int id)
    {
        var res = await _product.DeleteProductAsync(id);

        if (res == null)
            return NotFound("Product not found");

        return Ok(res);
    }

    [Authorize(Roles = "Seller,Admin")]
    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<ProductDto>> Restore([FromRoute] int id)
    {
        var res = await _product.RestoreProductAsync(id);

        if (res == null)
            return BadRequest("Cannot restore product");

        return Ok(res);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("hard/{id:int}")]
    public async Task<IActionResult> HardDelete([FromRoute] int id)
    {
        var ok = await _product.HardDeleteProductAsync(id);

        if (!ok)
            return NotFound("Product not found");

        return NoContent();
    }
}