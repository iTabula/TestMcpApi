using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Classes;
using TestMcpApi.Services;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController] // Use ApiController attributes if integrating into an existing Web API
public class LoansController : ControllerBase
{
    private readonly LoanTransactionService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;

    public LoansController()
    {
        svc = new LoanTransactionService();
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
    }
    // AGENT-RELATED TOOLS
    // Mark a method as an MCP tool with a clear description
    [McpServerTool]
    [Description("Gets the current weather for a specific city")]
    [HttpGet("/weather/{city}")] // Can be a standard web API endpoint too
    public string GetCurrentWeather(
        [Description("The name of the city, e.g., 'San Diego'")] string city)
    {
        // In a real scenario, you would call an external API or service
        return $"It is sunny and 90°F in {city}.";
    }

    [McpServerTool]
    [Description("Get top agents ranked by number of transactions")]
    [HttpGet("/top-agents")]
    public string GetTopAgents(
        [Description("who are the top agents for KAM")] int top = 5,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        //return $"the top agent for KAM is Khaled El Henawy";

        string names = "";
        //LoanTransactionService svc = new LoanTransactionService();
        if (!svc.IsCsvLoaded)
        {
            names = "not availabale right now";
        }
        var data = Filter(svc, null, year, from, to);

        var result = data.GroupBy(t => t.AgentName)
                        .OrderByDescending(g => g.Count())
                        .Take(top)
                        .Select(g => new { Agent = g.Key, Transactions = g.Count() });

        List<TopAgentResult> results = JsonSerializer.Deserialize<List<TopAgentResult>>(JsonSerializer.Serialize(result))!;

        names = results.Select(r => r.Agent + " with " + r.Transactions + " transactions").Aggregate((a, b) => a + ", " + b);
        return $"The top {top} agents for KAM are: {names}";
    }

    [McpServerTool]
    [Description("List transactions by agent name")]
    [HttpGet("/loans/{agent}")]
    public string GetTransactionsByAgent(
        [Description("List the transactions made by the agent, during the year")]
        string agent,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string transactions = "";
        if (!svc.IsCsvLoaded)
        {
            transactions = "not availabale right now";
        }
        var agents = new[] { agent };
        var data = Filter(svc, agents, year, from, to).Select(g => new { ID = g.LoanTransID, LoanAmount = g.LoanAmount, LoanType = g.LoanType, LoanTerm = g.LoanTerm });
        List<TransactionsResult> results = JsonSerializer.Deserialize<List<TransactionsResult>>(JsonSerializer.Serialize(data))!;

        transactions = results.Select(r => "Loan #" + r.ID + ", Loan Amount: " + r.LoanAmount + ", Loan Type: " + r.LoanType + ", Loan Term: " + r.LoanTerm)
            .Aggregate((a, b) => a + ", " + b);
        return $"The transactions made by {agent}, during the year {year} are: {transactions}";
    }

    [McpServerTool]
    [Description("Get Agent responsible for a specific loan")]
    [HttpGet("/loans/{loanId}")]
    public string GetAgentByLoan(
    [Description("who is the agent responsible for the loan")]
        string loanId)
    {
        string agent = "";
        if (!svc.IsCsvLoaded)
        {
            agent = "not availabale right now";
        }
        string result = svc.GetByLoanNumber(loanId)?.AgentName ?? "Not found";
        return $"The agent responsible for the loan #{loanId} is {result}";
    }

    [McpServerTool]
    [Description("Get total number of transactions for an agent")]
    [HttpGet("/loans/total/{agent}")]
    public string GetTotalTransactionsByAgent(
        [Description("How many transactions did the agent make, in the year")]
        string agent,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (!svc.IsCsvLoaded)
        {
            return "The total number of transactions is not available right now.";
        }

        var agents = new[] { agent };
        var count = Filter(svc, agents, year, from, to).Count();

        if (year.HasValue)
        {
            return $"The total number of transactions made by {agent} in {year} is {count}.";
        }

        return $"The total number of transactions made by {agent} is {count}.";
    }

