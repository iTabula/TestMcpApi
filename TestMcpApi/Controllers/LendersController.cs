using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Models;
using TestMcpApi.Services;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController] // Use ApiController attributes if integrating into an existing Web API
public class LendersController : ControllerBase
{
    private readonly ILenderService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;

    public LendersController(ILenderService lenderService, IConfiguration configuration)
    {
        svc = lenderService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
    }






    //HELPERS
    private static IEnumerable<Lender> Filter(
        ILenderService svc,
        string? lender = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = svc.GetLenders().Result.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(lender))
        {
            string normAgent = Normalize(lender);

            data = data.Where(t =>
                t.LenderContact != null &&
                Normalize(t.LenderContact).Contains(normAgent, StringComparison.OrdinalIgnoreCase));
        }

        if (year.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value.Year == year.Value);

        if (from.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value >= from.Value);

        if (to.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value <= to.Value);

        return data;
    }

    private static string Normalize(string value)
    {
        return string
            .Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries)) // remove duplicate spaces
            .Trim()
            .ToLowerInvariant();
    }


    private static IEnumerable<Lender> FilterByLenderAndYear(
    ILenderService svc,
    string? lender = null,
    int? year = null)
    {
        var data = svc.GetLenders().Result.AsEnumerable();
        if (!string.IsNullOrEmpty(lender))
            data = data.Where(t => t.LenderContact != null && t.LenderContact.Equals(lender, StringComparison.OrdinalIgnoreCase));

        if (year.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value.Year == year.Value);

        return data;
    }

    private static string GetMostPopularValueFiltered(
        ILenderService svc,
        Func<Lender, string?> selector,
        string? lender = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = Filter(svc, lender, year, from, to)
                   .Where(t => !string.IsNullOrEmpty(selector(t)))
                   .Where(t => selector(t) != "NULL");

        var key = data.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault()?.Key ?? "N/A";

        return key;
    }

}
