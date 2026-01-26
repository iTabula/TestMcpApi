using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Helpers;
using TestMcpApi.Models;
using TestMcpApi.Services;
using static Azure.Core.HttpHeader;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController] // Use ApiController attributes if integrating into an existing Web API
public class RealsController : ControllerBase
{
    private readonly IRealTransactionService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RealsController(IRealTransactionService realTransactionService, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        svc = realTransactionService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        _httpContextAccessor = httpContextAccessor;
    }

    [McpServerTool]
    [Description("List real estate transactions by agent name")]
    [HttpGet("/reals/agent/{agent}")]
    public string GetRealTransactionsByAgent(
         [Description("List the transactions made by the agent, during the selected year or date range")]
         string agent,
         [Description("Maximum number of transactions to return")] int top = 10,
         [Description("Filter by specific year")] int? year = null,
         [Description("Filter transactions from this date")] DateTime? from = null,
         [Description("Filter transactions to this date")] DateTime? to = null,
         [Description("user_id")] int user_id = 0,
         [Description("user_role")] string user_role = "unknown",
         [Description("token")] string token = "unknown",
         [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        var allAgents = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.AgentName))
            .Select(t => new { AgentName = t.AgentName })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for agent
        var matchedAgent = TestMcpApi.Helpers.Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

