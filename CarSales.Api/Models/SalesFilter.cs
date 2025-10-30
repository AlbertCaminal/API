namespace CarSales.Api.Models;

public class SalesFilter
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? FuelType { get; set; }
    public int? Year { get; set; }
    public decimal? EngineSizeMin { get; set; }
    public decimal? EngineSizeMax { get; set; }
    public int? MileageMin { get; set; }
    public int? MileageMax { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SortBy { get; set; } = "id";   // columnas permitidas (whitelist abajo)
    public string? SortDir { get; set; } = "asc"; // asc|desc
}
