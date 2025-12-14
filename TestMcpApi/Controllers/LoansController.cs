using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Services;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController] // Use ApiController attributes if integrating into an existing Web API
public class LoansController : ControllerBase
{
    // AGENT-RELATED TOOLS

    [McpServerTool]
    [Description("List transactions by agent name")]
    [HttpGet("/loans/{agent}")]
    public string GetTransactionsByAgent(
        [Description("The name of the agent, and maybe the year")] LoanTransactionService svc,
        string agent,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var agents = new[] { agent };
        var data = Filter(svc, agents, year, from, to);
        return JsonSerializer.Serialize(data);
    }

    [McpServerTool]
    [Description("Get Agent responsible for a specific loan")]
    [HttpGet("/loans/{loanId}")]
    public string GetAgentByLoan(
        [Description("who is the agent responsible for the loan")] LoanTransactionService svc,
        string loanId)
        => svc.GetByLoanNumber(loanId)?.AgentName ?? "Not found";

    [McpServerTool]
    [Description("Get total number of transactions for an agent")]
    [HttpGet("/loans/total/{agent}")]
    public string GetTotalTransactionsByAgent(
        [Description("The name of the agent, and maybe the year")] LoanTransactionService svc,
        string agent,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var agents = new[] { agent };
        var count = Filter(svc, agents, year, from, to).Count();
        return count.ToString();
    }

    [McpServerTool]
    [Description("Get top agents ranked by number of transactions")]
    [HttpGet("/top-agents")]
    public string GetTopAgents(
        [Description("who are the top agents")] LoanTransactionService svc,
        int top = 10,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = Filter(svc, null, year, from, to);

        var result = data.GroupBy(t => t.AgentName)
                         .OrderByDescending(g => g.Count())
                         .Take(top)
                         .Select(g => new { Agent = g.Key, Transactions = g.Count() });

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool]
    [Description("Get all agent names, optionally sorted")]
    [HttpGet("/agents")]
    public string GetAllAgents(
        [Description("The name of the agent, and maybe the year")] LoanTransactionService svc,
        bool sortByName = true,
        bool descending = false)
        => JsonSerializer.Serialize(svc.GetAllAgents(sortByName, descending));

    // LOAN-RELATED TOOLS

    [McpServerTool]
    [Description("Get subject address by loan number")]
    [HttpGet("/loans/{loanId}")]
    public string GetAddressByLoan(
        [Description("What is the address of the property fo this specific loan")] LoanTransactionService svc,
        string loanId)
        => svc.GetSubjectAddress(loanId) ?? "Not found";


    [McpServerTool]
    [Description("List loans in a specific state")]
    [HttpGet("/loans/{state}")]
    public string GetLoansByState(
        [Description("The state")] LoanTransactionService svc,
        string state,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = Filter(svc, null, year, from, to)
                   .Where(t => t.SubjectState != null && t.SubjectState.Equals(state, StringComparison.OrdinalIgnoreCase));
        return JsonSerializer.Serialize(data);
    }

    [McpServerTool]
    [Description("Get lender for a specific loan")]
    [HttpGet("/loans/{loanId}")]
    public string GetLender(
        [Description("Who is the lender for a specific loan")] LoanTransactionService svc,
        string loanId)
        => svc.GetLender(loanId) ?? "Not found";


    [McpServerTool]
    [Description("Get LTV of a specific loan")]
    [HttpGet("/loans/{loanId}")]
    public string GetLTV(
        [Description("What is the LTV for a specific loan")] LoanTransactionService svc,
        string loanId)
        => svc.GetLTV(loanId)?.ToString() ?? "Not found";

