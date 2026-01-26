using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Phonix;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Helpers;
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
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LendersController(ILenderService lenderService, IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        svc = lenderService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        _httpContextAccessor = httpContextAccessor;
    }

    [McpServerTool]
    [Description("What's the phone number or details of lender with company name?")]
    [HttpGet("/lenders/details/{company}")]
    public string GetLendersByCompany(
        [Description("the lender or company name")] string company_name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        if (string.IsNullOrWhiteSpace(company_name))
            return "Company name must be provided.";

        // Step 1: Get data for phonetic matching on company_name parameter
        var allLenders = svc.GetLenders().Result.AsEnumerable();

        if (!allLenders.Any())
            return "I could not find any lenders data";

        // Step 2: Match phonetics for company
        var matchedLender = Common.MatchPhonetic(allLenders, company_name, l => l.CompanyName ?? string.Empty);

        // Step 3: Get lender related to phonetic results
        if (matchedLender != null)
        {
            company_name = matchedLender.CompanyName ?? company_name;
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
        var result = allLenders
            .FirstOrDefault(x => x.CompanyName != null && 
                               x.CompanyName.Equals(company_name, StringComparison.OrdinalIgnoreCase));

        if (result == null)
            return "I could not find a lender with this name.";

        // Step 6: Present data
        string phone = string.IsNullOrEmpty(result.Cell) ? 
                      (string.IsNullOrEmpty(result.WorkPhone1) ? result.WorkPhone2 : result.WorkPhone1) : 
                      result.Cell;
        phone = phone.Replace(",", "").Replace("-", "").Replace(" ", "");
        phone = Common.FormatPhoneNumber(phone);
        string title = result.Title.Replace("--Select a Title--", "Account Executive");
        var summary = $" {title} {result.FirstName} {result.LastName} at {phone}. Email address is {result.Email}";
        return $"Lender {company_name} is found. Contact {summary}";
    }

    [McpServerTool]
    [Description("Get top lenders ranked by number of transactions")]
    [HttpGet("/lenders/top")]
    public string GetTopLenders(
        [Description("who are the top lenders for KAM")] int top = 10,
        [Description("Filter by lender name")] string? lender = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on lender parameter
        if (!string.IsNullOrEmpty(lender))
        {
            var allLenders = svc.GetLenders().Result
                .Where(l => !string.IsNullOrWhiteSpace(l.CompanyName))
                .Select(l => new { CompanyName = l.CompanyName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for lender
            var matchedLender = Common.MatchPhonetic(allLenders, lender, l => l.CompanyName ?? string.Empty);

            // Step 3: Get lender related to phonetic results
            if (matchedLender != null)
            {
                lender = matchedLender.CompanyName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, lender, name, user_id, user_role, token, out string effectiveLender);
        if (authError != null)
            return authError;

        lender = effectiveLender;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, lender, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.CompanyName));

        var grouped = data
            .GroupBy(l => l.CompanyName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(top)
            .Select(g => new { 
                Lender = g.Key, 
                Transactions = g.Count(),
                City = g.FirstOrDefault()?.City,
                State = g.FirstOrDefault()?.State
            });

        if (!grouped.Any())
            return "There are no lender transactions available for the selected filters.";

        // Step 6: Present data
        var results = JsonSerializer.Deserialize<List<TopLenderResult>>(
            JsonSerializer.Serialize(grouped))!;

        var summary = results
            .Select(r => {
                var location = !string.IsNullOrWhiteSpace(r.City) && !string.IsNullOrWhiteSpace(r.State)
                    ? $" ({r.City}, {r.State})"
                    : !string.IsNullOrWhiteSpace(r.City)
                        ? $" ({r.City})"
                        : "";
                return $"{r.Lender}{location} with {r.Transactions} transactions";
            })
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} lenders for KAM are: {summary}";
    }

    [McpServerTool]
    [Description("List lenders operating in a specific state")]
    [HttpGet("/lenders/by-state/{state}")]
    public string GetLendersByState(
        [Description("which lenders operate in this state")] string state,
        [Description("Maximum number of lenders to return")] int top = 10,
        [Description("Filter by lender name")] string? lender = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        if (string.IsNullOrWhiteSpace(state))
            return "State must be provided.";

        // Step 1: Get data for phonetic matching on lender parameter
        if (!string.IsNullOrEmpty(lender))
        {
            var allLenders = svc.GetLenders().Result
                .Where(l => !string.IsNullOrWhiteSpace(l.CompanyName))
                .Select(l => new { CompanyName = l.CompanyName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for lender
            var matchedLender = Common.MatchPhonetic(allLenders, lender, l => l.CompanyName ?? string.Empty);

            // Step 3: Get lender related to phonetic results
            if (matchedLender != null)
            {
                lender = matchedLender.CompanyName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, lender, name, user_id, user_role, token, out string effectiveLender);
        if (authError != null)
            return authError;

        lender = effectiveLender;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, lender, year, from, to)
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

        // Step 6: Present data
        var results = JsonSerializer.Deserialize<List<LenderStateResult>>(
            JsonSerializer.Serialize(data))!;

        var summary = results
            .Select(r => $"{r.CompanyName} ({r.LenderContact}) in {r.City}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The lenders operating in the state {state} are: {summary}";
    }

    [McpServerTool]
    [Description("List VA approved lenders")]
    [HttpGet("/lenders/va-approved")]
    public string GetVAApprovedLenders(
        [Description("which lenders are VA approved")] int top = 10,
        [Description("Filter by lender name")] string? lender = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on lender parameter
        if (!string.IsNullOrEmpty(lender))
        {
            var allLenders = svc.GetLenders().Result
                .Where(l => !string.IsNullOrWhiteSpace(l.CompanyName))
                .Select(l => new { CompanyName = l.CompanyName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for lender
            var matchedLender = Common.MatchPhonetic(allLenders, lender, l => l.CompanyName ?? string.Empty);

            // Step 3: Get lender related to phonetic results
            if (matchedLender != null)
            {
                lender = matchedLender.CompanyName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, lender, name, user_id, user_role, token, out string effectiveLender);
        if (authError != null)
            return authError;

        lender = effectiveLender;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, lender, year, from, to)
            .Where(l => l.VAApproved == "Yes")
            .Take(top)
            .Select(l => new
            {
                l.CompanyName,
                l.LenderContact
            });

        if (!data.Any())
            return "There are no VA approved lenders available for the selected filters.";

        // Step 6: Present data
        var results = JsonSerializer.Deserialize<List<VALenderResult>>(
            JsonSerializer.Serialize(data))!;

        var summary = results
            .Select(r => $"{r.CompanyName} ({r.LenderContact})")
            .Aggregate((a, b) => a + ", " + b);

        return $"The VA approved lenders are: {summary}";
    }

    [McpServerTool]
    [Description("Get statistics for lenders including total count, average compensation, and VA approval ratio")]
    [HttpGet("/lenders/stats")]
    public string GetLenderStats(
        [Description("what are the lender statistics for KAM")] string? lender = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on lender parameter
        if (!string.IsNullOrEmpty(lender))
        {
            var allLenders = svc.GetLenders().Result
                .Where(l => !string.IsNullOrWhiteSpace(l.CompanyName))
                .Select(l => new { CompanyName = l.CompanyName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for lender
            var matchedLender = Common.MatchPhonetic(allLenders, lender, l => l.CompanyName ?? string.Empty);

            // Step 3: Get lender related to phonetic results
            if (matchedLender != null)
            {
                lender = matchedLender.CompanyName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, lender, name, user_id, user_role, token, out string effectiveLender);
        if (authError != null)
            return authError;

        lender = effectiveLender;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, lender, year, from, to).ToList();

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

        // Step 6: Present data
        var result = new LenderStatsResult
        {
            TotalTransactions = total,
            AvgAmount = avgComp,
            VARatio = vaRatio
        };

        return $"The lender statistics are: total lenders {result.TotalTransactions}, average compensation {result.AvgAmount}, and VA approval ratio {result.VARatio} percent.";
    }

    [McpServerTool]
    [Description("Get the top cities with the most lenders")]
    [HttpGet("/lenders/top-cities")]
    public string GetTopLenderCities(
        [Description("what are the top cities with the most lenders")] int top = 10,
        [Description("Filter by lender name")] string? lender = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on lender parameter
        if (!string.IsNullOrEmpty(lender))
        {
            var allLenders = svc.GetLenders().Result
                .Where(l => !string.IsNullOrWhiteSpace(l.CompanyName))
                .Select(l => new { CompanyName = l.CompanyName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for lender
            var matchedLender = Common.MatchPhonetic(allLenders, lender, l => l.CompanyName ?? string.Empty);

            // Step 3: Get lender related to phonetic results
            if (matchedLender != null)
            {
                lender = matchedLender.CompanyName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, lender, name, user_id, user_role, token, out string effectiveLender);
        if (authError != null)
            return authError;

        lender = effectiveLender;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, lender, year, from, to)
            .Where(l => !string.IsNullOrWhiteSpace(l.City));

        var grouped = data
            .GroupBy(l => l.City, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(top)
            .Select(g => new TopLenderCityResult
            {
                City = g.Key,
                State = g.FirstOrDefault()?.State ?? "",
                Count = g.Count()
            })
            .ToList();

        if (!grouped.Any())
            return "There are no lender city records available for the selected filters.";

        // Step 6: Present data
        var cities = grouped
            .Select(c => string.IsNullOrWhiteSpace(c.State) 
                ? $"{c.City} with {c.Count} lenders"
                : $"{c.City}, {c.State} with {c.Count} lenders")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The top {top} cities with the most lenders are: {cities}.";
    }

    [McpServerTool]
    [Description("Get recently added lenders")]
    [HttpGet("/lenders/recent")]
    public string GetRecentlyAddedLenders(
        [Description("Who are the most recently added lenders??")]
        int top = 10,
        [Description("Filter by lender name")] string? lender = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on lender parameter
        if (!string.IsNullOrEmpty(lender))
        {
            var allLenders = svc.GetLenders().Result
                .Where(l => !string.IsNullOrWhiteSpace(l.CompanyName))
                .Select(l => new { CompanyName = l.CompanyName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for lender
            var matchedLender = Common.MatchPhonetic(allLenders, lender, l => l.CompanyName ?? string.Empty);

            // Step 3: Get lender related to phonetic results
            if (matchedLender != null)
            {
                lender = matchedLender.CompanyName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, lender, name, user_id, user_role, token, out string effectiveLender);
        if (authError != null)
            return authError;

        lender = effectiveLender;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Lender data is not available right now.";

        var data = Filter(svc, lender, null, from, to)
            .Where(l => l.DateAdded.HasValue)
            .OrderByDescending(l => l.DateAdded)
            .Take(top)
            .ToList();

        if (!data.Any())
            return "There are no recently added lenders.";

        // Step 6: Present data
        var lenders = data
            .Select(l => $"{l.CompanyName ?? l.LenderContact} ({l.DateAdded:yyyy-MM-dd})")
            .Aggregate((a, b) => $"{a}, {b}");

        return $"The most recently added lenders are: {lenders}.";
    }

    [McpServerTool]
    [Description("Get notes and processor notes for a specific lender company")]
    [HttpGet("/lenders/notes/{companyName}")]
    public string GetLenderNotes(
        [Description("The lender company name to get notes for")] string company_name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        if (string.IsNullOrWhiteSpace(company_name))
            return "Company name must be provided.";

        // Step 1: Get data for phonetic matching on company_name parameter
        var allLenders = svc.GetLenders().Result.AsEnumerable();

        if (!allLenders.Any())
            return "I could not find any lenders data";

        // Step 2: Match phonetics for company
        var matchedLender = Common.MatchPhonetic(allLenders, company_name, l => l.CompanyName ?? string.Empty);

        // Step 3: Get lender related to phonetic results
        if (matchedLender != null)
        {
            company_name = matchedLender.CompanyName ?? company_name;
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
        var result = allLenders
            .FirstOrDefault(x => x.CompanyName != null && 
                               x.CompanyName.Equals(company_name, StringComparison.OrdinalIgnoreCase));

        if (result == null)
            return $"I could not find a lender with the company name '{company_name}'.";

        // Step 6: Present data
        var notes = string.IsNullOrWhiteSpace(result.Notes) ? "No notes available" : result.Notes;
        var processorNotes = string.IsNullOrWhiteSpace(result.ProcessorNotes) ? "No processor notes available" : result.ProcessorNotes;

        return $"Notes for {company_name}:\n\nNotes: {notes}\n\nProcessor Notes: {processorNotes}";
    }

    [McpServerTool]
    [Description("Get the website URL for a specific lender company")]
    [HttpGet("/lenders/website/{companyName}")]
    public string GetLenderWebsite(
        [Description("The lender company name to get the website for")] string company_name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        if (string.IsNullOrWhiteSpace(company_name))
            return "Company name must be provided.";

        // Step 1: Get data for phonetic matching on company_name parameter
        var allLenders = svc.GetLenders().Result.AsEnumerable();

        if (!allLenders.Any())
            return "I could not find any lenders data";

        // Step 2: Match phonetics for company
        var matchedLender = Common.MatchPhonetic(allLenders, company_name, l => l.CompanyName ?? string.Empty);

        // Step 3: Get lender related to phonetic results
        if (matchedLender != null)
        {
            company_name = matchedLender.CompanyName ?? company_name;
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
        var result = allLenders
            .FirstOrDefault(x => x.CompanyName != null && 
                               x.CompanyName.Equals(company_name, StringComparison.OrdinalIgnoreCase));

        if (result == null)
            return $"I could not find a lender with the company name '{company_name}'.";

        // Step 6: Present data
        if (string.IsNullOrWhiteSpace(result.Website))
            return $"{company_name} does not have a website on record.";

        // Ensure the URL has a protocol
        string website = result.Website.Trim();
        if (!website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            website = "https://" + website;
        }

        return $"The website for {company_name} is: {website}";
    }

    //HELPERS
    private static IEnumerable<Lender> Filter(
        ILenderService svc,
        string? lender = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = svc.GetLenders().Result.AsEnumerable()
        .Where(l => l.Status != null && l.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(lender))
        {
            string normLender = TestMcpApi.Helpers.Common.Normalize(lender);

            data = data.Where(t =>
                t.CompanyName != null &&
                TestMcpApi.Helpers.Common.Normalize(t.CompanyName).Contains(normLender, StringComparison.OrdinalIgnoreCase));
        }

        if (year.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value.Year == year.Value);

        if (from.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value >= from.Value);

        if (to.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value <= to.Value);

        return data;
    }

    private static IEnumerable<Lender> FilterByLenderAndYear(
    ILenderService svc,
    string? lender = null,
    int? year = null)
    {
        var data = svc.GetLenders().Result.AsEnumerable();
        if (!string.IsNullOrEmpty(lender))
            data = data.Where(t => t.CompanyName != null && t.CompanyName.Equals(lender, StringComparison.OrdinalIgnoreCase));

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
