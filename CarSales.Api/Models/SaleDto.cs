namespace CarSales.Api.Models;

public class SaleDto
{
    public int Id { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public decimal? EngineSize { get; set; }
    public string? FuelType { get; set; }
    public int Year { get; set; }
    public int? Mileage { get; set; }
    public decimal? Price { get; set; }

    // Dapper necesita constructor por defecto para materializar
    public SaleDto() { }
}
