using DGustAR.Api.Data;
using DGustAR.Api.Models;
using DGustAR.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGustAR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly IStoreRepository _db;

    public OrdersController(OrderService orderService, IStoreRepository db)
    {
        _orderService = orderService;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto payload)
    {
        try
        {
            var createdOrder = await _orderService.CreateOrderAsync(payload);
            return StatusCode(201, new
            {
                success = true,
                message = "Pedido recibido con éxito por la cocina.",
                order = createdOrder
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Error interno procesando pedido", details = ex.Message });
        }
    }

    [HttpGet("{token}")]
    public IActionResult GetOrderByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Token de orden inválido." });
        }

        var order = _db.GetOrderByToken(token.Trim());
        if (order == null)
        {
            return NotFound(new { error = "No se encontró ninguna orden con el token provisto." });
        }

        return Ok(order);
    }
}
