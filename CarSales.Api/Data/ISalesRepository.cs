using System.Threading;
using System.Threading.Tasks;
using CarSales.Api.Models;

namespace CarSales.Api.Data;

public interface ISalesRepository
{
    // Lecturas
    Task<PagedResult<SaleDto>> GetSalesAsync(SalesFilter filter, CancellationToken ct);
    Task<SaleDto?> GetByIdAsync(int id, CancellationToken ct);

    // Escrituras (CRUD)
    Task<int> CreateAsync(CreateSaleRequest req, CancellationToken ct);
    Task<bool> UpdateAsync(int id, UpdateSaleRequest req, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