    [McpServerTool]
    [Description("Get the IDs of loans with a specific status (Active = Submitted / Not Submitted)")]
    [HttpGet("/loans/{status}")]
    public string GetLoanIdsByStatus(
        [Description("The status of loans")] LoanTransactionService svc,
        string status,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var loans = Filter(svc, null, year, from, to)
                    .Where(t => t.Active != null && t.Active.Equals(status, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.LoanTransID);
        return JsonSerializer.Serialize(loans);
    }

    [McpServerTool]
    [Description("Get loans that haven't been closed yet")]
    [HttpGet("/loans/open")]
    public string GetOpenLoans(
        [Description("The status of loans")] LoanTransactionService svc,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var loans = Filter(svc, null, year, from, to)
                    .Where(t => t.ActualClosedDate == null);
        return JsonSerializer.Serialize(loans);
    }


    //POPULARITY TOOLS

    [McpServerTool]
    [Description("GetMostPopularZip")]
    [HttpGet("/loans/zips")]
    public string GetMostPopularZip(
        [Description("The status of loans")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.SubjectPostalCode, new[] { agent! }, year, from, to);

    [McpServerTool]
    [Description("Get top cities")]
    [HttpGet("/top-cities")]
    public string GetTopCities(
        [Description("what are the top cities")] LoanTransactionService svc,
        int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var agents = new[] { agent! };
        var data = Filter(svc, agents, year, from, to);

        var result = data.GroupBy(t => t.SubjectCity)
                        .OrderByDescending(g => g.Count())
                        .Take(top)
                        .Select(g => new { City = g.Key, Count = g.Count() });

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool]
    [Description("Most popular property type")]
    [HttpGet("/top-property-type")]
    public string GetMostPopularPropType(
        [Description("What is the most popular property type")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.PropType, new[] { agent! }, year, from, to);


    [McpServerTool]
    [Description("Most popular transaction type")]
    [HttpGet("/top-transaction-type")]
    public string GetMostPopularTransactionType(
        [Description("What is the most popular transaction type")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.TransactionType, new[] { agent! }, year, from, to);


    [McpServerTool]
    [Description("Most popular mortgage type")]
    [HttpGet("/top-mortgage-type")]
    public string GetMostPopularMortgageType(
        [Description("What is the most popular mortgage type")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.MortgageType, new[] { agent! }, year, from, to);

    [McpServerTool]
    [Description("Most popular brokering type")]
    [HttpGet("/top-brokering-type")]
    public string GetMostPopularBrokeringType(
        [Description("What is the most popular brokering type")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.BrokeringType, new[] { agent! }, year, from, to);

    [McpServerTool]
    [Description("Most popular loan type")]
    [HttpGet("/top-loan-type")]
    public string GetMostPopularLoanType(
        [Description("What is the most popular loan type")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.LoanType, new[] { agent! }, year, from, to);

    [McpServerTool]
    [Description("Most popular escrow method send type")]
    [HttpGet("/top-escrow-send-type")]
    public string GetMostPopularEscrowMethod(
        [Description("What is the most popular escrow method send type")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.EscrowMethodSendType, new[] { agent! }, year, from, to);

    [McpServerTool]
    [Description("Most popular title company")]
    [HttpGet("/top-title-company")]
    public string GetMostPopularTitleCompany(
        [Description("What is the most popular title company")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.TitleCompany, new[] { agent! }, year, from, to);

    [McpServerTool]
    [Description("Most popular escrow company")]
    [HttpGet("/top-escrow-company")]
    public string GetMostPopularEscrowCompany(
        [Description("What is the most popular escrow company")] LoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
        => GetMostPopularValueFiltered(svc, t => t.EscrowCompany, new[] { agent! }, year, from, to);



    //HELPERS
    private static IEnumerable<LoanTransaction> Filter(
        LoanTransactionService svc,
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = svc.GetLoanTransactions().Result.AsEnumerable();

        if (agents != null && agents.Any())
            data = data.Where(t => t.AgentName != null && agents.Contains(t.AgentName, StringComparer.OrdinalIgnoreCase));

        if (year.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value.Year == year.Value);

        if (from.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value >= from.Value);

        if (to.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value <= to.Value);

        return data;
    }

    private static string GetMostPopularValueFiltered(
        LoanTransactionService svc,
        Func<LoanTransaction, string?> selector,
        IEnumerable<string>? agents = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = Filter(svc, agents, year, from, to)
                   .Where(t => !string.IsNullOrEmpty(selector(t)));

        var key = data.GroupBy(selector)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault()?.Key ?? "N/A";

        return key;
    }

}
