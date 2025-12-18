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
public class RealsController : ControllerBase
{
    private readonly IRealTransactionService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;

    public RealsController(IRealTransactionService realTransactionService, IConfiguration configuration)
    {
        svc = realTransactionService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
    }





    //HELPERS
    private static IEnumerable<RealTransaction> FilterRealTransactions(
            RealTransactionService svc,
            IEnumerable<string>? agents = null,
            IEnumerable<int>? years = null,
            DateTime? from = null,
            DateTime? to = null)
    {
        var data = svc.GetRealTransactions().Result.AsEnumerable();

        if (agents != null && agents.Any())
            data = data.Where(t => t.AgentName != null && agents.Any(a => string.Equals(a, t.AgentName, StringComparison.OrdinalIgnoreCase)));

        if (years != null && years.Any())
            data = data.Where(t => t.ActualClosedDate.HasValue && years.Contains(t.ActualClosedDate.Value.Year));

        if (from.HasValue)
            data = data.Where(t => t.ActualClosedDate.HasValue && t.ActualClosedDate.Value >= from.Value);

        if (to.HasValue)
            data = data.Where(t => t.ActualClosedDate.HasValue && t.ActualClosedDate.Value <= to.Value);

        return data;
    }

    private static string GetMostPopularValueFilteredReal(
            RealTransactionService svc,
            Func<RealTransaction, string?> selector,
            IEnumerable<string>? agents = null,
            IEnumerable<int>? years = null,
            DateTime? from = null,
            DateTime? to = null)
    {
        var data = FilterRealTransactions(svc, agents, years, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(selector(t)))
                   .Where(t => selector(t) != "NULL");

        var key = data.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault()?.Key ?? "N/A";

        return key;
    }
}
