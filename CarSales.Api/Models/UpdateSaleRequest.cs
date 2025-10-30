using System.ComponentModel.DataAnnotations;

namespace CarSales.Api.Models;

public class UpdateSaleRequest
{
	[Required, StringLength(100)]
	public string Manufacturer { get; set; } = string.Empty;

	[Required, StringLength(100)]
	public string Model { get; set; } = string.Empty;

	[Range(0, 20)]
	public decimal EngineSize { get; set; }

	[Required, StringLength(50)]
	public string FuelType { get; set; } = string.Empty;

	[Range(1900, 2100)]
	public int Year { get; set; }

	[Range(0, 2_000_000)]
	public int? Mileage { get; set; }

	[Range(0, 10_000_000)]
	public decimal Price { get; set; }
}
