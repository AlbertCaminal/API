using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CarSales.Api.Data;
using CarSales.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace CarSales.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // => /api/sales
public class SalesController : ControllerBase
{
    private readonly ISalesRepository _repo;

    public SalesController(ISalesRepository repo) => _repo = repo;

    // --------- GET LIST ----------
    [HttpGet]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetSales([FromQuery] SalesFilter filter, CancellationToken ct)
        => Ok(await _repo.GetSalesAsync(filter, ct));

    // --------- GET BY ID ----------
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SaleDto>> GetSaleById(int id, CancellationToken ct)
    {
        var sale = await _repo.GetByIdAsync(id, ct);
        if (sale is null) return NotFound(new { message = $"Sale with id {id} not found." });
        return Ok(sale);
    }

    // --------- CREATE (POST) ----------
    [HttpPost]
    [ServiceFilter(typeof(ApiKeyAuthFilter))]
    public async Task<ActionResult<SaleDto>> CreateSale([FromBody] CreateSaleRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var newId = await _repo.CreateAsync(body, ct);
        var created = await _repo.GetByIdAsync(newId, ct);

        return CreatedAtAction(nameof(GetSaleById), new { id = newId }, created);
    }

    // --------- UPDATE (PUT) ----------
    [HttpPut("{id:int}")]
    [ServiceFilter(typeof(ApiKeyAuthFilter))]
    public async Task<IActionResult> UpdateSale(int id, [FromBody] UpdateSaleRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ok = await _repo.UpdateAsync(id, body, ct);
        if (!ok) return NotFound(new { message = $"Sale with id {id} not found." });

        return NoContent();
    }

    // --------- DELETE ----------
    [HttpDelete("{id:int}")]
    [ServiceFilter(typeof(ApiKeyAuthFilter))]
    public async Task<IActionResult> DeleteSale(int id, CancellationToken ct)
    {
        var ok = await _repo.DeleteAsync(id, ct);
        if (!ok) return NotFound(new { message = $"Sale with id {id} not found." });

        return NoContent();
    }
}

// ============================================================================
// Filtro de autenticación por API Key (lee claves de appsettings.json)
// ============================================================================
public class ApiKeyAuthFilter : IAsyncActionFilter
{
    private readonly HashSet<string> _validKeys;
    private const string HeaderName = "X-API-Key";

    public ApiKeyAuthFilter(IConfiguration config)
    {
        var keys = config.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();
        _validKeys = new HashSet<string>(keys, StringComparer.Ordinal);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided))
        {
            context.Result = new ContentResult
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Content = $"Missing {HeaderName} header."
            };
            return;
        }

        var apiKey = provided.ToString();
        if (string.IsNullOrWhiteSpace(apiKey) || !_validKeys.Contains(apiKey))
        {
            context.Result = new ContentResult
            {
                StatusCode = (int)HttpStatusCode.Forbidden,
                Content = "Invalid API key."
            };
            return;
        }

        await next();
    }
}