    [McpServerTool]
    [Description("Get all agent names, optionally sorted")]
    [HttpGet("/agents")]
    public string GetAllAgents(
        [Description("List all agent names, sorted")]
        bool sortByName = true,
        bool descending = false)
    {
        if (!svc.IsCsvLoaded)
        {
            return "The agent names are not available right now.";
        }

        var agents = svc.GetAllAgents(sortByName, descending).ToList();

        if (!agents.Any())
        {
            return "There are no agents available.";
        }

        var names = agents.Aggregate((a, b) => a + ", " + b);

        return $"The agent names are: {names}.";
    }


    // LOAN-RELATED TOOLS

    [McpServerTool]
    [Description("Get subject address by loan number")]
    [HttpGet("/loans/{loanId}")]
    public string GetAddressByLoan(
        [Description("What is the address of the property for this specific loan?")] string loanId)
    {
        if (!svc.IsCsvLoaded)
        {
            return "The address of the property is not available right now.";
        }

        var address = svc.GetSubjectAddress(loanId);

        if (string.IsNullOrEmpty(address))
        {
            return $"The address of the property for loan #{loanId} was not found.";
        }

        return $"The address of the property for loan #{loanId} is {address}.";
    }


    [McpServerTool]
    [Description("Get the loans in a specific state")]
    [HttpGet("/loans/{state}")]
    public string GetLoansByState(
    [Description("Which state do you want to get loans for?")] string state,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        string loansText = "";

        if (!svc.IsCsvLoaded)
        {
            loansText = $"The loans for state {state} are not available right now.";
            return loansText;
        }

        var data = Filter(svc, null, year, from, to)
                   .Where(t => t.SubjectState != null && t.SubjectState.Equals(state, StringComparison.OrdinalIgnoreCase));

        var result = data
                     .Select(t => new LoanSummaryResult
                     {
                         LoanID = t.LoanTransID ?? "N/A",
                         Agent = t.AgentName,
                         LoanAmount = t.LoanAmount,
                         LoanType = t.LoanType,
                         DateAdded = t.DateAdded?.ToShortDateString()
                     });

        List<LoanSummaryResult> results = JsonSerializer.Deserialize<List<LoanSummaryResult>>(JsonSerializer.Serialize(result))!;

        if (!results.Any())
        {
            loansText = $"There are no loans found for state {state}.";
            return loansText;
        }

        loansText = results
            .Select(r => $"Loan #{r.LoanID}, Agent: {r.Agent}, Amount: {r.LoanAmount}, Type: {r.LoanType}, Date Added: {r.DateAdded}")
            .Aggregate((a, b) => a + "; " + b);

        return $"The loans in state {state} are: {loansText}";
    }


    [McpServerTool]
    [Description("Get lender for a specific loan")]
    [HttpGet("/loans/{loanId}")]
    public string GetLender(
        [Description("Who is the lender for this specific loan?")] string loanId)
    {
        string lender = "";
        if (!svc.IsCsvLoaded)
        {
            lender = "not available right now";
        }
        else
        {
            lender = svc.GetLender(loanId) ?? "Not found";
        }

        return $"The lender for loan #{loanId} is {lender}";
    }


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


    // LOAN AMOUNT STATISTICS

    [McpServerTool]
    [Description("Average loan amount (overall, by agent or by year)")]
    [HttpGet("/loans/average")]
    public string GetAverageLoanAmount(
        [Description("What is the average loan amount")] LoanTransactionService svc,
        string? agent = null,
        int? year = null)
    {
        var loans = FilterByAgentAndYear(svc, agent, year).Where(t => t.LoanAmount.HasValue).Select(t => t.LoanAmount!.Value);
        return loans.Any() ? loans.Average().ToString("F2") : "N/A";
    }

    [McpServerTool]
    [Description("Highest loan amount (overall, by agent or by year)")]
    [HttpGet("/loans/max")]
    public string GetHighestLoanAmount(
        [Description("What is the highest loan amount")] LoanTransactionService svc,
        string? agent = null,
        int? year = null)
    {
        var loans = FilterByAgentAndYear(svc, agent, year).Where(t => t.LoanAmount.HasValue).Select(t => t.LoanAmount!.Value);
        return loans.Any() ? loans.Max().ToString("F2") : "N/A";
    }

