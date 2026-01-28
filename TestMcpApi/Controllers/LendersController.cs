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
    [Description("Retrieves contact information (phone number, email, and representative details) for a specific lender by company name. " +
        "Allows finding how to reach or communicate with a lending institution. " +
        "Supports fuzzy name matching and phonetic search for company identification. " +
        "Returns representative title, name, phone number, and email address. " +
        "Use this when the user asks for lender contact details, phone number, email, or how to reach a lender. " +
        "Relevant for questions like: what's the phone number for this lender, how do I contact this lender, what's the email address for this lending company, or who is the lender representative.")]
    [HttpGet("/lenders/details/{company}")]
    public string GetLenderContactInfo(
        [Description("Name of the lender company whose contact information to retrieve (supports fuzzy and phonetic matching)")] string company_name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves a ranked list of top-performing lenders based on transaction count. " +
        "Allows identifying the most active or frequently used lending institutions. " +
        "Supports fuzzy name matching and phonetic search for lender identification when lender filter is used. " +
        "Supports optional filtering by lender name, year (specific year), and date range (from/to dates). " +
        "When lender filter is applied, only data for that lender is analyzed. " +
        "When year filter is applied, only transactions from that specific year are counted. " +
        "When date range filters are applied, only transactions within the from/to date range are included. " +
        "Use this when the user asks about top lenders, most used lenders, or lender rankings. " +
        "Relevant for questions like: who are the top lenders, which lenders have the most deals, show me the most active lenders, or rank lenders by volume.")]
    [HttpGet("/lenders/top")]
    public string GetTopLenders(
        [Description("Optional filter: Maximum number of top lenders to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the lender to filter by (supports fuzzy and phonetic matching)")] string? lender = null,
        [Description("Optional filter: Year to filter transactions by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves a list of lenders operating in a specific state. " +
        "Allows identifying lending institutions available in a particular geographic location. " +
        "Supports fuzzy name matching and phonetic search for lender identification when lender filter is used. " +
        "Supports optional filtering by lender name, year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When lender filter is applied, only that specific lender is included if they operate in the state. " +
        "When year filter is applied, only lenders active in that specific year are returned. " +
        "When date range filters are applied, only lenders active within the from/to date range are included. " +
        "Use this when the user asks about lenders in a state, available lenders by location, or state-specific lending options. " +
        "Relevant for questions like: which lenders operate in this state, show me lenders in California, what lending companies are available in Texas, or find lenders by state.")]
    [HttpGet("/lenders/by-state/{state}")]
    public string GetLendersByState(
        [Description("State abbreviation or name to filter lenders by (e.g., CA, California, TX, Texas)")] string state,
        [Description("Optional filter: Maximum number of lenders to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the lender to filter by (supports fuzzy and phonetic matching)")] string? lender = null,
        [Description("Optional filter: Year to filter lenders by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter lenders from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter lenders to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves a list of VA (Veterans Affairs) approved lenders. " +
        "Allows identifying lending institutions approved to process VA home loans for veterans and service members. " +
        "Supports fuzzy name matching and phonetic search for lender identification when lender filter is used. " +
        "Supports optional filtering by lender name, year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When lender filter is applied, only that specific lender is included if they are VA approved. " +
        "When year filter is applied, only VA approved lenders active in that specific year are returned. " +
        "When date range filters are applied, only VA approved lenders active within the from/to date range are included. " +
        "Use this when the user asks about VA lenders, veteran loan options, or VA approved lending institutions. " +
        "Relevant for questions like: which lenders are VA approved, show me VA lenders, what companies offer VA loans, or find VA approved lending institutions.")]
    [HttpGet("/lenders/va-approved")]
    public string GetVAApprovedLenders(
        [Description("Optional filter: Maximum number of VA approved lenders to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the lender to filter by (supports fuzzy and phonetic matching)")] string? lender = null,
        [Description("Optional filter: Year to filter lenders by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter lenders from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter lenders to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves comprehensive statistics for lenders including total count, average compensation, and VA approval ratio. " +
        "Allows analyzing overall lender portfolio metrics and characteristics. " +
        "Supports fuzzy name matching and phonetic search for lender identification when lender filter is used. " +
        "Supports optional filtering by lender name, year (specific year), and date range (from/to dates). " +
        "When lender filter is applied, only statistics for that specific lender are calculated. " +
        "When year filter is applied, only lenders active in that specific year are included in statistics. " +
        "When date range filters are applied, only lenders active within the from/to date range are included. " +
        "Returns total lender count, average maximum compensation, and percentage of VA approved lenders. " +
        "Use this when the user asks about lender statistics, lender analytics, or lender portfolio metrics. " +
        "Relevant for questions like: what are the lender statistics, show me lender analytics, what's the average compensation, or what percentage are VA approved.")]
    [HttpGet("/lenders/stats")]
    public string GetLenderStats(
        [Description("Optional filter: Name of the lender to calculate statistics for (supports fuzzy and phonetic matching)")] string? lender = null,
        [Description("Optional filter: Year to filter statistics by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves a ranked list of cities with the highest number of lenders. " +
        "Allows identifying geographic areas with the most lending institution presence. " +
        "Supports fuzzy name matching and phonetic search for lender identification when lender filter is used. " +
        "Supports optional filtering by lender name, year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When lender filter is applied, only cities where that specific lender operates are included. " +
        "When year filter is applied, only lenders active in that specific year are counted per city. " +
        "When date range filters are applied, only lenders active within the from/to date range are included. " +
        "Use this when the user asks about top cities for lenders, lender concentration by location, or cities with most lending options. " +
        "Relevant for questions like: which cities have the most lenders, what are the top cities for lending institutions, show me lender concentration by city, or where are most lenders located.")]
    [HttpGet("/lenders/top-cities")]
    public string GetTopLenderCities(
        [Description("Optional filter: Maximum number of top cities to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the lender to filter by (supports fuzzy and phonetic matching)")] string? lender = null,
        [Description("Optional filter: Year to filter lenders by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter lenders from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter lenders to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves the most recently added lenders to the system. " +
        "Allows viewing new lending institutions that have been added to the database. " +
        "Supports fuzzy name matching and phonetic search for lender identification when lender filter is used. " +
        "Supports optional filtering by lender name, date range (from/to dates by date added), and top count (maximum number of results). " +
        "When lender filter is applied, only that specific lender is included if recently added. " +
        "When date range filters are applied, only lenders added within the from/to date range are included. " +
        "Results are ordered by date added in descending order (most recent first). " +
        "Use this when the user asks about new lenders, recently added lenders, or latest lending institutions. " +
        "Relevant for questions like: who are the newest lenders, show me recently added lenders, what lenders were added recently, or list new lending companies.")]
    [HttpGet("/lenders/recent")]
    public string GetRecentlyAddedLenders(
        [Description("Optional filter: Maximum number of recently added lenders to return (default is 10)")]
        int top = 10,
        [Description("Optional filter: Name of the lender to filter by (supports fuzzy and phonetic matching)")] string? lender = null,
        [Description("Optional filter: Start date to filter lenders from by date added (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter lenders to by date added (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves notes and processor notes for a specific lender company. " +
        "Allows viewing internal documentation, comments, and processor-specific information about a lending institution. " +
        "Supports fuzzy name matching and phonetic search for company identification. " +
        "Returns both general notes and processor notes fields. " +
        "Use this when the user asks about lender notes, lender documentation, or internal lender information. " +
        "Relevant for questions like: show me notes for this lender, what are the lender notes, get internal documentation for this lending company, or show me processor notes.")]
    [HttpGet("/lenders/notes/{companyName}")]
    public string GetLenderNotes(
        [Description("Name of the lender company whose notes to retrieve (supports fuzzy and phonetic matching)")] string company_name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves the website URL for a specific lender company. " +
        "Allows accessing the official website of a lending institution. " +
        "Supports fuzzy name matching and phonetic search for company identification. " +
        "Returns the company's website URL. " +
        "Use this when the user asks about lender websites, company URLs, or online presence. " +
        "Relevant for questions like: what's the website for this lender, show me the lender's URL, where can I find this lending company online, or give me the lender's website.")]
    [HttpGet("/lenders/website/{companyName}")]
    public string GetLenderWebsite(
        [Description("Name of the lender company whose website to retrieve (supports fuzzy and phonetic matching)")] string company_name,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
