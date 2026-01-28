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
    [Description("Retrieves a detailed list of real estate transactions for a specific agent by agent name. " +
        "Allows viewing complete transaction details including client, property, amounts, and parties involved. " +
        "Supports fuzzy name matching and phonetic search for agent identification. " +
        "Supports optional filtering by year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When year filter is applied, only transactions closed in that specific year are returned. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks for real estate transaction details, deal information, or property sales history for an agent. " +
        "Relevant for questions like: show me real estate transactions for, list deals for, what properties did an agent sell, or get transaction details for an agent.")]
    [HttpGet("/reals/agent/{agent}")]
    public string GetRealTransactionsByAgent(
         [Description("Name of the agent whose transactions to retrieve (supports fuzzy and phonetic matching)")]
         string agent,
         [Description("Optional filter: Maximum number of transactions to return (default is 10)")] int top = 10,
         [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
         [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
         [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
         [Description("user_id")] int user_id = 0,
         [Description("user_role")] string user_role = "unknown",
         [Description("token")] string token = "unknown",
         [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves a detailed list of real estate transactions managed by a specific title company. " +
        "Allows viewing all deals handled by a particular title company including transaction details. " +
        "Supports fuzzy name matching and phonetic search for title company identification. " +
        "Supports optional filtering by agent name, year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When agent filter is applied, only transactions for that agent are included. " +
        "When year filter is applied, only transactions closed in that specific year are returned. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about title company transactions, deals handled by title company, or title company activity. " +
        "Relevant for questions like: show me transactions for this title company, what deals did this title company handle, list properties for a title company, or how many transactions did this title company process.")]
    [HttpGet("/reals/title-company/{titleCompany}")]
    public string GetRealTransactionsByTitleCompany(
        [Description("Name of the title company whose transactions to retrieve (supports fuzzy and phonetic matching)")] string titleCompany,
        [Description("Optional filter: Maximum number of transactions to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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

    [McpServerTool]
    [Description("Retrieves all real estate transactions that have not been closed yet (no actual closed date). " +
        "Allows viewing pending or in-progress property deals that are still active. " +
        "Supports optional filtering by year (added year), date range (from/to dates by added date), and top count (maximum number of results). " +
        "When year filter is applied, only open transactions added in that specific year are returned. " +
        "When date range filters are applied, only open transactions added within the from/to date range are included. " +
        "Use this when the user asks about pending sales, active deals, or unclosed real estate transactions. " +
        "Relevant for questions like: which properties are still open, show me pending real estate transactions, what deals haven't closed yet, or list active property sales.")]
    [HttpGet("/reals/open")]
    public string GetOpenRealTrans(
        [Description("Optional filter: Maximum number of open transactions to return (default is 10)")] int top = 10,
        [Description("Optional filter: Year to filter open transactions by date added (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter open transactions from by date added (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter open transactions to by date added (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            // Step 1: Get data for phonetic matching
            var allUsers = new UserService().GetUsers().Result;

            // Step 2: Match phonetics
            var matchedUser = TestMcpApi.Helpers.Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            // Step 3: Get user related to phonetic results
            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = TestMcpApi.Helpers.Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var reals = FilterRealTransactions(svc, null, year, from, to)
                        .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID) && t.ActualClosedDate == null)
                        .Take(top)
                        .Select(t => new RealTransactionDto
                        {
                            RealTransID = t.RealTransID,
                            AgentName = t.AgentName,
                            RealAmount = t.RealAmount ?? t.PurchasePrice,
                            TransactionType = t.TransactionType ?? t.TransType,
                            RealTerm = t.RealTerm?.ToString(),
                            ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                            LenderName = t.LenderName,
                            TitleCompanyName = t.TitleCompany,
                            PropertyAddress = t.SubjectAddress,
                            City = t.SubjectCity,
                            State = t.SubjectState,
                            AppraisedValue = t.AppraisedValue,
                        })
                        .ToList();

            if (!reals.Any())
                result = "No open real estate transactions found";
            else
            {
                result = string.Join(", ", reals.Select(r =>
                    $"Transaction #{r.RealTransID}, Agent: {r.AgentName}, Client: {r.ClientFullName}, " +
                    $"Lender: {r.LenderName ?? "N/A"}, Title Company: {r.TitleCompanyName ?? "N/A"}, " +
                    $"Real Term: {r.RealTerm ?? "N/A"}, Real Amount: {r.RealAmount?.ToString("C") ?? "N/A"}, Transaction Type: {r.TransactionType ?? "N/A"}, " +
                    $"Address: {r.PropertyAddress}, City: {r.City ?? "N/A"}, State: {r.State ?? "N/A"}, " +
                    $"Appraised Value: {r.AppraisedValue?.ToString("C") ?? "N/A"}"));
            }
        }

        // Step 6: Present data
        return $"The open real estate transactions are: {result}";
    }

    [McpServerTool]
    [Description("Retrieves a detailed list of real estate transactions handled by a specific escrow company. " +
        "Allows viewing all deals processed by a particular escrow company including transaction details. " +
        "Supports optional filtering by agent name, year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When agent filter is applied, only transactions for that agent are included. " +
        "When year filter is applied, only transactions closed in that specific year are returned. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about escrow company transactions, deals handled by escrow, or escrow company activity. " +
        "Relevant for questions like: show me transactions for this escrow company, what deals did this escrow company handle, list properties for an escrow company, or how many transactions did this escrow company process.")]
    [HttpGet("/reals/escrow/{escrowCompany}")]
    public string GetTransactionsByEscrowCompany(
        [Description("Name of the escrow company whose transactions to retrieve")] string escrowCompany,
        [Description("Optional filter: Maximum number of transactions to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Get real estate transaction info for a specific property address")]
    [HttpGet("/reals/property/{subjectAddress}")]
    public string GetRealPropertyAddressInfo(
        [Description("Get the real estate transaction information for the property at this address")] string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
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
    [Description("Retrieves the total count of real estate transactions completed by a specific agent. " +
        "Allows finding how many property deals, sales, or listings an agent has handled. " +
        "Supports fuzzy name matching and phonetic search for agent identification. " +
        "Supports optional filtering by year (specific year) and date range (from/to dates by closed date). " +
        "When year filter is applied, only transactions closed in that specific year are counted. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks how many deals, transactions, properties, or closings an agent has. " +
        "Relevant for questions like: how many real estate deals, number of property transactions, total sales closed, how active an agent is, or how many closings an agent has.")]
    [HttpGet("/reals/total-transactions/agent/{agent}")]
    public string GetTotalTransactionsByAgent(
        [Description("Name of the agent whose transaction count to retrieve (supports fuzzy and phonetic matching)")] string agent,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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

        // Step 6: Present data
        return $"The total number of transactions completed by {agent} {(year.HasValue ? "in " + year.Value : "")} is {total}.";
    }


    [McpServerTool]
    [Description("Retrieves the most frequently occurring ZIP code in real estate transactions. " +
        "Allows identifying popular areas or neighborhoods for property transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are counted. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about popular ZIP codes, most common areas, or top locations for real estate transactions. " +
        "Relevant for questions like: what's the most popular ZIP code, which area has the most property deals, show me top ZIP codes, or where are most properties located.")]
    [HttpGet("/reals/most-popular-zip")]
    public string GetMostPopularZip(
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")]
        string? agent = null,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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

        var popularZip = GetMostPopularValueFilteredReal(svc, t => t.SubjectPostalCode, agent, year, from, to);

        if (string.IsNullOrWhiteSpace(popularZip) || popularZip == "N/A")
        {
            return "No ZIP codes found for the selected filters.";
        }

        // Step 6: Present data
        return $"The most popular ZIP code among the real transactions is {popularZip}.";
    }

    [McpServerTool]
    [Description("Retrieves a ranked list of cities with the highest number of real estate transactions. " +
        "Allows identifying the most active cities for property transactions and market trends. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), date range (from/to dates by closed date), and top count (maximum number of results). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are counted. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about popular cities, most active real estate markets, or top locations for property transactions. " +
        "Relevant for questions like: which cities have the most property deals, what are the top cities for real estate, show me the most active markets, or where are most property transactions happening.")]
    [HttpGet("/reals/top-cities")]
    public string GetTopCities(
        [Description("Optional filter: Maximum number of top cities to return (default is 10)")]
        int top = 10,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                    .Where(t => !string.IsNullOrWhiteSpace(t.SubjectCity));

        var grouped = data.GroupBy(t => new { City = t.SubjectCity, State = t.SubjectState })
                          .OrderByDescending(g => g.Count())
                          .Take(top)
                          .Select(g => new { City = g.Key.City, State = g.Key.State ?? "N/A", Transactions = g.Count() });

        if (!grouped.Any())
            return "No city data found for the selected filters.";

        List<TopCityResult> results = JsonSerializer.Deserialize<List<TopCityResult>>(JsonSerializer.Serialize(grouped))!;

        // Step 6: Present data
        var formatted = results.Select(r => $"{r.City}, {r.State} with {r.Transactions} transactions")
                               .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} cities with the most real transactions are: {formatted}.";
    }

    [McpServerTool]
    [Description("Retrieves a ranked list of top-performing agents based on real estate transaction count. " +
        "Allows identifying the most active or productive real estate agents in the organization. " +
        "Supports optional filtering by year (specific year) and date range (from/to dates by closed date). " +
        "When year filter is applied, only transactions closed in that specific year are counted. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about top real estate agents, best performers, most active agents, or agent rankings. " +
        "Relevant for questions like: who are the top real estate agents, which agents have the most property deals, show me the best performers, or rank agents by activity.")]
    [HttpGet("/reals/top-agents")]
    public string GetTopAgents(
        [Description("Optional filter: Maximum number of top agents to return (default is 10)")]
        int top = 10,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        // (Not applicable as this lists top agents)

        // Step 2: Match phonetics for agent
        // (Not applicable as this lists top agents)

        // Step 3: Get user related to phonetic results
        // (Not applicable as this lists top agents)

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
        var authError = TestMcpApi.Helpers.Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        var formatted = results.Select(r => $"{r.Agent} with {r.Transactions} transactions")
                               .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} agents for real estate transactions are: {formatted}.";
    }


    [McpServerTool]
    [Description("Retrieves the IRS Form 1099 total commission amount for a specific agent for a given tax year. " +
        "Allows calculating total taxable income earned by a real estate agent from commissions. " +
        "Supports fuzzy name matching and phonetic search for agent identification. " +
        "Year parameter is required and specifies the tax year for the 1099 calculation. " +
        "Use this when the user asks about agent earnings, agent tax income, or agent 1099 information. " +
        "Relevant for questions like: what's the 1099 for this agent, how much taxable income did the agent earn, show me agent 1099 for the year, or what are the agent's commission totals.")]
    [HttpGet("/reals/agent-1099")]
    public string GetAgent1099(
        [Description("Name of the agent whose 1099 to retrieve (supports fuzzy and phonetic matching)")]
        string agent,
        [Description("Tax year for the 1099 calculation (e.g., 2024, 2025)")]
        int year,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(agent))
        {
            return "Agent name must be provided.";
        }

        decimal total1099 = svc.GetAgent1099(agent, year);

        // Step 6: Present data
        return $"The total 1099 income for agent {agent} in {year} is: {total1099:C}";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive real estate transaction statistics for a specific lender including total count and amount analytics. " +
        "Allows analyzing lender performance with metrics like average, highest, and lowest transaction amounts. " +
        "Returns total transactions, average transaction amount, maximum transaction amount, and minimum transaction amount. " +
        "Use this when the user asks about lender statistics, lender performance, or lender transaction analytics. " +
        "Relevant for questions like: show me lender statistics, what are the transaction amounts for this lender, how is this lender performing, or give me lender analytics.")]
    [HttpGet("/reals/lender-stats")]
    public string GetLenderStats(
        [Description("Name of the lender whose statistics to retrieve")]
        string lender,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        // (Not applicable as no agent parameter exists)

        // Step 2: Match phonetics for agent
        // (Not applicable as no agent parameter exists)

        // Step 3: Get user related to phonetic results
        // (Not applicable as no agent parameter exists)

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
        var authError = TestMcpApi.Helpers.Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"The lender {lender} has {result.TotalTransactions} transactions. " +
               $"Average transaction amount: {result.AvgAmount:C}, " +
               $"Maximum amount: {result.MaxAmount:C}, " +
               $"Minimum amount: {result.MinAmount:C}.";
    }


    [McpServerTool]
    [Description("Retrieves the most frequently occurring type for a specified category (Property, Transaction, Client, Real, or Real Sub). " +
        "Allows identifying popular transaction types, property types, client types, or other characteristics in the portfolio. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are counted. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Category parameter must be one of: Property, Transaction, Client, Real, or Real Sub. " +
        "Use this when the user asks about most common types, popular categories, or dominant transaction characteristics. " +
        "Relevant for questions like: what's the most popular property type, which transaction type is most common, show me the most frequent client type, or what real estate type do we handle most.")]
    [HttpGet("/reals/most-popular-type/{category}")]
    public string GetMostPopularType(
        [Description("Type category to analyze - must be one of: Property, Transaction, Client, Real, or Real Sub")] string category,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "not available right now";
        }
        else
        {
            // Normalize the category parameter
            string normalizedCategory = category.Trim().ToLower();

            // Determine which field to query based on category
            Func<RealTransaction, string?> selector = normalizedCategory switch
            {
                "property" => t => t.PropType,
                "transaction" => t => t.TransactionType,
                "client" => t => t.ClientType,
                "real" => t => t.RealType,
                "real sub" or "realsub" => t => t.RealSubType,
                _ => throw new ArgumentException($"Invalid category '{category}'. Valid options are: Property, Transaction, Client, Real, or Real Sub.")
            };

            var data = FilterRealTransactions(svc, agent, year, from, to)
                .Where(t => !string.IsNullOrWhiteSpace(selector(t)));

            var result = data.GroupBy(t => selector(t), StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { Type = g.Key, Transactions = g.Count() })
                             .FirstOrDefault();

            if (result == null)
            {
                return $"No {normalizedCategory} type data available for the selected filters.";
            }

            type = $"{result.Type} with {result.Transactions} transactions";
        }

        // Step 6: Present data
        return $"The most popular {category.ToLower()} type is: {type}";
    }


    [McpServerTool]
    [Description("Retrieves real estate transactions filtered by a specific category type (property, transaction, client, real, or real sub). " +
        "Allows viewing all deals matching a particular type characteristic with detailed transaction information. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), date range (from/to dates by closed date), and top count (maximum number of results). " +
        "When agent filter is applied, only transactions for that agent are included. " +
        "When year filter is applied, only transactions closed in that specific year are returned. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Category parameter must be one of: property, transaction, client, real, or real sub. Type parameter specifies the specific value within that category. " +
        "Use this when the user asks for transactions of a specific type, deals matching criteria, or filtered property lists. " +
        "Relevant for questions like: show me all single family homes, list commercial property transactions, get deals for this property type, or find transactions by type.")]
    [HttpGet("/reals/by-type/{category}/{type}")]
    public string GetByType(
        [Description("Category to filter by - must be one of: property, transaction, client, real, or real sub")] string category,
        [Description("Specific type value to filter within the category (e.g., 'Single Family' for property category)")] string type,
        [Description("Optional filter: Maximum number of transactions to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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

        // Normalize the category parameter
        string normalizedCategory = category.Trim().ToLower();

        // Determine which field to query based on category
        Func<RealTransaction, string?> selector;
        string fieldName;

        switch (normalizedCategory)
        {
            case "property":
                selector = t => t.PropType;
                fieldName = "property type";
                break;
            case "transaction":
                selector = t => t.TransactionType ?? t.TransType;
                fieldName = "transaction type";
                break;
            case "client":
                selector = t => t.ClientType;
                fieldName = "client type";
                break;
            case "real":
                selector = t => t.RealType;
                fieldName = "real type";
                break;
            case "real sub":
            case "realsub":
            case "sub":
                selector = t => t.RealSubType;
                fieldName = "real sub type";
                break;
            default:
                return $"Invalid category '{category}'. Valid options are: property, transaction, client, real, or real sub.";
        }

        var data = FilterRealTransactions(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(selector(t)))
                   .Where(t => string.Equals(selector(t), type, StringComparison.OrdinalIgnoreCase))
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
                       RealTerm = t.RealTerm?.ToString(),
                       AppraisedValue = t.AppraisedValue,
                       PropertyAddress = t.SubjectAddress,
                       City = t.SubjectCity,
                       State = t.SubjectState
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for {fieldName} '{type}' using the selected filters.";

        // Step 6: Present data
        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, " +
            $"Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount?.ToString("C") ?? "N/A"}, " +
            $"Closed: {r.ActualClosedDate?.ToShortDateString() ?? "N/A"}")
            .Aggregate((a, b) => a + "; " + b);

        return $"The top {top} transactions for {fieldName} '{type}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }


    [McpServerTool]
    [Description("Retrieves real estate transactions associated with a specific NMLS (Nationwide Multistate Licensing System) number. " +
        "Allows viewing all deals linked to a licensed loan originator or mortgage professional. " +
        "Supports optional filtering by agent name, year (specific year), date range (from/to dates by closed date), and top count (maximum number of results). " +
        "When agent filter is applied, only transactions for that agent are included. " +
        "When year filter is applied, only transactions closed in that specific year are returned. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about NMLS transactions, loan originator deals, or mortgage professional activity. " +
        "Relevant for questions like: show me transactions for this NMLS number, what deals are associated with this license, list properties for an NMLS number, or find transactions by loan originator.")]
    [HttpGet("/reals/by-nmls-number/{nmlsNumber}")]
    public string GetByNMLSNumber(
        [Description("NMLS number to look up transactions for")]
        string nmlsNumber,
        [Description("Optional filter: Maximum number of transactions to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
    [Description("Retrieves comprehensive statistics for transaction prices including count, average, maximum, and minimum values. " +
        "Allows analyzing price distribution and ranges for real estate transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are counted. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about price statistics, pricing analytics, or transaction value metrics. " +
        "Relevant for questions like: what are the price statistics, show me average prices, what's the price range, or give me pricing analytics.")]
    [HttpGet("/reals/price-stats")]
    public string GetPriceStats(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter statistics by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                   .Where(t => t.Price.HasValue);

        if (!data.Any())
            return "No transactions with prices found for the selected filters.";

        var totalTransactions = data.Count();
        var avgPrice = data.Average(t => t.Price!.Value);
        var maxPrice = data.Max(t => t.Price!.Value);
        var minPrice = data.Min(t => t.Price!.Value);

        // Step 6: Present data
        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average price is {avgPrice:C}, the maximum price is {maxPrice:C}, and the minimum price is {minPrice:C}.";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive statistics for real estate terms including count, average, maximum, and minimum values. " +
        "Allows analyzing term distribution and ranges for real estate transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are included. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about term statistics, term analytics, or term value metrics. " +
        "Relevant for questions like: what are the real estate term statistics, show me average terms, what's the term range, or give me term analytics.")]
    [HttpGet("/reals/realterm-stats")]
    public string GetRealTermStats(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter statistics by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                   .Where(t => t.RealTerm.HasValue);

        if (!data.Any())
            return "No transactions with real term found for the selected filters.";

        var totalTransactions = data.Count();
        var avgTerm = data.Average(t => t.RealTerm!.Value);
        var maxTerm = data.Max(t => t.RealTerm!.Value);
        var minTerm = data.Min(t => t.RealTerm!.Value);

        // Step 6: Present data
        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average real term is {avgTerm}, the maximum real term is {maxTerm}, and the minimum real term is {minTerm}.";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive statistics for real estate amounts including count, average, maximum, and minimum values. " +
        "Allows analyzing amount distribution and ranges for real estate transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are included. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about real amount statistics, amount analytics, or real estate value metrics. " +
        "Relevant for questions like: what are the real amount statistics, show me average amounts, what's the amount range, or give me amount analytics.")]
    [HttpGet("/reals/realamount-stats")]
    public string GetRealAmountStats(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter statistics by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                   .Where(t => t.RealAmount.HasValue);

        if (!data.Any())
            return "No transactions with real amount found for the selected filters.";

        var totalTransactions = data.Count();
        var avgAmount = data.Average(t => t.RealAmount!.Value);
        var maxAmount = data.Max(t => t.RealAmount!.Value);
        var minAmount = data.Min(t => t.RealAmount!.Value);

        // Step 6: Present data
        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average real amount is {avgAmount:C}, the maximum real amount is {maxAmount:C}, and the minimum real amount is {minAmount:C}.";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive statistics for appraised values including count, average, maximum, and minimum values. " +
        "Allows analyzing appraisal distribution and ranges for real estate transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are included. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about appraised value statistics, appraisal analytics, or property valuation metrics. " +
        "Relevant for questions like: what are the appraised value statistics, show me average appraisals, what's the appraisal range, or give me valuation analytics.")]
    [HttpGet("/reals/appraisedvalue-stats")]
    public string GetAppraisedValueStats(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter statistics by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                agent = matchedAgent.AgentName ?? agent;
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
                   .Where(t => t.AppraisedValue.HasValue);

        if (!data.Any())
            return "No transactions with appraised value found for the selected filters.";

        var totalTransactions = data.Count();
        var avgValue = data.Average(t => t.AppraisedValue!.Value);
        var maxValue = data.Max(t => t.AppraisedValue!.Value);
        var minValue = data.Min(t => t.AppraisedValue!.Value);

        // Step 6: Present data
        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average appraised value is {avgValue:C}, the maximum appraised value is {maxValue:C}, and the minimum appraised value is {minValue:C}.";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive statistics for LTV (Loan-to-Value) ratios including count, average, maximum, and minimum values. " +
        "Allows analyzing LTV distribution and ranges for real estate transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are included. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about LTV statistics, loan-to-value analytics, or lending ratio metrics. " +
        "Relevant for questions like: what are the LTV statistics, show me average loan-to-value ratios, what's the LTV range, or give me lending ratio analytics.")]
    [HttpGet("/reals/ltv-stats")]
    public string GetLTVStats(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter statistics by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                   .Where(t => t.LTV.HasValue);

        if (!data.Any())
            return "No transactions with LTV values found for the selected filters.";

        var totalTransactions = data.Count();
        var avgLTV = data.Average(t => t.LTV!.Value);
        var maxLTV = data.Max(t => t.LTV!.Value);
        var minLTV = data.Min(t => t.LTV!.Value);

        // Step 6: Present data
        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average LTV is {avgLTV}, the maximum LTV is {maxLTV}, and the minimum LTV is {minLTV}.";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive statistics for interest rates including count, average, maximum, and minimum values. " +
        "Allows analyzing interest rate distribution and ranges for real estate transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are included. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about interest rate statistics, rate analytics, or lending rate metrics. " +
        "Relevant for questions like: what are the interest rate statistics, show me average interest rates, what's the rate range, or give me interest rate analytics.")]
    [HttpGet("/reals/interest-rate-stats")]
    public string GetInterestRateStats(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter statistics by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                   .Where(t => t.InterestRate.HasValue);

        if (!data.Any())
            return "No transactions with interest rate values found for the selected filters.";

        var totalTransactions = data.Count();
        var avgRate = data.Average(t => t.InterestRate!.Value);
        var maxRate = data.Max(t => t.InterestRate!.Value);
        var minRate = data.Min(t => t.InterestRate!.Value);

        // Step 6: Present data
        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average interest rate is {avgRate}, the maximum interest rate is {maxRate}, and the minimum interest rate is {minRate}.";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive statistics for TC (Transaction Coordinator) Fees including count, average, maximum, and minimum values. " +
        "Allows analyzing TC fee distribution and ranges for real estate transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates by closed date). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions closed in that specific year are included. " +
        "When date range filters are applied, only transactions closed within the from/to date range are included. " +
        "Use this when the user asks about TC fee statistics, transaction coordinator cost analytics, or fee metrics. " +
        "Relevant for questions like: what are the TC fee statistics, show me average TC fees, what's the fee range, or give me TC cost analytics.")]
    [HttpGet("/reals/tcfees-stats")]
    public string GetTCFeesStats(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")]
        string? agent = null,
        [Description("Optional filter: Year to filter statistics by closed date (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter statistics from by closed date (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter statistics to by closed date (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
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
                   .Where(t => t.TCFees.HasValue);

        if (!data.Any())
            return "No transactions with TC Fees values found for the selected filters.";

        var totalTransactions = data.Count();
        var avgFees = data.Average(t => t.TCFees!.Value);
        var maxFees = data.Max(t => t.TCFees!.Value);
        var minFees = data.Min(t => t.TCFees!.Value);

        // Step 6: Present data
        return $"For the selected filters, there are {totalTransactions} transactions. " +
               $"The average TC Fees is {avgFees:C}, the maximum TC Fees is {maxFees:C}, and the minimum TC Fees is {minFees:C}.";
    }

    [McpServerTool]
    [Description("Retrieves home inspection information for a specific property by address. " +
        "Allows viewing inspection details including inspector name, completion status, contact information, and notes. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns inspection name, done status, phone, email, and notes. " +
        "Use this when the user asks about home inspection details, inspector information, or inspection status. " +
        "Relevant for questions like: what's the home inspection information for this address, who did the home inspection, is the inspection complete, or show me inspection details.")]
    [HttpGet("/reals/home-inspection-info/{subjectAddress}")]
    public string GetHomeInspectionInfo(
        [Description("Property address to look up home inspection information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"Home Inspection Info for {subjectAddress}: Name: {info.Name}, Done: {info.Done}, " +
               $"Phone: {info.Phone}, Email: {info.Email}, Notes: {info.Notes}";
    }

    [McpServerTool]
    [Description("Retrieves pest inspection information for a specific property by address. " +
        "Allows viewing pest inspection details including inspector name, completion status, contact information, and notes. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns pest inspection name, done status, phone, email, and notes. " +
        "Use this when the user asks about pest inspection details, pest inspector information, or pest inspection status. " +
        "Relevant for questions like: what's the pest inspection information for this address, who did the pest inspection, is the pest inspection complete, or show me pest inspection details.")]
    [HttpGet("/reals/pest-inspection-info/{subjectAddress}")]
    public string GetPestInspectionInfo(
        [Description("Property address to look up pest inspection information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"Pest Inspection Info for {subjectAddress}: Name: {info.Name}, Done: {info.Done}, " +
               $"Phone: {info.Phone}, Email: {info.Email}, Notes: {info.Notes}";
    }

    [McpServerTool]
    [Description("Retrieves escrow information for a specific property by address. " +
        "Allows viewing escrow details including company, officer, contact information, escrow number, and method. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns escrow company, phone, officer name, officer email, officer phone, escrow number, and method send type. " +
        "Use this when the user asks about escrow details, escrow company information, or escrow officer contact. " +
        "Relevant for questions like: what's the escrow information for this address, who is the escrow officer, what's the escrow number, or show me escrow details.")]
    [HttpGet("/reals/escrow-info/{subjectAddress}")]
    public string GetEscrowInfo(
        [Description("Property address to look up escrow information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"Escrow Info for {subjectAddress}: Company: {info.Company}, Phone: {info.Phone}, " +
               $"Officer: {info.Officer}, Officer Email: {info.OfficerEmail}, Officer Phone: {info.OfficerPhone}, " +
               $"Escrow Number: {info.EscrowNumber}, Method Send Type: {info.MethodSendType}";
    }

    [McpServerTool]
    [Description("Retrieves title company information for a specific property by address. " +
        "Allows viewing title company details including company name and contact phone. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns title company name and phone number. " +
        "Use this when the user asks about title company details, title company name, or title company contact. " +
        "Relevant for questions like: what's the title company for this address, who is handling title, what's the title company phone number, or show me title company information.")]
    [HttpGet("/reals/title-company-info/{subjectAddress}")]
    public string GetTitleCompanyInfo(
        [Description("Property address to look up title company information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"Title Company Info for {subjectAddress}: Company: {info.Company}, Phone: {info.Phone}";
    }

    [McpServerTool]
    [Description("Retrieves appraisal company information for a specific property by address. " +
        "Allows viewing appraisal company details including company name and contact phone. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns appraisal company name and phone number. " +
        "Use this when the user asks about appraisal company details, appraiser name, or appraisal company contact. " +
        "Relevant for questions like: what's the appraisal company for this address, who did the appraisal, what's the appraiser's phone number, or show me appraisal company information.")]
    [HttpGet("/reals/appraisal-company-info/{subjectAddress}")]
    public string GetAppraisalCompanyInfo(
        [Description("Property address to look up appraisal company information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"Appraisal Company Info for {subjectAddress}: Company: {info.Company}, Phone: {info.Phone}";
    }

    [McpServerTool]
    [Description("Retrieves real estate transaction coordinator (TC) information for a specific property by address. " +
        "Allows viewing TC details including flag, coordinator name, and fees. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns TC flag, name, and fees. " +
        "Use this when the user asks about transaction coordinator details, TC fees, or TC contact information. " +
        "Relevant for questions like: what's the TC information for this address, who is the transaction coordinator, what are the TC fees, or show me TC details.")]
    [HttpGet("/reals/tc-info/{subjectAddress}")]
    public string GetTCInfo(
        [Description("Property address to lookup transaction coordinator information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"Transaction Coordinator Info for {subjectAddress}: Flag: {info.Flag}, TC: {info.TC}, Fees: {info.Fees}";
    }

    [McpServerTool]
    [Description("Retrieves payment information for a specific property by address. " +
        "Allows viewing payment details including expected date, payable to, agent address, processor amount, check amount, mailing fee, and notes. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns expected date, payable to, agent address, processor amount, check amount, routing number, mailing fee, notes, and clear date. " +
        "Use this when the user asks about payment details, payment status, or payment instructions for a property. " +
        "Relevant for questions like: what's the payment information for this address, who do I pay, how much is the payment, or show me payment details.")]
    [HttpGet("/reals/payment-info/{subjectAddress}")]
    public string GetPaymentInfo(
        [Description("Property address to lookup payment information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
        return $"Payment Info for {subjectAddress}: Expected Date: {info.ExpectedDate}, Payable To: {info.PayableTo}, Agent Address: {info.AgentAddress}, Processor Amount: {info.ProcessorAmount}, Check Amount: {info.CheckAmount}, Routing Number: {info.RoutingNumber}, Mailing Fee: {info.MailingFee}, Notes: {info.Notes}, Clear Date: {info.ClearDate}";
    }

    [McpServerTool]
    [Description("Retrieves bank information for a specific property by address. " +
        "Allows viewing banking details including incoming and outgoing bank information, account details, and transaction amounts. " +
        "Supports fuzzy name matching and phonetic search for address identification. " +
        "Returns incoming bank, outgoing bank, bank name, account name, routing number, account number, amount retained by KAM, and amount paid to KAM agent. " +
        "Use this when the user asks about banking details, bank contact information, or transaction banking information for a property. " +
        "Relevant for questions like: what's the bank information for this address, who handles the banking, what are the banking details, or show me bank information.")]
    [HttpGet("/reals/bank-info/{subjectAddress}")]
    public string GetBankInfo(
        [Description("Property address to lookup bank information for (supports fuzzy and phonetic matching)")]
        string subjectAddress,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on subjectAddress parameter
        var allAddresses = svc.GetRealTransactions().Result
            .Where(t => !string.IsNullOrWhiteSpace(t.SubjectAddress))
            .Select(t => new { SubjectAddress = t.SubjectAddress })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for address
        var matchedAddress = TestMcpApi.Helpers.Common.MatchPhonetic(allAddresses, subjectAddress, a => a.SubjectAddress ?? string.Empty);

        // Step 3: Get address related to phonetic results
        if (matchedAddress != null)
        {
            subjectAddress = matchedAddress.SubjectAddress ?? subjectAddress;
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
        var authError = TestMcpApi.Helpers.Common.CheckSpecificAuthorization(_httpContextAccessor, null, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        // Step 5: Get data if authorized
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

        // Step 6: Present data
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