        // Step 3: Get user related to phonetic results
        if (matchedAgent != null)
        {
            agent = matchedAgent.AgentName ?? agent;
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "not available right now";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                    .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID))
                    .Take(top)
                    .Select(t => new
                    {
                        RealTransID = t.RealTransID,
                        ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                        AgentName = t.AgentName,
                        TransactionType = t.TransactionType ?? t.TransType,
                        RealAmount = t.RealAmount ?? t.PurchasePrice,
                        SubjectAddress = t.SubjectAddress,
                        ActualClosedDate = t.ActualClosedDate,
                        LenderName = t.LenderName,
                        TitleCompanyName = t.TitleCompany,
                        RealTerm = t.RealTerm,
                        AppraisedValue = t.AppraisedValue,
                        PropertyAddress = t.SubjectAddress,
                        City = t.SubjectCity,
                        State = t.SubjectState
                    });

        if (!data.Any())
            return $"No real estate transactions found for agent {agent} using the selected filters.";

        List<RealTransactionDto> results = JsonSerializer.Deserialize<List<RealTransactionDto>>(JsonSerializer.Serialize(data))!;

        // Step 6: Present data
        string transactions = results
            .Select(r =>
                "Transaction #" + r.RealTransID +
                ", Amount: " + (r.RealAmount?.ToString("C") ?? "N/A") +
                ", Type: " + r.TransactionType +
                ", Address: " + r.SubjectAddress +
                ", City: " + (r.City ?? "N/A") +
                ", State: " + (r.State ?? "N/A") +
                ", Lender: " + (r.LenderName ?? "N/A") +
                ", Title Company: " + (r.TitleCompanyName ?? "N/A") +
                ", Term: " + (r.RealTerm?.ToString() ?? "N/A") +
                ", Appraised Value: " + (r.AppraisedValue?.ToString("C") ?? "N/A"))
            .Aggregate((a, b) => a + "; " + b);

        return $"The transactions made by {agent}, during the year {year} are: {transactions}";
    }

    [McpServerTool]
    [Description("List real estate transactions by title company")]
    [HttpGet("/reals/title-company/{titleCompany}")]
    public string GetRealTransactionsByTitleCompany(
        [Description("List the real estate transactions managed by the title company")] string titleCompany,
        [Description("Maximum number of transactions to return")] int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on titleCompany parameter
        var allTitleCompanies = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.TitleCompany))
            .Select(t => new { TitleCompany = t.TitleCompany })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for title company
        var matchedTitleCompany = TestMcpApi.Helpers.Common.MatchPhonetic(allTitleCompanies, titleCompany, tc => tc.TitleCompany ?? string.Empty);

        // Step 3: Get title company related to phonetic results
        if (matchedTitleCompany != null)
        {
            titleCompany = matchedTitleCompany.TitleCompany ?? titleCompany;
        }

        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetRealTransactions().Result
                .Where(t => !string.IsNullOrWhiteSpace(t.AgentName))
                .Select(t => new { AgentName = t.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = TestMcpApi.Helpers.Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

            // Step 3: Get user related to phonetic results
            if (matchedAgent != null)
            {
                agent = matchedAgent.AgentName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                    .Where(t => !string.IsNullOrWhiteSpace(t.TitleCompany))
                    .Where(t => string.Equals(t.TitleCompany, titleCompany, StringComparison.OrdinalIgnoreCase))
                    .Take(top)
                    .Select(t => new
                    {
                        RealTransID = t.RealTransID,
                        ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                        AgentName = t.AgentName,
                        SubjectAddress = t.SubjectAddress,
                        TransactionType = t.TransactionType ?? t.TransType,
                        RealAmount = t.RealAmount ?? t.PurchasePrice,
                        ActualClosedDate = t.ActualClosedDate,
                        LenderName = t.LenderName,
                        TitleCompanyName = t.TitleCompany,
                        RealTerm = t.RealTerm,
                        AppraisedValue = t.AppraisedValue,
                        PropertyAddress = t.SubjectAddress,
                        City = t.SubjectCity,
                        State = t.SubjectState
                    });

        if (!data.Any())
            return $"No real estate transactions were found for the title company {titleCompany} using the selected filters.";

        List<RealTransactionDto> results =
            JsonSerializer.Deserialize<List<RealTransactionDto>>(JsonSerializer.Serialize(data))!;

        // Step 6: Present data
        string transactions = results
            .Select(r =>
                "Transaction #" + r.RealTransID +
                ", Amount: " + (r.RealAmount?.ToString("C") ?? "N/A") +
                ", Type: " + r.TransactionType +
                ", Address: " + r.SubjectAddress +
                ", City: " + (r.City ?? "N/A") +
                ", State: " + (r.State ?? "N/A") +
                ", Lender: " + (r.LenderName ?? "N/A") +
                ", Term: " + (r.RealTerm?.ToString() ?? "N/A") +
                ", Appraised Value: " + (r.AppraisedValue?.ToString("C") ?? "N/A"))
            .Aggregate((a, b) => a + "; " + b);

        return $"The real estate transactions managed by the title company {titleCompany} are: {transactions}";
    }

    // property info - LTV, lender, 
    [McpServerTool]
    [Description("Get a real estate transaction by property address")]
    [HttpGet("/reals/property/{subjectAddress}")]
    public string GetRealTransactionByPropertyAddress(
        [Description("Get the real estate transaction information for the property at this address")] string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetRealTransactions().Result
                            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.SubjectAddress) &&
                                                 string.Equals(t.SubjectAddress, subjectAddress, StringComparison.OrdinalIgnoreCase));

        if (transaction == null)
            return $"No real estate transaction was found for the property address {subjectAddress}.";

        var result = new RealTransactionDto
        {
            RealTransID = transaction.RealTransID,
            ClientFullName = $"{transaction.ClientFirstName} {transaction.ClientLastName}".Trim(),
            AgentName = transaction.AgentName,
            SubjectAddress = transaction.SubjectAddress,
            TransactionType = transaction.TransactionType ?? transaction.TransType,
            RealAmount = transaction.RealAmount ?? transaction.PurchasePrice,
            ActualClosedDate = transaction.ActualClosedDate,
            LenderName = transaction.LenderName,
            TitleCompanyName = transaction.TitleCompany,
            RealTerm = transaction.RealTerm.ToString(),
            AppraisedValue = transaction.AppraisedValue,
            PropertyAddress = transaction.SubjectAddress,
            City = transaction.SubjectCity,
            State = transaction.SubjectState
        };

        return $"Transaction #{result.RealTransID} for property {result.SubjectAddress}, " +
               $"City: {result.City ?? "N/A"}, State: {result.State ?? "N/A"}, " +
               $"handled by agent {result.AgentName}, client {result.ClientFullName}, " +
               $"type: {result.TransactionType}, amount: {(result.RealAmount?.ToString("C") ?? "N/A")}, " +
               $"closed on: {(result.ActualClosedDate?.ToShortDateString() ?? "N/A")}, " +
               $"lender: {result.LenderName ?? "N/A"}, title company: {result.TitleCompanyName ?? "N/A"}, " +
               $"term: {(result.RealTerm?.ToString() ?? "N/A")}, " +
               $"appraised value: {(result.AppraisedValue?.ToString("C") ?? "N/A")}";
    }

    [McpServerTool]
    [Description("List real estate transactions by escrow company")]
    [HttpGet("/reals/escrow/{escrowCompany}")]
    public string GetTransactionsByEscrowCompany(
        [Description("List the real estate transactions handled by this escrow company")] string escrowCompany,
        [Description("Maximum number of transactions to return")] int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetRealTransactions().Result
                .Where(t => !string.IsNullOrWhiteSpace(t.AgentName))
                .Select(t => new { AgentName = t.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = TestMcpApi.Helpers.Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

            // Step 3: Get user related to phonetic results
            if (matchedAgent != null)
            {
                agent = matchedAgent.AgentName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.EscrowCompany) &&
                               string.Equals(t.EscrowCompany, escrowCompany, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType ?? t.TransType,
                       RealAmount = t.RealAmount ?? t.PurchasePrice,
                       ActualClosedDate = t.ActualClosedDate,
                       LenderName = t.LenderName,
                       TitleCompanyName = t.TitleCompany,
                       RealTerm = t.RealTerm.ToString(),
                       AppraisedValue = t.AppraisedValue,
                       PropertyAddress = t.SubjectAddress,
                       City = t.SubjectCity,
                       State = t.SubjectState
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for escrow company {escrowCompany} using the selected filters.";

        // Step 6: Present data
        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID} for property {r.SubjectAddress}, " +
            $"City: {r.City ?? "N/A"}, State: {r.State ?? "N/A"}, " +
            $"handled by agent {r.AgentName}, client {r.ClientFullName}, " +
            $"type: {r.TransactionType}, amount: {(r.RealAmount?.ToString("C") ?? "N/A")}, " +
            $"closed on: {(r.ActualClosedDate?.ToShortDateString() ?? "N/A")}, " +
            $"lender: {r.LenderName ?? "N/A"}, title company: {r.TitleCompanyName ?? "N/A"}, " +
            $"term: {(r.RealTerm?.ToString() ?? "N/A")}, " +
            $"appraised value: {(r.AppraisedValue?.ToString("C") ?? "N/A")}"
        ).Aggregate((a, b) => a + "\n" + b);

        return $"The top {data.Count} transactions for escrow company {escrowCompany} are:\n{transactions}";
    }


    [McpServerTool]
    [Description("Get the total number of transactions made by a specific agent")]
    [HttpGet("/reals/total-transactions/agent/{agent}")]
    public string GetTotalTransactionsByAgent(
        [Description("How many transactions has the agent completed?")] string agent,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(agent))
        {
            return "No agent name was provided.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
            .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID));

        int total = data.Count();

        if (total == 0)
        {
            return $"No transactions found for agent {agent} using the selected filters.";
        }

        return $"The total number of transactions completed by {agent} {(year.HasValue ? "in " + year.Value : "")} is {total}.";
    }


    [McpServerTool]
    [Description("Get the most popular ZIP code among real transactions")]
    [HttpGet("/reals/most-popular-zip")]
    public string GetMostPopularZip(
        [Description("Which ZIP code appears most frequently among the real transactions?")]
        string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var popularZip = GetMostPopularValueFilteredReal(svc, t => t.SubjectPostalCode, agent, year, from, to);

        if (string.IsNullOrWhiteSpace(popularZip) || popularZip == "N/A")
        {
            return "No ZIP codes found for the selected filters.";
        }

        return $"The most popular ZIP code among the real transactions is {popularZip}.";
    }

    [McpServerTool]
    [Description("Get the top cities by number of real transactions")]
    [HttpGet("/reals/top-cities")]
    public string GetTopCities(
        [Description("What are the top cities with the most real transactions?")]
        int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                    .Where(t => !string.IsNullOrWhiteSpace(t.SubjectCity));

        var grouped = data.GroupBy(t => t.SubjectCity, StringComparer.OrdinalIgnoreCase)
                          .OrderByDescending(g => g.Count())
                          .Take(top)
                          .Select(g => new { City = g.Key, Transactions = g.Count() });

        if (!grouped.Any())
            return "No city data found for the selected filters.";

        List<TopCityResult> results = JsonSerializer.Deserialize<List<TopCityResult>>(JsonSerializer.Serialize(grouped))!;

        var formatted = results.Select(r => $"{r.City} with {r.Transactions} transactions")
                               .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} cities with the most real transactions are: {formatted}.";
    }

    [McpServerTool]
    [Description("Get the top agents ranked by number of real transactions")]
    [HttpGet("/reals/top-agents")]
    public string GetTopAgents(
        [Description("Who are the top agents for real estate transactions?")]
        int top = 10,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, null, year, from, to)
                    .Where(t => !string.IsNullOrWhiteSpace(t.AgentName));

        var grouped = data.GroupBy(t => t.AgentName, StringComparer.OrdinalIgnoreCase)
                          .OrderByDescending(g => g.Count())
                          .Take(top)
                          .Select(g => new { Agent = g.Key, Transactions = g.Count() });

        if (!grouped.Any())
            return "No agent data found for the selected filters.";

        List<TopAgentResult> results = JsonSerializer.Deserialize<List<TopAgentResult>>(JsonSerializer.Serialize(grouped))!;

        var formatted = results.Select(r => $"{r.Agent} with {r.Transactions} transactions")
                               .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} agents for real estate transactions are: {formatted}.";
    }


    [McpServerTool]
    [Description("Get the total 1099 income for a specific agent during a year")]
    [HttpGet("/reals/agent-1099")]
    public string GetAgent1099(
        [Description("What is the agent's name you want to get the 1099 for?")]
        string agent,
        [Description("For which year do you want to get the 1099?")]
        int year,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(agent))
        {
            return "Agent name must be provided.";
        }

        decimal total1099 = svc.GetAgent1099(agent, year);

        return $"The total 1099 income for agent {agent} in {year} is: {total1099:C}";
    }

    [McpServerTool]
    [Description("Get total transactions and summary statistics for a lender")]
    [HttpGet("/reals/lender-stats")]
    public string GetLenderStats(
        [Description("Which lender do you want to get stats for?")]
        string lender,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(lender))
        {
            return "Lender name must be provided.";
        }

        var stats = svc.GetLenderStats(lender);

        if (stats.totalTransactions == 0)
        {
            return $"No transactions found for lender {lender}.";
        }

        LenderStatsResult result = new LenderStatsResult
        {
            TotalTransactions = stats.totalTransactions,
            AvgAmount = stats.avgAmount,
            MaxAmount = stats.maxAmount,
            MinAmount = stats.minAmount
        };

        return $"The lender {lender} has {result.TotalTransactions} transactions. " +
               $"Average transaction amount: {result.AvgAmount:C}, " +
               $"Maximum amount: {result.MaxAmount:C}, " +
               $"Minimum amount: {result.MinAmount:C}.";
    }

    [McpServerTool]
    [Description("Get the most popular transaction type")]
    [HttpGet("/reals/most-popular-transaction-type")]
    public string GetMostPopularTransactionType(
        [Description("Which transaction type is most common for the selected filters?")]
        string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        string mostPopularType = GetMostPopularValueFilteredReal(svc, t => t.TransactionType, agent, year, from, to);

        if (mostPopularType == "N/A")
            return "No transaction types found for the selected filters.";

        return $"The most popular transaction type{(agent != null ? $" for {agent}" : "")} is: {mostPopularType}.";
    }

    [McpServerTool]
    [Description("Get the most popular client type")]
    [HttpGet("/reals/most-popular-client-type")]
    public string GetMostPopularClientType(
        [Description("Which client type is most common for the selected filters?")]
        string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        string mostPopularClientType = GetMostPopularValueFilteredReal(svc, t => t.ClientType, agent, year, from, to);

        if (mostPopularClientType == "N/A")
            return "No client types found for the selected filters.";

        return $"The most popular client type{(agent != null ? $" for {agent}" : "")} is: {mostPopularClientType}.";
    }

    [McpServerTool]
    [Description("Get the most popular real type")]
    [HttpGet("/reals/most-popular-real-type")]
    public string GetMostPopularRealType(
        [Description("Which real type is most common for the selected filters?")]
        string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        string mostPopularRealType = GetMostPopularValueFilteredReal(svc, t => t.RealType, agent, year, from, to);

        if (mostPopularRealType == "N/A")
            return "No real types found for the selected filters.";

        return $"The most popular real type{(agent != null ? $" for {agent}" : "")} is: {mostPopularRealType}.";
    }

    [McpServerTool]
    [Description("Get the most popular real sub-type")]
    [HttpGet("/reals/most-popular-real-subtype")]
    public string GetMostPopularRealSubType(
        [Description("Which real sub-type is most common for the selected filters?")]
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        string mostPopularRealSubType = GetMostPopularValueFilteredReal(svc, t => t.RealSubType, agent, year, from, to);

        if (mostPopularRealSubType == "N/A")
            return "No real sub-types found for the selected filters.";

        return $"The most popular real sub-type{(agent != null ? $" for {agent}" : "")} is: {mostPopularRealSubType}.";
    }

    //one method get by type

    [McpServerTool]
    [Description("List transactions by client type")]
    [HttpGet("/reals/by-client-type/{clientType}")]
    public string GetByClientType(
        [Description("List the transactions for the specified client type")]
        string clientType,
        [Description("Maximum number of transactions to return")] int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetRealTransactions().Result
                .Where(t => !string.IsNullOrWhiteSpace(t.AgentName))
                .Select(t => new { AgentName = t.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = TestMcpApi.Helpers.Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

            // Step 3: Get user related to phonetic results
            if (matchedAgent != null)
            {
                agent = matchedAgent.AgentName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.ClientType))
                   .Where(t => string.Equals(t.ClientType, clientType, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate,
                       LenderName = t.LenderName,
                       TitleCompanyName = t.TitleCompany,
                       RealTerm = t.RealTerm.ToString(),
                       AppraisedValue = t.AppraisedValue,
                       PropertyAddress = t.SubjectAddress,
                       City = t.SubjectCity,
                       State = t.SubjectState
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for client type '{clientType}' using the selected filters.";

        // Step 6: Present data
        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for client type '{clientType}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by property type")]
    [HttpGet("/reals/by-prop-type/{propType}")]
    public string GetByPropType(
        [Description("List the transactions for the specified property type")]
        string propType,
        [Description("Maximum number of transactions to return")] int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetRealTransactions().Result
                .Where(t => !string.IsNullOrWhiteSpace(t.AgentName))
                .Select(t => new { AgentName = t.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = TestMcpApi.Helpers.Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

            // Step 3: Get user related to phonetic results
            if (matchedAgent != null)
            {
                agent = matchedAgent.AgentName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.PropType))
                   .Where(t => string.Equals(t.PropType, propType, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate,
                       LenderName = t.LenderName,
                       TitleCompanyName = t.TitleCompany,
                       RealTerm = t.RealTerm.ToString(),
                       AppraisedValue = t.AppraisedValue,
                       PropertyAddress = t.SubjectAddress,
                       City = t.SubjectCity,
                       State = t.SubjectState
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for property type '{propType}' using the selected filters.";

        // Step 6: Present data
        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for property type '{propType}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by transaction type")]
    [HttpGet("/reals/by-trans-type/{transType}")]
    public string GetByTransactionType(
        [Description("List the transactions for the specified transaction type")]
        string transType,
        [Description("Maximum number of transactions to return")] int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetRealTransactions().Result
                .Where(t => !string.IsNullOrWhiteSpace(t.AgentName))
                .Select(t => new { AgentName = t.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = TestMcpApi.Helpers.Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

            // Step 3: Get user related to phonetic results
            if (matchedAgent != null)
            {
                agent = matchedAgent.AgentName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.TransType))
                   .Where(t => string.Equals(t.TransType, transType, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate,
                       LenderName = t.LenderName,
                       TitleCompanyName = t.TitleCompany,
                       RealTerm = t.RealTerm.ToString(),
                       AppraisedValue = t.AppraisedValue,
                       PropertyAddress = t.SubjectAddress,
                       City = t.SubjectCity,
                       State = t.SubjectState
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for transaction type '{transType}' using the selected filters.";

        // Step 6: Present data
        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for transaction type '{transType}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }


    [McpServerTool]
    [Description("List transactions by NMLS number")]
    [HttpGet("/reals/by-nmls-number/{nmlsNumber}")]
    public string GetByNMLSNumber(
        [Description("List the transactions associated with the specified NMLS number")]
        string nmlsNumber,
        [Description("Maximum number of transactions to return")] int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetRealTransactions().Result
                .Where(t => !string.IsNullOrWhiteSpace(t.AgentName))
                .Select(t => new { AgentName = t.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = TestMcpApi.Helpers.Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

            // Step 3: Get user related to phonetic results
            if (matchedAgent != null)
            {
                agent = matchedAgent.AgentName;
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.NMLSNumber) && t.NMLSNumber.Equals(nmlsNumber, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType ?? t.TransType,
                       RealAmount = t.RealAmount ?? t.PurchasePrice,
                       ActualClosedDate = t.ActualClosedDate,
                       LenderName = t.LenderName,
                       TitleCompanyName = t.TitleCompany,
                       RealTerm = t.RealTerm.ToString(),
                       AppraisedValue = t.AppraisedValue,
                       PropertyAddress = t.SubjectAddress,
                       City = t.SubjectCity,
                       State = t.SubjectState
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for NMLS number {nmlsNumber} using the selected filters.";

        // Step 6: Present data
        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for NMLS number {nmlsNumber}{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("Get statistics for transaction prices")]
    [HttpGet("/reals/price-stats")]
    public string GetPriceStats(
        [Description("What are the total number of transactions, average price, maximum price, and minimum price for the selected filters?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => t.Price.HasValue);

        if (!data.Any())
            return "No transactions with prices found for the selected filters.";

        var totalTransactions = data.Count();
        var avgPrice = data.Average(t => t.Price!.Value);
        var maxPrice = data.Max(t => t.Price!.Value);
        var minPrice = data.Min(t => t.Price!.Value);

        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average price is {avgPrice:C}, the maximum price is {maxPrice:C}, and the minimum price is {minPrice:C}.";
    }

    [McpServerTool]
    [Description("Get statistics for real terms of transactions")]
    [HttpGet("/reals/realterm-stats")]
    public string GetRealTermStats(
        [Description("What are the total number of transactions, average real term, maximum real term, and minimum real term for the selected filters?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => t.RealTerm.HasValue);

        if (!data.Any())
            return "No transactions with real term found for the selected filters.";

        var totalTransactions = data.Count();
        var avgTerm = data.Average(t => t.RealTerm!.Value);
        var maxTerm = data.Max(t => t.RealTerm!.Value);
        var minTerm = data.Min(t => t.RealTerm!.Value);

        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average real term is {avgTerm}, the maximum real term is {maxTerm}, and the minimum real term is {minTerm}.";
    }

    [McpServerTool]
    [Description("Get statistics for real amounts of transactions")]
    [HttpGet("/reals/realamount-stats")]
    public string GetRealAmountStats(
        [Description("What are the total number of transactions, average real amount, maximum real amount, and minimum real amount for the selected filters?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => t.RealAmount.HasValue);

        if (!data.Any())
            return "No transactions with real amount found for the selected filters.";

        var totalTransactions = data.Count();
        var avgAmount = data.Average(t => t.RealAmount!.Value);
        var maxAmount = data.Max(t => t.RealAmount!.Value);
        var minAmount = data.Min(t => t.RealAmount!.Value);

        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average real amount is {avgAmount}, the maximum real amount is {maxAmount}, and the minimum real amount is {minAmount}.";
    }

    [McpServerTool]
    [Description("Get statistics for appraised values of transactions")]
    [HttpGet("/reals/appraisedvalue-stats")]
    public string GetAppraisedValueStats(
        [Description("What are the total number of transactions, average appraised value, maximum appraised value, and minimum appraised value for the selected filters?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => t.AppraisedValue.HasValue);

        if (!data.Any())
            return "No transactions with appraised value found for the selected filters.";

        var totalTransactions = data.Count();
        var avgValue = data.Average(t => t.AppraisedValue!.Value);
        var maxValue = data.Max(t => t.AppraisedValue!.Value);
        var minValue = data.Min(t => t.AppraisedValue!.Value);

        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average appraised value is {avgValue}, the maximum appraised value is {maxValue}, and the minimum appraised value is {minValue}.";
    }

    [McpServerTool]
    [Description("Get statistics for LTV (Loan-to-Value) of real estate transactions")]
    [HttpGet("/reals/ltv-stats")]
    public string GetLTVStats(
        [Description("What are the total number of transactions, average LTV, maximum LTV, and minimum LTV for the selected filters?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => t.LTV.HasValue);

        if (!data.Any())
            return "No transactions with LTV values found for the selected filters.";

        var totalTransactions = data.Count();
        var avgLTV = data.Average(t => t.LTV!.Value);
        var maxLTV = data.Max(t => t.LTV!.Value);
        var minLTV = data.Min(t => t.LTV!.Value);

        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average LTV is {avgLTV}, the maximum LTV is {maxLTV}, and the minimum LTV is {minLTV}.";
    }

    [McpServerTool]
    [Description("Get statistics for Interest Rate of real estate transactions")]
    [HttpGet("/reals/interest-rate-stats")]
    public string GetInterestRateStats(
        [Description("What are the total number of transactions, average interest rate, maximum interest rate, and minimum interest rate for the selected filters?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => t.InterestRate.HasValue);

        if (!data.Any())
            return "No transactions with interest rate values found for the selected filters.";

        var totalTransactions = data.Count();
        var avgRate = data.Average(t => t.InterestRate!.Value);
        var maxRate = data.Max(t => t.InterestRate!.Value);
        var minRate = data.Min(t => t.InterestRate!.Value);

        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average interest rate is {avgRate}, the maximum interest rate is {maxRate}, and the minimum interest rate is {minRate}.";
    }

    [McpServerTool]
    [Description("Get statistics for TC Fees of real estate transactions")]
    [HttpGet("/reals/tcfees-stats")]
    public string GetTCFeesStats(
        [Description("What are the total number of transactions, average TC Fees, maximum TC Fees, and minimum TC Fees for the selected filters?")]
        string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => t.TCFees.HasValue);

        if (!data.Any())
            return "No transactions with TC Fees values found for the selected filters.";

        var totalTransactions = data.Count();
        var avgFees = data.Average(t => t.TCFees!.Value);
        var maxFees = data.Max(t => t.TCFees!.Value);
        var minFees = data.Min(t => t.TCFees!.Value);

        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average TC Fees is {avgFees}, the maximum TC Fees is {maxFees}, and the minimum TC Fees is {minFees}.";
    }

    [McpServerTool]
    [Description("Get home inspection information for a property by address")]
    [HttpGet("/reals/home-inspection-info/{subjectAddress}")]
    public string GetHomeInspectionInfo(
        [Description("What is the home inspection information for the property located at this address?")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new HomeInspectionInfo
        {
            Name = transaction.HomeInspectionName ?? "N/A",
            Done = transaction.HomeInspectionDone ?? "N/A",
            Phone = transaction.HomeInspectionPhone ?? "N/A",
            Email = transaction.HomeInspectionEmail ?? "N/A",
            Notes = transaction.HomeInspectionNotes ?? "N/A"
        };

        return $"Home Inspection Info for {subjectAddress}: Name: {info.Name}, Done: {info.Done}, " +
               $"Phone: {info.Phone}, Email: {info.Email}, Notes: {info.Notes}";
    }

    [McpServerTool]
    [Description("Get pest inspection information for a property by address")]
    [HttpGet("/reals/pest-inspection-info/{subjectAddress}")]
    public string GetPestInspectionInfo(
        [Description("What is the pest inspection information for the property located at this address?")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new PestInspectionInfo
        {
            Name = transaction.PestInspectionName ?? "N/A",
            Done = transaction.PestInspectionDone ?? "N/A",
            Phone = transaction.PestInspectionPhone ?? "N/A",
            Email = transaction.PestInspectionEmail ?? "N/A",
            Notes = transaction.PestInspectionNotes ?? "N/A"
        };

        return $"Pest Inspection Info for {subjectAddress}: Name: {info.Name}, Done: {info.Done}, " +
               $"Phone: {info.Phone}, Email: {info.Email}, Notes: {info.Notes}";
    }

    [McpServerTool]
    [Description("Get escrow information for a property by address")]
    [HttpGet("/reals/escrow-info/{subjectAddress}")]
    public string GetEscrowInfo(
        [Description("What is the escrow information for the property located at this address?")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new EscrowInfo
        {
            Company = transaction.EscrowCompany ?? "N/A",
            Phone = transaction.EscrowCoPhone ?? "N/A",
            Officer = transaction.EscrowOfficer ?? "N/A",
            OfficerEmail = transaction.EscrowOfficerEmail ?? "N/A",
            OfficerPhone = transaction.EscrowOfficerPhone ?? "N/A",
            EscrowNumber = transaction.EscrowNumber ?? "N/A",
            MethodSendType = transaction.EscrowMethodSendType ?? "N/A"
        };

        return $"Escrow Info for {subjectAddress}: Company: {info.Company}, Phone: {info.Phone}, " +
               $"Officer: {info.Officer}, Officer Email: {info.OfficerEmail}, Officer Phone: {info.OfficerPhone}, " +
               $"Escrow Number: {info.EscrowNumber}, Method Send Type: {info.MethodSendType}";
    }

    [McpServerTool]
    [Description("Get title company information for a property by address")]
    [HttpGet("/reals/title-company-info/{subjectAddress}")]
    public string GetTitleCompanyInfo(
        [Description("What is the title company information for the property located at this address?")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new TitleCompanyInfo
        {
            Company = transaction.TitleCompany ?? "N/A",
            Phone = transaction.TitleCoPhone ?? "N/A"
        };

        return $"Title Company Info for {subjectAddress}: Company: {info.Company}, Phone: {info.Phone}";
    }

    [McpServerTool]
    [Description("Get appraisal company information for a property by address")]
    [HttpGet("/reals/appraisal-company-info/{subjectAddress}")]
    public string GetAppraisalCompanyInfo(
        [Description("What is the appraisal company information for the property located at this address?")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new AppraisalCompanyInfo
        {
            Company = transaction.AppraisalCompany ?? "N/A",
            Phone = transaction.AppraisalCoPhone ?? "N/A"
        };

        return $"Appraisal Company Info for {subjectAddress}: Company: {info.Company}, Phone: {info.Phone}";
    }

    [McpServerTool]
    [Description("Get transaction coordinator (TC) information for a property by address")]
    [HttpGet("/reals/tc-info/{subjectAddress}")]
    public string GetTCInfo(
        [Description("What is the transaction coordinator (TC) information for the property located at this address?")]
        string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new TCInfo
        {
            Flag = transaction.TCFlag ?? "N/A",
            TC = transaction.TC,
            Fees = transaction.TCFees
        };

        return $"Transaction Coordinator Info for {subjectAddress}: Flag: {info.Flag}, TC: {info.TC}, Fees: {info.Fees}";
    }

    [McpServerTool]
    [Description("Get payment information for a property by address")]
    [HttpGet("/reals/payment-info/{subjectAddress}")]
    public string GetPaymentInfo(
        [Description("What is the payment information for the property located at this address?")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new PaymentInfo
        {
            ExpectedDate = transaction.ExpectedDate,
            PayableTo = transaction.PayableTo,
            AgentAddress = transaction.AgentAddress,
            ProcessorAmount = transaction.ProcessorAmount,
            CheckAmount = transaction.CheckAmount,
            MailingFee = transaction.MailingFee,
            Notes = transaction.Notes,
            RoutingNumber = transaction.RoutingNumber,
            ClearDate = transaction.ClearDate
        };

        return $"Payment Info for {subjectAddress}: Expected Date: {info.ExpectedDate}, Payable To: {info.PayableTo}, Agent Address: {info.AgentAddress}, Processor Amount: {info.ProcessorAmount}, Check Amount: {info.CheckAmount}, Routing Number: {info.RoutingNumber}, Mailing Fee: {info.MailingFee}, Notes: {info.Notes}, Clear Date: {info.ClearDate}";
    }

    [McpServerTool]
    [Description("Get bank information for a property by address")]
    [HttpGet("/reals/bank-info/{subjectAddress}")]
    public string GetBankInfo(
        [Description("What is the banking information for the property located at this address?")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown")
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null)
            return $"No real estate transaction found for the property address: {subjectAddress}.";

        var info = new BankInfo
        {
            IncomingBank = transaction.IncomingBank,
            OutgoingBank = transaction.OutgoingBank,
            BankName = transaction.BankName,
            AccountName = transaction.AccountName,
            RoutingNumber = transaction.RoutingNumber,
            AccountNumber = transaction.AccountNumber,
            AmountRetainedByKam = transaction.AmountRetainedByKam,
            AmountPaidToKamAgent = transaction.AmountPaidToKamAgent
        };

        return $"Bank Info for {subjectAddress}: Incoming Bank: {info.IncomingBank}, Outgoing Bank: {info.OutgoingBank}, Bank Name: {info.BankName}, Account Name: {info.AccountName}, Routing Number: {info.RoutingNumber}, Account Number: {info.AccountNumber}, Amount Retained By KAM: {info.AmountRetainedByKam}, Amount Paid To KAM Agent: {info.AmountPaidToKamAgent}";
    }



    //HELPERS
    private static IEnumerable<RealTransaction> FilterRealTransactions(
            IRealTransactionService svc,
            string? agent = null,
            int? year = null,
            DateTime? from = null,
            DateTime? to = null)
    {
        var data = svc.GetRealTransactions().Result.OrderByDescending(t => t.DateAdded).AsEnumerable();

        // Filter by agent (case-insensitive)
        if (!string.IsNullOrWhiteSpace(agent))
        {
            string normAgent = TestMcpApi.Helpers.Common.Normalize(agent);

            data = data.Where(t =>
                t.AgentName != null &&
                TestMcpApi.Helpers.Common.Normalize(t.AgentName).Contains(normAgent, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by year
        if (year.HasValue)
            data = data.Where(t => t.ActualClosedDate.HasValue && t.ActualClosedDate.Value.Year == year.Value);

        // Filter by date range
        if (from.HasValue)
            data = data.Where(t => t.ActualClosedDate.HasValue && t.ActualClosedDate.Value >= from.Value);

        if (to.HasValue)
            data = data.Where(t => t.ActualClosedDate.HasValue && t.ActualClosedDate.Value <= to.Value);

        return data;
    }

    private static string GetMostPopularValueFilteredReal(
            IRealTransactionService svc,
            Func<RealTransaction, string?> selector,
           string? agent = null,
            int? year = null,
            DateTime? from = null,
            DateTime? to = null)
    {
        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrEmpty(selector(t)))
                   .Where(t => selector(t) != "NULL");

        var key = data.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault()?.Key ?? "N/A";

        return key;
    }
}