    [McpServerTool]
    [Description("Lowest loan amount (overall, by agent or by year)")]
    [HttpGet("/loans/min")]
    public string GetLowestLoanAmount(
        [Description("What is the lowest loan amount")] LoanTransactionService svc,
        string? agent = null,
        int? year = null)
    {
        var loans = FilterByAgentAndYear(svc, agent, year).Where(t => t.LoanAmount.HasValue).Select(t => t.LoanAmount!.Value);
        return loans.Any() ? loans.Min().ToString("F2") : "N/A";
    }

    // CREDIT SCORE STATISTICS

    [McpServerTool]
    [Description("Average credit score (overall, by agent or by year)")]
    [HttpGet("/credit-score/average")]
    public string GetAverageCreditScore(
        [Description("What is the average credit score")] LoanTransactionService svc,
        string? agent = null,
        int? year = null)
    {
        var loans = FilterByAgentAndYear(svc, agent, year).Where(t => t.CreditScore.HasValue).Select(t => t.CreditScore!.Value);
        return loans.Any() ? loans.Average().ToString("F2") : "N/A";
    }

    [McpServerTool]
    [Description("Highest credit score (overall, by agent or by year)")]
    [HttpGet("/credit-score/max")]
    public string GetHighestCreditScore(
        [Description("What is the highest credit score")] LoanTransactionService svc,
        string? agent = null,
        int? year = null)
    {
        var loans = FilterByAgentAndYear(svc, agent, year).Where(t => t.CreditScore.HasValue).Select(t => t.CreditScore!.Value);
        return loans.Any() ? loans.Max().ToString("F2") : "N/A";
    }

    [McpServerTool]
    [Description("Lowest credit score (overall, by agent or by year)")]
    [HttpGet("/credit-score/min")]
    public string GetLowestCreditScore(
        [Description("What is the lowest credit score")] LoanTransactionService svc,
        string? agent = null,
        int? year = null)
    {
        var loans = FilterByAgentAndYear(svc, agent, year).Where(t => t.CreditScore.HasValue).Select(t => t.CreditScore!.Value);
        return loans.Any() ? loans.Min().ToString("F2") : "N/A";
    }

    // ESCROW-RELATED TOOLS

    [McpServerTool]
    [Description("Number of loans for a specific escrow company ")]
    [HttpGet("/loans/total/{escrowCompany}")]
    public string GetLoansByEscrow(
    [Description("What are the loans for a specific escrow company")] LoanTransactionService svc,
        string escrowCompany,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var loans = Filter(svc, new[] { agent! }, year, from, to)
                    .Where(t => t.ActualClosedDate == null);
        return JsonSerializer.Serialize(loans);
    }

    [McpServerTool]
    [Description("Get all Escrow Companies")]
    [HttpGet("/escrow-cpmanies")]
    public string GetAllEscrowCompanies(
        [Description("What are the names of all escrow companies")] LoanTransactionService svc)
        => JsonSerializer.Serialize(svc.GetLoanTransactions().Result
                                    .Select(t => t.EscrowCompany)
                                    .Where(c => !string.IsNullOrEmpty(c))
                                    .Distinct());

    [McpServerTool]
    [Description("Get transactions for a specific Escrow Company")]
    [HttpGet("/loans/{escrowCompany}")]
    public string GetTransactionsByEscrowCompany(
        [Description("list the transactions made by a specific escrow company")] LoanTransactionService svc, string escrowCompany)
        => JsonSerializer.Serialize(svc.GetByEscrowCompany(escrowCompany));

