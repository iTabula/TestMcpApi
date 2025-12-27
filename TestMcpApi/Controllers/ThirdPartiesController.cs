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


    [McpServerTool]
    [Description("Get the most popular third-party purpose")]
    [HttpGet("/thirdparties/most-popular-purpose")]
    public string GetMostPopularPurpose(
        [Description("Which is the most common purpose of third parties?")] string? name = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Third-party data is not available right now.";

        string popular = GetMostPopularValueFiltered(svc, t => t.Purpose, name, null, year, from, to);
        return $"The most common purpose of third parties {(name != null ? $"matching '{name}'" : "")} is: {popular}";
    }

    [McpServerTool]
    [Description("Get top third-party names ranked by number of occurrences")]
    [HttpGet("/thirdparties/top-names")]
    public string GetTopThirdPartyNames(
        [Description("Which third parties are the most common?")] int top = 10,
        string? name = null,
        string? purpose = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Third-party data is not available right now.";

        var data = Filter(svc, name, purpose, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.Name));

        var result = data.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(g => g.Count())
                         .Take(top)
                         .Select(g => new TopThirdPartyResult { Name = g.Key!, Count = g.Count() })
                         .ToList();

        if (!result.Any())
            return "No third-party records found with the specified filters.";

        string names = string.Join(", ", result.Select(r => $"{r.Name} ({r.Count})"));
        return $"The top {top} third-party names are: {names}";
    }

    [McpServerTool]
    [Description("Get contact info for third parties")]
    [HttpGet("/thirdparties/contacts")]
    public string GetThirdPartyContacts(
        [Description("Which third parties' contact info do you want?")] string? name = null,
        string? purpose = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Third-party data is not available right now.";

        var data = Filter(svc, name, purpose, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.Name));

        if (!data.Any())
            return "No third-party contacts found with the specified filters.";

        var result = data.Select(t => new ThirdPartyContactResult
        {
            Name = t.Name!,
            Username = t.Username,
            Website = t.Website
        }).ToList();

        string response = string.Join(", ", result.Select(r => $"{r.Name} (Username: {r.Username ?? "N/A"}, Website: {r.Website ?? "N/A"})"));
        return $"The requested third-party contacts are: {response}";
    }

    [McpServerTool]
    [Description("Get all third parties with notes")]
    [HttpGet("/thirdparties/with-notes")]
    public string GetThirdPartiesWithNotes(
        [Description("Do you want to see all third parties that have notes?")] int top = 10,
        string? name = null,
        string? purpose = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Third-party data is not available right now.";

        var data = Filter(svc, name, purpose, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.Notes));

        if (!data.Any())
            return "No third-party notes found with the specified filters.";

        var result = data.Take(top).Select(t => t.Name!).ToList();
        string response = string.Join(", ", result);
        return $"The top {top} third parties with notes are: {response}";
    }

    [McpServerTool]
    [Description("Get all third parties marked as admin view only")]
    [HttpGet("/thirdparties/admin-only")]
    public string GetAdminOnlyThirdParties(
        [Description("Do you want to see third parties that are admin view only?")] int top = 10,
        string? name = null,
        string? purpose = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Third-party data is not available right now.";

        var data = Filter(svc, name, purpose, year, from, to)
           .Where(t => !string.IsNullOrEmpty(t.AdminViewOnly) && t.AdminViewOnly.Equals("Yes", StringComparison.OrdinalIgnoreCase));

        if (!data.Any())
            return "No admin-only third-party records found with the specified filters.";

        var result = data.Take(top).Select(t => t.Name!).ToList();
        string response = string.Join(", ", result);
        return $"The top {top} third parties marked as admin view only are: {response}";
    }

    [McpServerTool]
    [Description("Get third party info by username")]
    [HttpGet("/thirdparties/by-username")]
    public string GetThirdPartyByUsername(
        [Description("What is the username of the third party you are looking for?")] string username)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Third-party data is not available right now.";

        if (string.IsNullOrWhiteSpace(username))
            return "Please provide a valid username.";

        var tp = svc.GetThirdParties().Result
                    .FirstOrDefault(t => !string.IsNullOrEmpty(t.Username) &&
                                         t.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (tp == null)
            return $"No third party found with username '{username}'.";

        return $"Third party '{tp.Name}' has username '{tp.Username}', purpose '{tp.Purpose}', website '{tp.Website ?? "N/A"}', and notes: '{tp.Notes ?? "N/A"}'.";
    }

    [McpServerTool]
    [Description("Get statistics for third parties")]
    [HttpGet("/thirdparties/stats")]
    public string GetThirdPartyStats(
        [Description("What are the third-party stats?")] string? name = null,
        string? purpose = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Third-party data is not available right now.";

        var data = Filter(svc, name, purpose, year, from, to);

        if (!data.Any())
            return "No third-party records found with the specified filters.";

        int total = data.Count();
        DateTime? lastUpdated = data.Max(t => t.LastUpdatedOn);

        var purposes = data.Where(t => !string.IsNullOrEmpty(t.Purpose))
                           .GroupBy(t => t.Purpose, StringComparer.OrdinalIgnoreCase)
                           .OrderByDescending(g => g.Count())
                           .FirstOrDefault()?.Key ?? "N/A";

        return $"Filtered third-party stats: Total = {total}, Most recent update = {(lastUpdated.HasValue ? lastUpdated.Value.ToString("yyyy-MM-dd") : "N/A")}, Most common purpose = {purposes}.";
    }

    // HELPERS
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
