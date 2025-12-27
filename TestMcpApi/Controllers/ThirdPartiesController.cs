using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Models;
using TestMcpApi.Services;

[McpServerToolType]
[ApiController]
public class ThirdPartiesController : ControllerBase
{
    private readonly IThirdPartyService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;

    public ThirdPartiesController(IThirdPartyService thirdPartyService, IConfiguration configuration)
    {
        svc = thirdPartyService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
    }

    // =========================
    // HELPERS
    // =========================
    private static IEnumerable<ThirdParty> Filter(
        IThirdPartyService svc,
        string? name = null,
        string? purpose = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = svc.GetThirdParties().Result.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            string normName = Normalize(name);
            data = data.Where(t => t.Name != null &&
                                   Normalize(t.Name).Contains(normName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            string normPurpose = Normalize(purpose);
            data = data.Where(t => t.Purpose != null &&
                                   Normalize(t.Purpose).Contains(normPurpose, StringComparison.OrdinalIgnoreCase));
        }

        if (year.HasValue)
        {
            data = data.Where(t => t.LastUpdatedOn.HasValue && t.LastUpdatedOn.Value.Year == year.Value);
        }

        if (from.HasValue)
            data = data.Where(t => t.LastUpdatedOn.HasValue && t.LastUpdatedOn.Value >= from.Value);

        if (to.HasValue)
            data = data.Where(t => t.LastUpdatedOn.HasValue && t.LastUpdatedOn.Value <= to.Value);

        return data;
    }

    private static string Normalize(string value)
    {
        return string.Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                     .Trim()
                     .ToLowerInvariant();
    }

    private static IEnumerable<ThirdParty> FilterByNameAndYear(
        IThirdPartyService svc,
        string? name = null,
        int? year = null)
    {
        var data = svc.GetThirdParties().Result.AsEnumerable();

        if (!string.IsNullOrEmpty(name))
        {
            data = data.Where(t => t.Name != null &&
                                   t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        if (year.HasValue)
        {
            data = data.Where(t => t.LastUpdatedOn.HasValue && t.LastUpdatedOn.Value.Year == year.Value);
        }

        return data;
    }

    private static string GetMostPopularValueFiltered(
        IThirdPartyService svc,
        Func<ThirdParty, string?> selector,
        string? name = null,
        string? purpose = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = Filter(svc, name, purpose, year, from, to)
                   .Where(t => !string.IsNullOrEmpty(selector(t)) && selector(t) != "NULL");

        var key = data.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault()?.Key ?? "N/A";

        return key;
    }
}
