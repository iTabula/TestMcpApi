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
    [Description("List transactions by agent name (optional filters: year, from, to)")]
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
    {
        return svc.GetByLoanNumber(loanId)?.AgentName ?? "Not found";
    }

    [McpServerTool]
    [Description("Get total number of transactions for an agent (optional filters: year, from, to)")]
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
    [Description("Get top agents ranked by number of transactions (optional filters: year, from, to)")]
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
    {
        return JsonSerializer.Serialize(svc.GetAllAgents(sortByName, descending));
    }





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

}
