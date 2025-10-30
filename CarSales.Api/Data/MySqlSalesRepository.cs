using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CarSales.Api.Models;
using Dapper;
using MySqlConnector;

namespace CarSales.Api.Data;

public class MySqlSalesRepository : ISalesRepository
{
    private readonly string _cs;

    private static readonly Dictionary<string, string> OrderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "id",         "l.listing_id" },
        { "manufacturer","mfr.name" },
        { "model",      "md.name" },
        { "enginesize", "l.engine_size" },
        { "fueltype",   "ft.name" },
        { "year",       "l.year" },
        { "mileage",    "l.mileage" },
        { "price",      "l.price" }
    };

    public MySqlSalesRepository(string connectionString) => _cs = connectionString;

    private IDbConnection Open() => new MySqlConnection(_cs);

    // -----------------------
    //      READ (GET)
    // -----------------------
    public async Task<PagedResult<SaleDto>> GetSalesAsync(SalesFilter f, CancellationToken ct)
    {
        using var conn = Open();

        var sqlBase = @"
FROM listings l
LEFT JOIN fuel_types ft ON ft.fuel_type_id = l.fuel_type_id
LEFT JOIN models md ON md.model_id = l.model_id
LEFT JOIN manufacturers mfr ON mfr.manufacturer_id = md.manufacturer_id
WHERE 1=1
";

        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(f.Manufacturer))
        {
            sqlBase += " AND mfr.name LIKE @Manufacturer ";
            p.Add("@Manufacturer", $"%{f.Manufacturer}%");
        }
        if (!string.IsNullOrWhiteSpace(f.Model))
        {
            sqlBase += " AND md.name LIKE @Model ";
            p.Add("@Model", $"%{f.Model}%");
        }
        if (!string.IsNullOrWhiteSpace(f.FuelType))
        {
            sqlBase += " AND ft.name LIKE @FuelType ";
            p.Add("@FuelType", $"%{f.FuelType}%");
        }
        if (f.Year.HasValue) { sqlBase += " AND l.year = @Year "; p.Add("@Year", f.Year.Value); }
        if (f.EngineSizeMin.HasValue) { sqlBase += " AND l.engine_size >= @EngineSizeMin "; p.Add("@EngineSizeMin", f.EngineSizeMin.Value); }
        if (f.EngineSizeMax.HasValue) { sqlBase += " AND l.engine_size <= @EngineSizeMax "; p.Add("@EngineSizeMax", f.EngineSizeMax.Value); }
        if (f.MileageMin.HasValue) { sqlBase += " AND l.mileage >= @MileageMin "; p.Add("@MileageMin", f.MileageMin.Value); }
        if (f.MileageMax.HasValue) { sqlBase += " AND l.mileage <= @MileageMax "; p.Add("@MileageMax", f.MileageMax.Value); }
        if (f.PriceMin.HasValue) { sqlBase += " AND l.price >= @PriceMin "; p.Add("@PriceMin", f.PriceMin.Value); }
        if (f.PriceMax.HasValue) { sqlBase += " AND l.price <= @PriceMax "; p.Add("@PriceMax", f.PriceMax.Value); }

        var countSql = "SELECT COUNT(*) " + sqlBase;
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, p, cancellationToken: ct));

        var sortByKey = string.IsNullOrWhiteSpace(f.SortBy) ? "id" : f.SortBy!;
        if (!OrderMap.TryGetValue(sortByKey, out var orderExpr)) orderExpr = OrderMap["id"];
        var sortDir = (f.SortDir?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true) ? "DESC" : "ASC";

        var page = Math.Max(1, f.Page);
        var pageSize = Math.Clamp(f.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;

        var dataSql = $@"
SELECT 
  CAST(l.listing_id AS SIGNED)          AS Id,
  mfr.name                               AS Manufacturer,
  md.name                                AS Model,
  CAST(l.engine_size AS DECIMAL(10,2))   AS EngineSize,
  ft.name                                AS FuelType,
  CAST(l.year AS SIGNED)                 AS Year,
  CAST(l.mileage AS SIGNED)              AS Mileage,
  CAST(l.price AS DECIMAL(10,2))         AS Price
{sqlBase}
ORDER BY {orderExpr} {sortDir}
LIMIT @PageSize OFFSET @Offset;
";
        p.Add("@PageSize", pageSize);
        p.Add("@Offset", offset);

        var items = (await conn.QueryAsync<SaleDto>(new CommandDefinition(dataSql, p, cancellationToken: ct))).ToList();

        return new PagedResult<SaleDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<SaleDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        using var conn = Open();

        var sql = @"
SELECT 
  CAST(l.listing_id AS SIGNED)          AS Id,
  mfr.name                               AS Manufacturer,
  md.name                                AS Model,
  CAST(l.engine_size AS DECIMAL(10,2))   AS EngineSize,
  ft.name                                AS FuelType,
  CAST(l.year AS SIGNED)                 AS Year,
  CAST(l.mileage AS SIGNED)              AS Mileage,
  CAST(l.price AS DECIMAL(10,2))         AS Price
FROM listings l
LEFT JOIN fuel_types ft ON ft.fuel_type_id = l.fuel_type_id
LEFT JOIN models md ON md.model_id = l.model_id
LEFT JOIN manufacturers mfr ON mfr.manufacturer_id = md.manufacturer_id
WHERE l.listing_id = @Id;
";
        return await conn.QuerySingleOrDefaultAsync<SaleDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    // -----------------------
    //      WRITE (CRUD)
    // -----------------------
    public async Task<int> CreateAsync(CreateSaleRequest req, CancellationToken ct)
    {
        using var conn = Open();
        // Ensure the MySqlConnection is open before beginning a transaction
        await ((MySqlConnection)conn).OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        try
        {
            var manufacturerId = await GetOrCreateManufacturerId(conn, tx, req.Manufacturer, ct);
            var modelId = await GetOrCreateModelId(conn, tx, manufacturerId, req.Model, ct);
            var fuelTypeId = await GetOrCreateFuelTypeId(conn, tx, req.FuelType, ct);

            var sql = @"
INSERT INTO listings (model_id, fuel_type_id, engine_size, year, mileage, price)
VALUES (@ModelId, @FuelTypeId, @EngineSize, @Year, @Mileage, @Price);
SELECT LAST_INSERT_ID();
";
            var newId = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new
                {
                    ModelId = modelId,
                    FuelTypeId = fuelTypeId,
                    EngineSize = req.EngineSize,
                    Year = req.Year,
                    Mileage = req.Mileage,
                    Price = req.Price
                }, tx, cancellationToken: ct));

            tx.Commit();
            return newId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateAsync(int id, UpdateSaleRequest req, CancellationToken ct)
    {
        using var conn = Open();
        // Ensure the MySqlConnection is open before beginning a transaction
        await ((MySqlConnection)conn).OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        try
        {
            // Verificar que exista el listing
            var exists = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition("SELECT COUNT(*) FROM listings WHERE listing_id = @Id;", new { Id = id }, tx, cancellationToken: ct));
            if (exists == 0) { tx.Rollback(); return false; }

            var manufacturerId = await GetOrCreateManufacturerId(conn, tx, req.Manufacturer, ct);
            var modelId = await GetOrCreateModelId(conn, tx, manufacturerId, req.Model, ct);
            var fuelTypeId = await GetOrCreateFuelTypeId(conn, tx, req.FuelType, ct);

            var sql = @"
UPDATE listings
SET model_id = @ModelId,
    fuel_type_id = @FuelTypeId,
    engine_size = @EngineSize,
    year = @Year,
    mileage = @Mileage,
    price = @Price
WHERE listing_id = @Id;
";
            var rows = await conn.ExecuteAsync(
                new CommandDefinition(sql, new
                {
                    Id = id,
                    ModelId = modelId,
                    FuelTypeId = fuelTypeId,
                    EngineSize = req.EngineSize,
                    Year = req.Year,
                    Mileage = req.Mileage,
                    Price = req.Price
                }, tx, cancellationToken: ct));

            tx.Commit();
            return rows > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        using var conn = Open();

        var rows = await conn.ExecuteAsync(
            new CommandDefinition("DELETE FROM listings WHERE listing_id = @Id;", new { Id = id }, cancellationToken: ct));

        return rows > 0;
    }

    // -----------------------
    //   Helpers (FK ensure)
    // -----------------------
    private static async Task<int> GetOrCreateManufacturerId(IDbConnection conn, IDbTransaction tx, string name, CancellationToken ct)
    {
        var id = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition("SELECT manufacturer_id FROM manufacturers WHERE name = @Name LIMIT 1;", new { Name = name }, tx, cancellationToken: ct));
        if (id.HasValue) return id.Value;

        var newId = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("INSERT INTO manufacturers(name) VALUES(@Name); SELECT LAST_INSERT_ID();", new { Name = name }, tx, cancellationToken: ct));
        return newId;
    }

    private static async Task<int> GetOrCreateModelId(IDbConnection conn, IDbTransaction tx, int manufacturerId, string modelName, CancellationToken ct)
    {
        var id = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition(@"SELECT model_id FROM models WHERE manufacturer_id = @Man AND name = @Name LIMIT 1;",
                                   new { Man = manufacturerId, Name = modelName }, tx, cancellationToken: ct));
        if (id.HasValue) return id.Value;

        var newId = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(@"INSERT INTO models(manufacturer_id, name) VALUES(@Man, @Name); SELECT LAST_INSERT_ID();",
                                   new { Man = manufacturerId, Name = modelName }, tx, cancellationToken: ct));
        return newId;
    }

    private static async Task<int> GetOrCreateFuelTypeId(IDbConnection conn, IDbTransaction tx, string fuelTypeName, CancellationToken ct)
    {
        var id = await conn.ExecuteScalarAsync<int?>(
            new CommandDefinition("SELECT fuel_type_id FROM fuel_types WHERE name = @Name LIMIT 1;", new { Name = fuelTypeName }, tx, cancellationToken: ct));
        if (id.HasValue) return id.Value;

        var newId = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("INSERT INTO fuel_types(name) VALUES(@Name); SELECT LAST_INSERT_ID();", new { Name = fuelTypeName }, tx, cancellationToken: ct));
        return newId;
    }
}
