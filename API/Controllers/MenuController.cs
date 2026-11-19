using DGustAR.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace DGustAR.Api.Controllers;

[ApiController]
[Route("api")]
public class MenuController : ControllerBase
{
    private readonly IStoreRepository _db;

    public MenuController(IStoreRepository db)
    {
        _db = db;
    }

    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu()
    {
        try
        {
            var menu = await _db.GetMenuAsync();
            return Ok(menu);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error al obtener el menú", details = ex.Message });
        }
    }

    [HttpGet("products/{id}")]
    public IActionResult GetProduct(string id)
    {
        var product = _db.GetProductById(id);
        if (product == null)
        {
            return NotFound(new { error = "Producto no encontrado" });
        }
        return Ok(product);
    }
}
