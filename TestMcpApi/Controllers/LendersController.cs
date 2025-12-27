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

    [McpServerTool]
    [Description("Get top lenders ranked by number of transactions")]
    [HttpGet("/lenders/top")]
    public string GetTopLenders(
    [Description("who are the top lenders for KAM")] int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.LenderContact));

        var grouped = data
            .GroupBy(l => l.LenderContact, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(top)
            .Select(g => new { Lender = g.Key, Transactions = g.Count() });

        if (!grouped.Any())
            return "There are no lender transactions available for the selected filters.";

        var results = JsonSerializer.Deserialize<List<TopLenderResult>>(
            JsonSerializer.Serialize(grouped))!;

        var summary = results
            .Select(r => $"{r.Lender} with {r.Transactions} transactions")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} lenders for KAM are: {summary}";
    }

    [McpServerTool]
    [Description("List lenders operating in a specific state")]
    [HttpGet("/lenders/by-state/{state}")]
    public string GetLendersByState(
    [Description("which lenders operate in this state")] string state,
    int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        if (string.IsNullOrWhiteSpace(state))
            return "State must be provided.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrEmpty(l.State) &&
                        l.State.Equals(state, StringComparison.OrdinalIgnoreCase))
            .Take(top)
            .Select(l => new
            {
                l.CompanyName,
                l.LenderContact,
                l.City
            });

        if (!data.Any())
            return $"No lenders were found in the state {state} using the selected filters.";

        var results = JsonSerializer.Deserialize<List<LenderStateResult>>(
            JsonSerializer.Serialize(data))!;

        var summary = results
            .Select(r => $"{r.CompanyName} ({r.LenderContact}) in {r.City}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The lenders operating in the state {state} are: {summary}";
    }

    [McpServerTool]
    [Description("List lenders by company name")]
    [HttpGet("/lenders/by-company/{company}")]
    public string GetLendersByCompany(
    [Description("which lenders work at this company")] string company,
    int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        if (string.IsNullOrWhiteSpace(company))
            return "Company name must be provided.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrEmpty(l.CompanyName) &&
                        l.CompanyName.Contains(company, StringComparison.OrdinalIgnoreCase))
            .Take(top)
            .Select(l => new
            {
                l.LenderContact,
                l.WorkPhone1,
                l.Email
            });

        if (!data.Any())
            return $"No lenders were found for the company {company} using the selected filters.";

        var results = JsonSerializer.Deserialize<List<LenderCompanyResult>>(
            JsonSerializer.Serialize(data))!;

        var summary = results
            .Select(r => $"{r.LenderContact} (Phone: {r.Phone}, Email: {r.Email})")
            .Aggregate((a, b) => a + ", " + b);

        return $"The lenders working at {company} are: {summary}";
    }

    [McpServerTool]
    [Description("List VA approved lenders")]
    [HttpGet("/lenders/va-approved")]
    public string GetVAApprovedLenders(
    [Description("which lenders are VA approved")] int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => l.VAApproved == "Yes")
            .Take(top)
            .Select(l => new
            {
                l.CompanyName,
                l.LenderContact
            });

        if (!data.Any())
            return "There are no VA approved lenders available for the selected filters.";

        var results = JsonSerializer.Deserialize<List<VALenderResult>>(
            JsonSerializer.Serialize(data))!;

        var summary = results
            .Select(r => $"{r.CompanyName} ({r.LenderContact})")
            .Aggregate((a, b) => a + ", " + b);

        return $"The VA approved lenders are: {summary}";
    }

    [McpServerTool]
    [Description("Get the most popular lender company based on number of transactions")]
    [HttpGet("/lenders/most-popular-company")]
    public string GetMostPopularLenderCompany(
    [Description("what is the most popular lender company")] int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var company = GetMostPopularValueFiltered(
            svc,
            l => l.CompanyName,
            null,
            year,
            from,
            to);

        if (company == "N/A")
            return "There is no lender company data available for the selected filters.";

        return $"The most popular lender company is {company}.";
    }

    [McpServerTool]
    [Description("Get statistics for lenders including total count, average compensation, and VA approval ratio")]
    [HttpGet("/lenders/stats")]
    public string GetLenderStats(
    [Description("what are the lender statistics for KAM")] int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to).ToList();

        if (!data.Any())
            return "There are no lenders available for the selected filters.";

        var total = data.Count;

        var avgComp = data
            .Select(l => decimal.TryParse(l.MaximumComp, out var v) ? v : (decimal?)null)
            .Where(v => v.HasValue)
            .DefaultIfEmpty()
            .Average();

        var vaApprovedCount = data.Count(l => l.VAApproved == "Yes");
        var vaRatio = total == 0 ? 0 : Math.Round((decimal)vaApprovedCount / total * 100, 2);

        var result = new LenderStatsResult
        {
            TotalTransactions = total,
            AvgAmount = avgComp,
            VARatio = vaRatio
        };

        return $"The lender statistics are: total lenders {result.TotalTransactions}, average compensation {result.AvgAmount}, and VA approval ratio {result.VARatio} percent.";
    }

    [McpServerTool]
    [Description("Get the primary lender contact for a specific company")]
    [HttpGet("/lenders/contact-by-company/{company}")]
    public string GetLenderContactByCompany(
    [Description("who is the lender contact for this company")] string company)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        if (string.IsNullOrWhiteSpace(company))
            return "Company name must be provided.";

        var lender = svc.GetByCompany(company)
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.LenderContact));

        if (lender == null)
            return $"No lender contact was found for the company {company}.";

        return $"The primary lender contact for {company} is {lender.LenderContact}.";
    }

    [McpServerTool]
    [Description("Get lender information by ID")]
    [HttpGet("/lenders/by-username/{username}")]
    public string GetLenderByID(
    [Description("which lender is associated with this ID")] string id)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        if (string.IsNullOrWhiteSpace(id))
            return "Username must be provided.";

        var lender = svc.GetLenders().Result
            .FirstOrDefault(l => l.LenderID.ToString() == id);

        if (lender == null)
            return $"No lender was found with the ID {id}.";

        var result = new LenderUsernameResult
        {
            CompanyName = lender.CompanyName,
            LenderContact = lender.LenderContact,
            Email = lender.Email,
            Phone = lender.WorkPhone1
        };

        return $"The lender associated with the ID {id} is {result.LenderContact} from {result.CompanyName}.";
    }

    [McpServerTool]
    [Description("Get the top cities with the most lenders")]
    [HttpGet("/lenders/top-cities")]
    public string GetTopLenderCities(
    [Description("what are the top cities with the most lenders")] int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.City));

        var grouped = data
            .GroupBy(l => l.City, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(top)
            .Select(g => new TopLenderCityResult
            {
                City = g.Key,
                Count = g.Count()
            })
            .ToList();

        if (!grouped.Any())
            return "There are no lender city records available for the selected filters.";

        var cities = grouped
            .Select(c => $"{c.City} with {c.Count} lenders")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The top {top} cities with the most lenders are: {cities}.";
    }

    [McpServerTool]
    [Description("List lenders that have notes or processor notes")]
    [HttpGet("/lenders/with-notes")]
    public string GetLendersWithNotes(
    [Description("which lenders have notes recorded")] int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.Notes) ||
                        !string.IsNullOrWhiteSpace(l.ProcessorNotes))
            .Take(top)
            .ToList();

        if (!data.Any())
            return "There are no lenders with notes available for the selected filters.";

        var names = data
            .Select(l => l.CompanyName ?? l.LenderContact ?? "Unknown lender")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The lenders with notes are: {names}.";
    }

    [McpServerTool]
    [Description("List inactive lenders")]
    [HttpGet("/lenders/inactive")]
    public string GetInactiveLenders(
    [Description("which lenders are inactive")] int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => l.Status != "Active")
            .Take(top)
            .ToList();

        if (!data.Any())
            return "There are no inactive lenders for the selected filters.";

        var names = data
            .Select(l => l.CompanyName ?? l.LenderContact ?? "Unknown lender")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The inactive lenders are: {names}.";
    }

    [McpServerTool]
    [Description("List lenders by website domain")]
    [HttpGet("/lenders/by-website/{website}")]
    public string GetLendersByWebsite(
    [Description("which lenders use this website")] string website,
    int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        if (string.IsNullOrWhiteSpace(website))
            return "Website value must be provided.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.Website) &&
                        l.Website.Contains(website, StringComparison.OrdinalIgnoreCase))
            .Take(top)
            .ToList();

        if (!data.Any())
            return $"There are no lenders associated with the website {website}.";

        var names = data
            .Select(l => l.CompanyName ?? l.LenderContact ?? "Unknown lender")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The lenders associated with the website {website} are: {names}.";
    }

    [McpServerTool]
    [Description("Get the top states with the most lenders")]
    [HttpGet("/lenders/top-states")]
    public string GetTopLendersByState(
    int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.State));

        var grouped = data
            .GroupBy(l => l.State, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(top)
            .Select(g => new TopLenderStateResult
            {
                State = g.Key,
                Count = g.Count()
            })
            .ToList();

        if (!grouped.Any())
            return "There are no lender state records available.";

        var states = grouped
            .Select(s => $"{s.State} ({s.Count})")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The top {top} states with the most lenders are: {states}.";
    }

    [McpServerTool]
    [Description("Get VA approved lender ratio by state")]
    [HttpGet("/lenders/va-ratio-by-state")]
    public string GetLendersVAApprovedRatioByState(
    int top = 10,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.State));

        var stats = data
            .GroupBy(l => l.State, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var total = g.Count();
                var va = g.Count(l => l.VAApproved == "Yes");
                return new LenderVAStateStats
                {
                    State = g.Key,
                    Total = total,
                    VAApproved = va,
                    Ratio = total == 0 ? 0 : Math.Round((double)va / total, 2)
                };
            })
            .OrderByDescending(s => s.Ratio)
            .Take(top)
            .ToList();

        if (!stats.Any())
            return "There are no VA approval statistics available.";

        var result = stats
            .Select(s => $"{s.State}: {s.Ratio:P0}")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The states with the highest VA-approved lender ratios are: {result}.";
    }

    [McpServerTool]
    [Description("Get the most common lender job titles")]
    [HttpGet("/lenders/common-titles")]
    public string GetMostCommonLenderTitle(
    int top = 5,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.Title));

        var titles = data
            .GroupBy(l => l.Title!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(top)
            .Select(g => $"{g.Key} ({g.Count()})")
            .ToList();

        if (!titles.Any())
            return "There are no lender titles available.";

        return $"The most common lender titles are: {string.Join(", ", titles)}.";
    }

    [McpServerTool]
    [Description("Get recently added lenders")]
    [HttpGet("/lenders/recent")]
    public string GetRecentlyAddedLenders(
    int top = 10,
    DateTime? from = null,
    DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, null, null, from, to)
            .Where(l => l.DateAdded.HasValue)
            .OrderByDescending(l => l.DateAdded)
            .Take(top)
            .ToList();

        if (!data.Any())
            return "There are no recently added lenders.";

        var lenders = data
            .Select(l => $"{l.CompanyName ?? l.LenderContact} ({l.DateAdded:yyyy-MM-dd})")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The most recently added lenders are: {lenders}.";
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