    [McpServerTool]
    [Description("Get top Escrow Companies ranked by number of transactions")]
    [HttpGet("/top-escrow-companies")]
    public string GetTopEscrowCompanies(
        [Description("What are the top escrow companies")] LoanTransactionService svc, int top = 10)
    {
        var result = svc.GetLoanTransactions().Result
                        .Where(t => !string.IsNullOrEmpty(t.EscrowCompany))
                        .GroupBy(t => t.EscrowCompany)
                        .OrderByDescending(g => g.Count())
                        .Take(top)
                        .Select(g => new { EscrowCompany = g.Key, Transactions = g.Count() });

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool]
    [Description("Get Escrow Company statistics (total loans, average, highest, lowest loan amounts)")]
    [HttpGet("/loans/statistics/{escrowCompany}")]
    public string GetEscrowCompanyStats(
        [Description("Get the statistics of loan amounts for a specific escrow company")] LoanTransactionService svc, string escrowCompany)
    {
        var loans = svc.GetByEscrowCompany(escrowCompany).Where(t => t.LoanAmount.HasValue).ToList();
        if (!loans.Any())
            return JsonSerializer.Serialize(new { TotalLoans = 0, AverageLoanAmount = 0, HighestLoanAmount = 0, LowestLoanAmount = 0 });

        var amounts = loans.Select(t => t.LoanAmount!.Value);
        return JsonSerializer.Serialize(new
        {
            TotalLoans = loans.Count,
            AverageLoanAmount = amounts.Average(),
            HighestLoanAmount = amounts.Max(),
            LowestLoanAmount = amounts.Min()
        });
    }

    // OTHER TOOLS

    [McpServerTool]
    [Description("Get total number of transactions for a lender")]
    [HttpGet("/loans/total/{lender}")]
    public string GetTotalTransactionsByLender(
        [Description("What are the number of transactions made by this lender")] LoanTransactionService svc,
        string lender,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var count = Filter(svc, new[] { agent! }, year, from, to)
                    .Count(t => t.LenderName != null && t.LenderName.Equals(lender, StringComparison.OrdinalIgnoreCase));
        return count.ToString();
    }

    [McpServerTool]
    [Description("Get all Title Companies")]
    [HttpGet("/title-companies")]
    public string GetAllTitleCompanies(
        [Description("give a list of all title companies")] LoanTransactionService svc)
        => JsonSerializer.Serialize(svc.GetAllTitleCompanies());

    [McpServerTool]
    [Description("Get transactions of a specific Title Company")]
    [HttpGet("/loans/{titleCompany}")]
    public string GetTransactionsByTitleCompany(
        [Description("List all transactions made by this title company")] LoanTransactionService svc,
        string titleCompany,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = Filter(svc, new[] { agent! }, year, from, to)
                   .Where(t => t.TitleCompany != null && t.TitleCompany.Equals(titleCompany, StringComparison.OrdinalIgnoreCase));
        return JsonSerializer.Serialize(data);
    }

    [McpServerTool]
    [Description("Get 1099 for an agent for a specific year")]
    [HttpGet("/1099/{agent}/{year}")]
    public string GetAgent1099(
        [Description("What is the 1099 for this agent for a specific year")] LoanTransactionService svc,
        string agent,
        int year)
        => svc.GetAgent1099(agent, year).ToString("F2");

    [McpServerTool]
    [Description("Get lender statistics (total loans, average, highest, lowest loan amounts)")]
    [HttpGet("/loans/statistics/{lender}")]
    public string GetLenderStats(
        [Description("What are the loan statistics of this lender")] LoanTransactionService svc,
        string lender)
    {
        var stats = svc.GetLenderStats(lender);
        return JsonSerializer.Serialize(new
        {
            TotalLoans = stats.totalLoans,
            AverageLoanAmount = stats.avgAmount,
            HighestLoanAmount = stats.maxAmount,
            LowestLoanAmount = stats.minAmount
        });
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

    private static IEnumerable<LoanTransaction> FilterByAgentAndYear(
    LoanTransactionService svc,
    string? agent = null,
    int? year = null)
    {
        var data = svc.GetLoanTransactions().Result.AsEnumerable();
        if (!string.IsNullOrEmpty(agent))
            data = data.Where(t => t.AgentName != null && t.AgentName.Equals(agent, StringComparison.OrdinalIgnoreCase));

        if (year.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value.Year == year.Value);

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
