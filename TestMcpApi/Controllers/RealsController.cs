using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
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

    public RealsController(IRealTransactionService realTransactionService, IConfiguration configuration)
    {
        svc = realTransactionService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
    }

    [McpServerTool]
    [Description("Get details of a real transaction by its RealTransID")]
    [HttpGet("/real-transaction/{realTransID}")]
    public string GetRealTransactionById(
        [Description("What is the real transaction information for RealTransID?")] string realTransID)
    {
        if (svc == null || !string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "Real transactions data is not available right now.";


        if (string.IsNullOrWhiteSpace(realTransID))
            return "Please provide a valid RealTransID.";

        var transaction = svc.GetRealTransactionById(realTransID).Result;

        if (transaction == null)
            return $"No transaction found with RealTransID '{realTransID}'.";

        var dto = new RealTransactionDto
        {
            RealTransID = transaction.RealTransID,
            ClientFullName = $"{transaction.ClientFirstName} {transaction.ClientMiddleName} {transaction.ClientLastName}".Trim(),
            AgentName = transaction.AgentName,
            SubjectAddress = transaction.SubjectAddress,
            TransactionType = transaction.TransactionType,
            RealAmount = transaction.RealAmount,
            ActualClosedDate = transaction.ActualClosedDate
        };

        string result = $"Real transaction {dto.RealTransID}: Client '{dto.ClientFullName}', handled by Agent '{dto.AgentName}', " +
                        $"Property at '{dto.SubjectAddress}', Transaction Type '{dto.TransactionType}', Amount '{dto.RealAmount?.ToString("C") ?? "N/A"}', " +
                        $"Actual Closed Date '{dto.ActualClosedDate?.ToString("yyyy-MM-dd") ?? "N/A"}'.";

        return result;
    }

    [McpServerTool]
    [Description("List real estate transactions by agent name")]
    [HttpGet("/reals/{agent}")]
    public string GetRealTransactionsByAgent(
         [Description("List the transactions made by the agent, during the selected year or date range")]
         string agent,
         int top = 10,
         int? year = null,
         DateTime? from = null,
         DateTime? to = null)
    {
        string transactions = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            transactions = "not available right now";
            return transactions;
        }

        var agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                    .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID))
                    .Take(top)
                    .Select(t => new
                    {
                        ID = t.RealTransID,
                        Type = t.TransactionType ?? t.TransType,
                        Amount = t.RealAmount ?? t.PurchasePrice,
                        Address = t.SubjectAddress,
                        ClosedDate = t.ActualClosedDate
                    });

        if (!data.Any())
            return $"No real estate transactions found for agent {agent} using the selected filters.";

        List<RealTransactionDto> results =
            JsonSerializer.Deserialize<List<RealTransactionDto>>(JsonSerializer.Serialize(data))!;

        transactions = results
            .Select(r =>
                "Transaction #" + r.RealTransID +
                ", Amount: " + (r.RealAmount?.ToString() ?? "N/A") +
                ", Type: " + r.TransactionType +
                ", Address: " + r.SubjectAddress)
            .Aggregate((a, b) => a + ", " + b);

        return $"The transactions made by {agent}, during the year {year} are: {transactions}";
    }

    [McpServerTool]
    [Description("List real estate transactions in a specific state")]
    [HttpGet("/reals/state/{state}")]
    public string GetRealTransactionsByState(
        [Description("List the real estate transactions located in the state")] string state,
        int top = 10,
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string transactions = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
                    .Where(t => !string.IsNullOrWhiteSpace(t.SubjectState))
                    .Where(t => string.Equals(t.SubjectState, state, StringComparison.OrdinalIgnoreCase))
                    .Take(top)
                    .Select(t => new
                    {
                        RealTransID = t.RealTransID,
                        ClientFullName = $"{t.ClientFirstName} {t.ClientLastName}".Trim(),
                        AgentName = t.AgentName,
                        SubjectAddress = t.SubjectAddress,
                        TransactionType = t.TransactionType ?? t.TransType,
                        RealAmount = t.RealAmount ?? t.PurchasePrice,
                        ActualClosedDate = t.ActualClosedDate
                    });

        if (!data.Any())
            return $"No real estate transactions were found in the state {state} using the selected filters.";

        List<RealTransactionDto> results =
            JsonSerializer.Deserialize<List<RealTransactionDto>>(JsonSerializer.Serialize(data))!;

        transactions = results
            .Select(r =>
                "Transaction #" + r.RealTransID +
                ", Amount: " + (r.RealAmount?.ToString() ?? "N/A") +
                ", Type: " + r.TransactionType +
                ", Address: " + r.SubjectAddress)
            .Aggregate((a, b) => a + ", " + b);

        return $"The real estate transactions in the state {state} are: {transactions}";
    }

    [McpServerTool]
    [Description("List real estate transactions by title company")]
    [HttpGet("/reals/title-company/{titleCompany}")]
    public string GetRealTransactionsByTitleCompany(
        [Description("List the real estate transactions managed by the title company")] string titleCompany,
        int top = 10,
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string transactions = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
                        ActualClosedDate = t.ActualClosedDate
                    });

        if (!data.Any())
            return $"No real estate transactions were found for the title company {titleCompany} using the selected filters.";

        List<RealTransactionDto> results =
            JsonSerializer.Deserialize<List<RealTransactionDto>>(JsonSerializer.Serialize(data))!;

        transactions = results
            .Select(r =>
                "Transaction #" + r.RealTransID +
                ", Amount: " + (r.RealAmount?.ToString() ?? "N/A") +
                ", Type: " + r.TransactionType +
                ", Address: " + r.SubjectAddress)
            .Aggregate((a, b) => a + ", " + b);

        return $"The real estate transactions managed by the title company {titleCompany} are: {transactions}";
    }

    [McpServerTool]
    [Description("Get a real estate transaction by property address")]
    [HttpGet("/reals/property/{subjectAddress}")]
    public string GetRealTransactionByPropertyAddress(
        [Description("Get the real estate transaction information for the property at this address")] string subjectAddress)
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
            ActualClosedDate = transaction.ActualClosedDate
        };

        return $"Transaction #{result.RealTransID} for property {result.SubjectAddress}, handled by agent {result.AgentName}, client {result.ClientFullName}, type: {result.TransactionType}, amount: {(result.RealAmount?.ToString() ?? "N/A")}, closed on: {(result.ActualClosedDate?.ToShortDateString() ?? "N/A")}";
    }

    [McpServerTool]
    [Description("List real estate transactions by escrow company")]
    [HttpGet("/reals/escrow/{escrowCompany}")]
    public string GetTransactionsByEscrowCompany(
        [Description("List the real estate transactions handled by this escrow company")] string escrowCompany,
        int top = 10,
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string transactions = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for escrow company {escrowCompany} using the selected filters.";

        transactions = data.Select(r =>
            $"Transaction #{r.RealTransID} for property {r.SubjectAddress}, handled by agent {r.AgentName}, client {r.ClientFullName}, type: {r.TransactionType}, amount: {(r.RealAmount?.ToString() ?? "N/A")}, closed on: {(r.ActualClosedDate?.ToShortDateString() ?? "N/A")}"
        ).Aggregate((a, b) => a + "\n" + b);

        return $"The top {data.Count} transactions for escrow company {escrowCompany} are:\n{transactions}";
    }

    [McpServerTool]
    [Description("Get the lender name for a specific property address")]
    [HttpGet("/reals/lender")]
    public string GetLenderByPropertyAddress(
        [Description("Who is the lender for the property with this address?")] string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(subjectAddress))
        {
            return "No property address was provided.";
        }

        var transaction = svc.GetRealTransactions().Result
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.SubjectAddress) &&
                                 string.Equals(t.SubjectAddress, subjectAddress, StringComparison.OrdinalIgnoreCase));

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.LenderName))
        {
            return $"No lender found for the property address {subjectAddress}.";
        }

        return $"The lender for the property at {subjectAddress} is {transaction.LenderName}.";
    }

    [McpServerTool]
    [Description("Get the LTV (Loan-to-Value) for a specific property address")]
    [HttpGet("/reals/ltv")]
    public string GetLTVByPropertyAddress(
        [Description("What is the LTV for the property with this address?")] string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(subjectAddress))
        {
            return "No property address was provided.";
        }

        var transaction = svc.GetRealTransactions().Result
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.SubjectAddress) &&
                                 string.Equals(t.SubjectAddress, subjectAddress, StringComparison.OrdinalIgnoreCase));

        if (transaction == null || !transaction.LTV.HasValue)
        {
            return $"No LTV found for the property address {subjectAddress}.";
        }

        return $"The LTV for the property at {subjectAddress} is {transaction.LTV.Value:F2}.";
    }

    [McpServerTool]
    [Description("Get the total number of transactions made by a specific agent")]
    [HttpGet("/reals/total-transactions/agent")]
    public string GetTotalTransactionsByAgent(
        [Description("How many transactions has the agent completed?")] string agent,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(agent))
        {
            return "No agent name was provided.";
        }

        var agents = new[] { agent };
        var data = FilterRealTransactions(svc, agents, year, from, to)
            .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID));

        int total = data.Count();

        if (total == 0)
        {
            return $"No transactions found for agent {agent} using the selected filters.";
        }

        return $"The total number of transactions completed by {agent} {(year.HasValue ? "in " + year.Value : "")} is {total}.";
    }

    [McpServerTool]
    [Description("Get the total number of transactions associated with a specific lender")]
    [HttpGet("/reals/total-transactions/lender")]
    public string GetTotalTransactionsByLender(
        [Description("How many transactions have been handled by the lender?")] string lender,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(lender))
        {
            return "No lender name was provided.";
        }

        var data = FilterRealTransactions(svc, null, year, from, to)
            .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID) && !string.IsNullOrWhiteSpace(t.LenderName))
            .Where(t => string.Equals(t.LenderName, lender, StringComparison.OrdinalIgnoreCase));

        int total = data.Count();

        if (total == 0)
        {
            return $"No transactions found for lender {lender} using the selected filters.";
        }

        return $"The total number of transactions handled by {lender} {(year.HasValue ? "in " + year.Value : "")} is {total}.";
    }

    [McpServerTool]
    [Description("Get the property address by real transaction ID")]
    [HttpGet("/reals/subject-address/{realTransId}")]
    public string GetSubjectAddressById(
        [Description("What is the property address for the real transaction ID?")] string realTransId)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        if (string.IsNullOrWhiteSpace(realTransId))
        {
            return "No real transaction ID was provided.";
        }

        var transaction = svc.GetRealTransactionById(realTransId).Result;

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.SubjectAddress))
        {
            return $"No property address found for real transaction ID {realTransId}.";
        }

        return $"The property address for real transaction ID {realTransId} is {transaction.SubjectAddress}.";
    }

    [McpServerTool]
    [Description("Get the most popular ZIP code among real transactions")]
    [HttpGet("/reals/most-popular-zip")]
    public string GetMostPopularZip(
        [Description("Which ZIP code appears most frequently among the real transactions?")]
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var popularZip = GetMostPopularValueFilteredReal(svc, t => t.SubjectPostalCode, agents, year, from, to);

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
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
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
    [Description("Get all agent names, optionally sorted")]
    [HttpGet("/agents")]
    public string GetAllAgents(
        [Description("List all agent names, sorted")]
        bool sortByName = true,
        bool descending = false)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The agent names are not available right now.";
        }

        var agents = svc.GetAllAgents(sortByName, descending)
            .Where(a => !string.IsNullOrWhiteSpace(a)).ToList();

        if (!agents.Any())
        {
            return "There are no agents available.";
        }

        var names = agents.Aggregate((a, b) => a + ", " + b);

        return $"The agent names are: {names}.";
    }

    [McpServerTool]
    [Description("List all title companies involved in real estate transactions")]
    [HttpGet("/reals/all-title-companies")]
    public string GetAllTitleCompanies()
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = svc.GetRealTransactions().Result
                      .Where(t => !string.IsNullOrWhiteSpace(t.TitleCompany))
                      .Select(t => t.TitleCompany!.Trim())
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderBy(tc => tc)
                      .ToList();

        if (!data.Any())
            return "No title companies found in the real estate transactions.";

        var formatted = string.Join(", ", data);
        return $"The title companies involved in real estate transactions are: {formatted}.";
    }

    [McpServerTool]
    [Description("List real estate transactions filtered by agents and/or years")]
    [HttpGet("/reals/transactions-by-agents-years")]
    public string GetTransactionsByAgentsAndYears(
        [Description("Which agents' transactions do you want to see? Provide a comma-separated list.")]
        string? agents = null,
        [Description("Which year's transactions do you want to see?")]
        int? year = null,
        int top = 10,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agentList = null;
        if (!string.IsNullOrWhiteSpace(agents))
            agentList = agents.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var data = FilterRealTransactions(svc, agentList, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID) && t.RealAmount.HasValue)
                   .Take(top)
                   .Select(t => new
                   {
                       t.RealTransID,
                       ClientName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Replace("  ", " ").Trim(),
                       t.AgentName,
                       t.SubjectAddress,
                       t.TransactionType,
                       t.RealAmount,
                       t.ActualClosedDate
                   });

        if (!data.Any())
        {
            string agentText = agentList != null ? string.Join(", ", agentList) : "all agents";
            string yearText = year != null ? year.ToString() : "all years";
            return $"No transactions found for {agentText} during {yearText} using the selected filters.";
        }

        var results = JsonSerializer.Deserialize<List<RealTransactionDto>>(JsonSerializer.Serialize(data))!;

        string formatted = results.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + "; " + b);

        return $"The filtered transactions are: {formatted}";
    }

    [McpServerTool]
    [Description("List real estate transactions filtered by a date range")]
    [HttpGet("/reals/transactions-by-date-range")]
    public string GetTransactionsByDateRange(
        [Description("From which date do you want to see transactions?")]
        DateTime? from = null,
        [Description("Up to which date do you want to see transactions?")]
        DateTime? to = null,
        int top = 10,
        string? agents = null,
        int? year = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agentList = null;
        if (!string.IsNullOrWhiteSpace(agents))
            agentList = agents.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var data = FilterRealTransactions(svc, agentList, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.RealTransID) && t.RealAmount.HasValue)
                   .Take(top)
                   .Select(t => new
                   {
                       t.RealTransID,
                       ClientName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Replace("  ", " ").Trim(),
                       t.AgentName,
                       t.SubjectAddress,
                       t.TransactionType,
                       t.RealAmount,
                       t.ActualClosedDate
                   });

        if (!data.Any())
        {
            string agentText = agentList != null ? string.Join(", ", agentList) : "all agents";
            string yearText = year != null ? year.ToString() : "all years";
            string fromText = from.HasValue ? from.Value.ToShortDateString() : "the beginning";
            string toText = to.HasValue ? to.Value.ToShortDateString() : "today";
            return $"No transactions found for {agentText} during {yearText} from {fromText} to {toText}.";
        }

        var results = JsonSerializer.Deserialize<List<RealTransactionDto>>(JsonSerializer.Serialize(data))!;

        string formatted = results.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + "; " + b);

        string fromDisplay = from.HasValue ? from.Value.ToShortDateString() : "the beginning";
        string toDisplay = to.HasValue ? to.Value.ToShortDateString() : "today";

        return $"The filtered transactions from {fromDisplay} to {toDisplay} are: {formatted}";
    }

    [McpServerTool]
    [Description("Get the total 1099 income for a specific agent during a year")]
    [HttpGet("/reals/agent-1099")]
    public string GetAgent1099(
        [Description("What is the agent's name you want to get the 1099 for?")]
        string agent,
        [Description("For which year do you want to get the 1099?")]
        int year)
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
        string lender)
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
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        string mostPopularType = GetMostPopularValueFilteredReal(svc, t => t.TransactionType, agents, year, from, to);

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
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        string mostPopularClientType = GetMostPopularValueFilteredReal(svc, t => t.ClientType, agents, year, from, to);

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
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        string mostPopularRealType = GetMostPopularValueFilteredReal(svc, t => t.RealType, agents, year, from, to);

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

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        string mostPopularRealSubType = GetMostPopularValueFilteredReal(svc, t => t.RealSubType, agents, year, from, to);

        if (mostPopularRealSubType == "N/A")
            return "No real sub-types found for the selected filters.";

        return $"The most popular real sub-type{(agent != null ? $" for {agent}" : "")} is: {mostPopularRealSubType}.";
    }

    [McpServerTool]
    [Description("List transactions by transaction type")]
    [HttpGet("/reals/by-trans-type")]
    public string GetByTransType(
        [Description("List the transactions that have the given transaction type")]
        string transType,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.TransType))
                   .Where(t => string.Equals(t.TransType, transType, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found with transaction type '{transType}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with transaction type '{transType}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by party presented")]
    [HttpGet("/reals/by-party-presented")]
    public string GetByPartyPresented(
        [Description("List the transactions where the specified party was presented")]
        string party,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.PartyPresented))
                   .Where(t => string.Equals(t.PartyPresented, party, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found where the party '{party}' was presented using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions where the party '{party}' was presented{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by client type")]
    [HttpGet("/reals/by-client-type")]
    public string GetByClientType(
        [Description("List the transactions for the specified client type")]
        string clientType,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.ClientType))
                   .Where(t => string.Equals(t.ClientType, clientType, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for client type '{clientType}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for client type '{clientType}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by property type")]
    [HttpGet("/reals/by-prop-type")]
    public string GetByPropType(
        [Description("List the transactions for the specified property type")]
        string propType,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.PropType))
                   .Where(t => string.Equals(t.PropType, propType, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for property type '{propType}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for property type '{propType}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by transaction type")]
    [HttpGet("/reals/by-trans-type")]
    public string GetByTransactionType(
        [Description("List the transactions for the specified transaction type")]
        string transType,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.TransType))
                   .Where(t => string.Equals(t.TransType, transType, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for transaction type '{transType}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for transaction type '{transType}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by finance info")]
    [HttpGet("/reals/by-finance-info")]
    public string GetByFinanceInfo(
        [Description("List the transactions with the specified finance information")]
        string financeInfo,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.FinanceInfo))
                   .Where(t => string.Equals(t.FinanceInfo, financeInfo, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found with finance info '{financeInfo}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with finance info '{financeInfo}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by CAR forms count")]
    [HttpGet("/reals/by-car-forms")]
    public string GetByCARForms(
        [Description("List the transactions with the specified number of CAR forms")]
        int carForms,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => t.CARForms.HasValue && t.CARForms.Value == carForms)
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found with {carForms} CAR forms using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with {carForms} CAR forms{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by NMLS number")]
    [HttpGet("/reals/by-nmls-number")]
    public string GetByNMLSNumber(
        [Description("List the transactions associated with the specified NMLS number")]
        string nmlsNumber,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.NMLSNumber) && t.NMLSNumber.Equals(nmlsNumber, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for NMLS number {nmlsNumber} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for NMLS number {nmlsNumber}{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by home inspection name")]
    [HttpGet("/reals/by-home-inspection-name")]
    public string GetByHomeInspectionName(
        [Description("List the transactions associated with the specified home inspection company or inspector")]
        string inspectionName,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.HomeInspectionName) &&
                               t.HomeInspectionName.Equals(inspectionName, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for home inspection '{inspectionName}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for home inspection '{inspectionName}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by pest inspection name")]
    [HttpGet("/reals/by-pest-inspection-name")]
    public string GetByPestInspectionName(
        [Description("List the transactions associated with the specified pest inspection company or inspector")]
        string inspectionName,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.PestInspectionName) &&
                               t.PestInspectionName.Equals(inspectionName, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for pest inspection '{inspectionName}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for pest inspection '{inspectionName}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions by TC flag")]
    [HttpGet("/reals/by-tc-flag")]
    public string GetByTCFlag(
        [Description("List the transactions associated with the specified TC flag")]
        string tcFlag,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.TCFlag) &&
                               t.TCFlag.Equals(tcFlag, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for TC flag '{tcFlag}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for TC flag '{tcFlag}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }


    [McpServerTool]
    [Description("List transactions by TC number")]
    [HttpGet("/reals/by-tc")]
    public string GetByTC(
        [Description("List the transactions associated with the specified TC number")]
        int tc,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => t.TC.HasValue && t.TC.Value == tc)
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found for TC number '{tc}' using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions for TC number '{tc}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions within a specified price range")]
    [HttpGet("/reals/by-price-range")]
    public string GetByPriceRange(
        [Description("List the transactions with RealAmount within the specified range")]
        decimal minPrice,
        decimal maxPrice,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => t.RealAmount.HasValue && t.RealAmount.Value >= minPrice && t.RealAmount.Value <= maxPrice)
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found within the price range {minPrice} - {maxPrice} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions within the price range {minPrice} - {maxPrice}{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List transactions within a specified Actual Closed Date range")]
    [HttpGet("/reals/by-closed-date")]
    public string GetByActualClosedDateRange(
        [Description("List the transactions that were actually closed within the specified date range")]
        DateTime from,
        DateTime to,
        int top = 10,
        string? agent = null,
        int? year = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => t.ActualClosedDate.HasValue && t.ActualClosedDate.Value >= from && t.ActualClosedDate.Value <= to)
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found between {from.ToShortDateString()} and {to.ToShortDateString()}{(agent != null ? $" for agent {agent}" : "")}.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {r.ActualClosedDate?.ToShortDateString()}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions closed between {from.ToShortDateString()} and {to.ToShortDateString()}{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List all open real estate transactions")]
    [HttpGet("/reals/open-transactions")]
    public string GetOpenTransactions(
        [Description("List the transactions that are still open and not closed")]
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => string.IsNullOrWhiteSpace(t.ActiveStatus) || !string.Equals(t.ActiveStatus, "Closed", StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No open transactions found{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} open transactions{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List all real estate transactions that have completed home inspections")]
    [HttpGet("/reals/home-inspection-done")]
    public string GetWithHomeInspectionDone(
        [Description("List the transactions where the home inspection has been completed")]
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.HomeInspectionDone) &&
                               string.Equals(t.HomeInspectionDone, "Yes", StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions with completed home inspections found{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with completed home inspections{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List all real estate transactions that have not completed home inspections")]
    [HttpGet("/reals/home-inspection-not-done")]
    public string GetWithHomeInspectionNotDone(
        [Description("List the transactions where the home inspection has not been completed")]
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => string.IsNullOrWhiteSpace(t.HomeInspectionDone) ||
                               !string.Equals(t.HomeInspectionDone, "Yes", StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions with pending home inspections found{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with pending home inspections{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List all real estate transactions that have completed pest inspections")]
    [HttpGet("/reals/pest-inspection-done")]
    public string GetWithPestInspectionDone(
        [Description("List the transactions where the pest inspection has been completed")]
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.PestInspectionDone) &&
                               string.Equals(t.PestInspectionDone, "Yes", StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions with completed pest inspections found{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with completed pest inspections{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List all real estate transactions that have not completed pest inspections")]
    [HttpGet("/reals/pest-inspection-not-done")]
    public string GetWithPestInspectionNotDone(
        [Description("List the transactions where the pest inspection has not been completed")]
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => string.IsNullOrWhiteSpace(t.PestInspectionDone) ||
                               !string.Equals(t.PestInspectionDone, "Yes", StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions with pending pest inspections found{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with pending pest inspections{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List real estate transactions by TC Fees range")]
    [HttpGet("/reals/tc-fees-range")]
    public string GetByTCFeesRange(
        [Description("List the transactions with TC Fees within the specified minimum and maximum values")]
        decimal? minFee = null,
        decimal? maxFee = null,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var years = year.HasValue ? new[] { year.Value } : null;

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => t.TCFees.HasValue &&
                              (!minFee.HasValue || t.TCFees.Value >= minFee.Value) &&
                              (!maxFee.HasValue || t.TCFees.Value <= maxFee.Value))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found with TC Fees in the range {(minFee.HasValue ? minFee.Value.ToString("C") : "N/A")} - {(maxFee.HasValue ? maxFee.Value.ToString("C") : "N/A")}{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with TC Fees in the selected range{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List real estate transactions by payable to")]
    [HttpGet("/reals/payable-to")]
    public string GetByPayableTo(
        [Description("List the transactions where the payment is payable to the specified recipient")]
        string payableTo,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.PayableTo) &&
                               string.Equals(t.PayableTo, payableTo, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found payable to '{payableTo}'{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions payable to '{payableTo}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("List real estate transactions by routing number")]
    [HttpGet("/reals/routing-number")]
    public string GetByRoutingNumber(
        [Description("List the transactions that use the specified routing number")]
        string routingNumber,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        IEnumerable<string>? agents = null;
        if (!string.IsNullOrWhiteSpace(agent))
            agents = new[] { agent };

        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(t.RoutingNumber) &&
                               string.Equals(t.RoutingNumber, routingNumber, StringComparison.OrdinalIgnoreCase))
                   .Take(top)
                   .Select(t => new RealTransactionDto
                   {
                       RealTransID = t.RealTransID,
                       ClientFullName = $"{t.ClientFirstName} {t.ClientMiddleName} {t.ClientLastName}".Trim(),
                       AgentName = t.AgentName,
                       SubjectAddress = t.SubjectAddress,
                       TransactionType = t.TransactionType,
                       RealAmount = t.RealAmount,
                       ActualClosedDate = t.ActualClosedDate
                   }).ToList();

        if (!data.Any())
            return $"No transactions found with routing number '{routingNumber}'{(agent != null ? $" for agent {agent}" : "")} using the selected filters.";

        string transactions = data.Select(r =>
            $"Transaction #{r.RealTransID}, Client: {r.ClientFullName}, Agent: {r.AgentName}, Address: {r.SubjectAddress}, Type: {r.TransactionType}, Amount: {r.RealAmount}, Closed: {(r.ActualClosedDate.HasValue ? r.ActualClosedDate.Value.ToShortDateString() : "N/A")}")
            .Aggregate((a, b) => a + ", " + b);

        return $"The top {top} transactions with routing number '{routingNumber}'{(agent != null ? $" for agent {agent}" : "")} are: {transactions}";
    }

    [McpServerTool]
    [Description("Get the transaction type for a property using its address")]
    [HttpGet("/reals/trans-type")]
    public string GetTransTypeByPropertyAddress(
        [Description("What is the transaction type for the property at this address?")]
        string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.TransType))
            return $"No transaction type found for the property at '{subjectAddress}'.";

        return $"The transaction type for the property at '{subjectAddress}' is: {transaction.TransType}";
    }

    [McpServerTool]
    [Description("Get the party presented for a property using its address")]
    [HttpGet("/reals/party-presented")]
    public string GetPartyPresentedByPropertyAddress(
        [Description("Who is the party presented for the property at this address?")]
        string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.PartyPresented))
            return $"No party presented found for the property at '{subjectAddress}'.";

        return $"The party presented for the property at '{subjectAddress}' is: {transaction.PartyPresented}";
    }

    [McpServerTool]
    [Description("Get the client type for a property using its address")]
    [HttpGet("/reals/client-type")]
    public string GetClientTypeByPropertyAddress(
        [Description("What is the client type for the property at this address?")]
        string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.ClientType))
            return $"No client type found for the property at '{subjectAddress}'.";

        return $"The client type for the property at '{subjectAddress}' is: {transaction.ClientType}";
    }

    [McpServerTool]
    [Description("Get the price for a property using its address")]
    [HttpGet("/reals/price")]
    public string GetPriceByPropertyAddress(
        [Description("What is the price for the property at this address?")]
        string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null || !transaction.Price.HasValue)
            return $"No price information found for the property at '{subjectAddress}'.";

        return $"The price for the property at '{subjectAddress}' is: {transaction.Price.Value:C}";
    }

    [McpServerTool]
    [Description("Get the number of CAR forms for a property using its address")]
    [HttpGet("/reals/carforms")]
    public string GetCARFormsByPropertyAddress(
        [Description("How many CAR forms are associated with the property at this address?")]
        string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null || !transaction.CARForms.HasValue)
            return $"No CAR forms information found for the property at '{subjectAddress}'.";

        return $"The property at '{subjectAddress}' has {transaction.CARForms.Value} CAR form(s).";
    }

    [McpServerTool]
    [Description("Get the NMLS number for a property using its address")]
    [HttpGet("/reals/nmls")]
    public string GetNMLSNumberByPropertyAddress(
        [Description("What is the NMLS number associated with the property at this address?")]
        string subjectAddress)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var transaction = svc.GetByPropertyAddress(subjectAddress);

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.NMLSNumber))
            return $"No NMLS number found for the property at '{subjectAddress}'.";

        return $"The NMLS number for the property at '{subjectAddress}' is {transaction.NMLSNumber}.";
    }

    [McpServerTool]
    [Description("Get statistics for transaction prices")]
    [HttpGet("/reals/price-stats")]
    public string GetPriceStats(
        [Description("What are the total number of transactions, average price, maximum price, and minimum price for the selected filters?")]
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
        [Description("What are the total number of transactions, average real term, maximum real term, and minimum real term for the selected filters?")]
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
        [Description("What are the total number of transactions, average real amount, maximum real amount, and minimum real amount for the selected filters?")]
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
        [Description("What are the total number of transactions, average appraised value, maximum appraised value, and minimum appraised value for the selected filters?")]
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
        [Description("What are the total number of transactions, average LTV, maximum LTV, and minimum LTV for the selected filters?")]
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
        [Description("What are the total number of transactions, average interest rate, maximum interest rate, and minimum interest rate for the selected filters?")]
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "The real estate transactions data is not available right now.";
        }

        var data = FilterRealTransactions(svc, agents, year, from, to)
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
    [HttpGet("/reals/home-inspection/{subjectAddress}")]
    public string GetHomeInspectionInfo(
        [Description("What is the home inspection information for the property located at this address?")]
        string subjectAddress)
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
    [HttpGet("/reals/pest-inspection/{subjectAddress}")]
    public string GetPestInspectionInfo(
        [Description("What is the pest inspection information for the property located at this address?")]
        string subjectAddress)
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
        string subjectAddress)
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
        string subjectAddress)
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
        string subjectAddress)
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
        string subjectAddress)
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
        string subjectAddress)
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
            IEnumerable<string>? agents = null,
            int? year = null,
            DateTime? from = null,
            DateTime? to = null)
    {
        var data = svc.GetRealTransactions().Result.AsEnumerable();

        // Filter by agents (case-insensitive)
        if (agents != null && agents.Any())
            data = data.Where(t => t.AgentName != null
                                && agents.Any(a => string.Equals(a, t.AgentName, StringComparison.OrdinalIgnoreCase)));

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
            IEnumerable<string>? agents = null,
            int? year = null,
            DateTime? from = null,
            DateTime? to = null)
    {
        var data = FilterRealTransactions(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrWhiteSpace(selector(t)))
                   .Where(t => selector(t) != "NULL");

        var key = data.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault()?.Key ?? "N/A";

        return key;
    }
}
