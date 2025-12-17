using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Models;
using TestMcpApi.Services;

// Mark the class as a collection of MCP tools
[McpServerToolType]
[ApiController] // Use ApiController attributes if integrating into an existing Web API
public class LoansController : ControllerBase
{
    private readonly ILoanTransactionService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;

    public LoansController(ILoanTransactionService loanTransactionService, IConfiguration configuration)
    {
        svc = loanTransactionService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
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
        [Description("What is the LTV for this specific loan?")] string loanId)
    {
        string ltv = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            ltv = "not available right now";
        }
        else
        {
            var value = svc.GetLTV(loanId);
            ltv = value.HasValue ? value.Value.ToString("F2") : "Not found";
        }

        return $"The LTV for loan #{loanId} is {ltv}";
    }


    [McpServerTool]
    [Description("Get the IDs of loans with a specific status (Active = Submitted / Not Submitted)")]
    [HttpGet("/loans/status/{status}")]
    public string GetLoanIdsByStatus(
    [Description("What are the loan IDs with this status?")] string status,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var loans = Filter(svc, null, year, from, to)
                        .Where(t => t.Active != null && t.Active.Equals(status, StringComparison.OrdinalIgnoreCase))
                        .Select(t => t.LoanTransID)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList();

            if (!loans.Any())
                result = "No loans found with the specified status";
            else
                result = loans.Aggregate((a, b) => a + ", " + b)!;
        }

        return $"The loan IDs with status '{status}' are: {result}";
    }


    [McpServerTool]
    [Description("Get loans that haven't been closed yet")]
    [HttpGet("/loans/open")]
    public string GetOpenLoans(
        [Description("Which loans are still open and haven't been closed yet?")] int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var loans = Filter(svc, null, year, from, to)
                        .Where(t => t.ActualClosedDate == null)
                        .Select(t => new { ID = t.LoanTransID, Agent = t.AgentName, LoanAmount = t.LoanAmount, LoanType = t.LoanType })
                        .ToList();

            if (!loans.Any())
                result = "No open loans found";
            else
            {
                result = loans.Select(l =>
                    $"Loan #{l.ID}, Agent: {l.Agent}, Loan Amount: {l.LoanAmount}, Loan Type: {l.LoanType}")
                    .Aggregate((a, b) => a + ", " + b);
            }
        }

        return $"The open loans are: {result}";
    }



    //POPULARITY TOOLS

    [McpServerTool]
    [Description("Get the most popular ZIP code or get the top zip codes for properties being sold or bought")]
    [HttpGet("/loans/zips")]
    public string GetMostPopularZip(
        [Description("Which ZIP code appears most frequently in the loans or what are the top zip codes for properties being sold or bought?")] int top = 1, 
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var zip = GetMostPopularValueFiltered(svc, t => t.SubjectPostalCode, string.IsNullOrEmpty(agent) ? null : new[] { agent }, year, from, to);
            result = string.IsNullOrEmpty(zip) ? "N/A" : zip;
        }

        return $"The most popular ZIP code is: {result}";
    }


    [McpServerTool]
    [Description("Get top cities ranked by number of transactions")]
    [HttpGet("/top-cities")]
    public string GetTopCities(
        [Description("Which cities have the highest number of transactions?")] int top = 10,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string names = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            names = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.SubjectCity)
                             .OrderByDescending(g => g.Count())
                             .Take(top)
                             .Select(g => new { City = g.Key, Transactions = g.Count() });

            List<TopCityResult> results = JsonSerializer.Deserialize<List<TopCityResult>>(JsonSerializer.Serialize(result))!;

            names = results.Select(r => r.City + " with " + r.Transactions + " transactions")
                           .Aggregate((a, b) => a + ", " + b);
        }

        return $"The {top} cities with the highest number of transactions are: {names}";
    }


    [McpServerTool]
    [Description("Get most popular property type")]
    [HttpGet("/top-property-type")]
    public string GetMostPopularPropType(
        [Description("What is the most popular property type?")] string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.PropType)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { PropType = g.Key, Transactions = g.Count() });

            List<TopPropertyTypeResult> results = JsonSerializer.Deserialize<List<TopPropertyTypeResult>>(JsonSerializer.Serialize(result))!;

            type = results.Select(r => r.PropType + " with " + r.Transactions + " transactions")
                          .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular property type is: {type}";
    }


    [McpServerTool]
    [Description("Get most popular transaction type")]
    [HttpGet("/top-transaction-type")]
    public string GetMostPopularTransactionType(
        [Description("What is the most popular transaction type?")] string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.TransactionType)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { TransactionType = g.Key, Transactions = g.Count() });

            List<TopTransactionTypeResult> results = JsonSerializer.Deserialize<List<TopTransactionTypeResult>>(JsonSerializer.Serialize(result))!;

            type = results.Select(r => r.TransactionType + " with " + r.Transactions + " transactions")
                          .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular transaction type is: {type}";
    }

    [McpServerTool]
    [Description("Get most popular mortgage type")]
    [HttpGet("/top-mortgage-type")]
    public string GetMostPopularMortgageType(
        [Description("What is the most popular mortgage type?")] string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.MortgageType)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { MortgageType = g.Key, Transactions = g.Count() });

            List<TopMortgageTypeResult> results = JsonSerializer.Deserialize<List<TopMortgageTypeResult>>(JsonSerializer.Serialize(result))!;

            type = results.Select(r => r.MortgageType + " with " + r.Transactions + " transactions")
                          .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular mortgage type is: {type}";
    }

    [McpServerTool]
    [Description("Get most popular brokering type")]
    [HttpGet("/top-brokering-type")]
    public string GetMostPopularBrokeringType(
        [Description("What is the most popular brokering type?")] string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.BrokeringType)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { BrokeringType = g.Key, Transactions = g.Count() });

            List<TopBrokeringTypeResult> results = JsonSerializer.Deserialize<List<TopBrokeringTypeResult>>(JsonSerializer.Serialize(result))!;

            type = results.Select(r => r.BrokeringType + " with " + r.Transactions + " transactions")
                          .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular brokering type is: {type}";
    }

    [McpServerTool]
    [Description("Get most popular loan type")]
    [HttpGet("/top-loan-type")]
    public string GetMostPopularLoanType(
        [Description("What is the most popular loan type?")] string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.LoanType)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { LoanType = g.Key, Transactions = g.Count() });

            List<TopLoanTypeResult> results = JsonSerializer.Deserialize<List<TopLoanTypeResult>>(JsonSerializer.Serialize(result))!;
            type = results.Select(r => r.LoanType + " with " + r.Transactions + " transactions")
                          .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular loan type is: {type}";
    }


    [McpServerTool]
    [Description("Get most popular escrow method send type")]
    [HttpGet("/top-escrow-send-type")]
    public string GetMostPopularEscrowMethod(
    [Description("What is the most popular escrow method send type?")] string? agent = null,
    int? year = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        string method = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            method = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.EscrowMethodSendType)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { EscrowMethod = g.Key, Transactions = g.Count() });

            List<TopEscrowMethodResult> results = JsonSerializer.Deserialize<List<TopEscrowMethodResult>>(JsonSerializer.Serialize(result))!;
            method = results.Select(r => r.EscrowMethod + " with " + r.Transactions + " transactions")
                            .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular escrow method send type is: {method}";
    }


    [McpServerTool]
    [Description("Get most popular title company")]
    [HttpGet("/top-title-company")]
    public string GetMostPopularTitleCompany(
        [Description("What is the most popular title company?")] string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string company = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            company = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.TitleCompany)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { TitleCompany = g.Key, Transactions = g.Count() });

            List<TopTitleCompanyResult> results = JsonSerializer.Deserialize<List<TopTitleCompanyResult>>(JsonSerializer.Serialize(result))!;
            company = results.Select(r => r.TitleCompany + " with " + r.Transactions + " transactions")
                             .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular title company is: {company}";
    }


    [McpServerTool]
    [Description("Get most popular escrow company")]
    [HttpGet("/top-escrow-company")]
    public string GetMostPopularEscrowCompany(
        [Description("What is the most popular escrow company?")] string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string company = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            company = "not available right now";
        }
        else
        {
            var agentsArray = string.IsNullOrEmpty(agent) ? null : new[] { agent };
            var data = Filter(svc, agentsArray, year, from, to);

            var result = data.GroupBy(t => t.EscrowCompany)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { EscrowCompany = g.Key, Transactions = g.Count() });

            List<TopEscrowCompanyResult> results = JsonSerializer.Deserialize<List<TopEscrowCompanyResult>>(JsonSerializer.Serialize(result))!;
            company = results.Select(r => r.EscrowCompany + " with " + r.Transactions + " transactions")
                             .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular escrow company is: {company}";
    }


    // LOAN AMOUNT STATISTICS

    [McpServerTool]
    [Description("Average loan amount (overall, by agent or by year)")]
    [HttpGet("/loans/average")]
    public string GetAverageLoanAmount(
        [Description("What is the average loan amount?")]
        string? agent = null,
        int? year = null)
    {
        string result;

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var loans = FilterByAgentAndYear(svc, agent, year)
                        .Where(t => t.LoanAmount.HasValue)
                        .Select(t => t.LoanAmount!.Value);

            result = loans.Any() ? loans.Average().ToString("F2") : "N/A";
        }

        return $"The average loan amount is: {result}";
    }

    [McpServerTool]
    [Description("Highest loan amount (overall, by agent or by year)")]
    [HttpGet("/loans/max")]
    public string GetHighestLoanAmount(
        [Description("What is the highest loan amount?")]
        string? agent = null,
        int? year = null)
    {
        string result;

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var loans = FilterByAgentAndYear(svc, agent, year)
                        .Where(t => t.LoanAmount.HasValue)
                        .Select(t => t.LoanAmount!.Value);

            result = loans.Any() ? loans.Max().ToString("F2") : "N/A";
        }

        return $"The highest loan amount is: {result}";
    }

    [McpServerTool]
    [Description("Lowest loan amount (overall, by agent or by year)")]
    [HttpGet("/loans/min")]
    public string GetLowestLoanAmount(
        [Description("What is the lowest loan amount?")]
        string? agent = null,
        int? year = null)
    {
        string result;

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var loans = FilterByAgentAndYear(svc, agent, year)
                        .Where(t => t.LoanAmount.HasValue)
                        .Select(t => t.LoanAmount!.Value);

            result = loans.Any() ? loans.Min().ToString("F2") : "N/A";
        }

        return $"The lowest loan amount is: {result}";
    }


    // CREDIT SCORE STATISTICS

    [McpServerTool]
    [Description("Get average credit score (overall, by agent or by year)")]
    [HttpGet("/credit-score/average")]
    public string GetAverageCreditScore(
        [Description("What is the average credit score for the agent?")] string? agent = null,
        int? year = null)
    {
        string result = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var data = FilterByAgentAndYear(svc, agent, year)
                       .Where(t => t.CreditScore.HasValue)
                       .Select(t => t.CreditScore!.Value);

            result = data.Any() ? data.Average().ToString("F2") : "N/A";
        }

        return $"The average credit score is: {result}";
    }

    [McpServerTool]
    [Description("Get highest credit score (overall, by agent or by year)")]
    [HttpGet("/credit-score/max")]
    public string GetHighestCreditScore(
        [Description("What is the highest credit score for the agent?")] string? agent = null,
        int? year = null)
    {
        string result = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var data = FilterByAgentAndYear(svc, agent, year)
                       .Where(t => t.CreditScore.HasValue)
                       .Select(t => t.CreditScore!.Value);

            result = data.Any() ? data.Max().ToString("F2") : "N/A";
        }

        return $"The highest credit score is: {result}";
    }

    [McpServerTool]
    [Description("Get lowest credit score (overall, by agent or by year)")]
    [HttpGet("/credit-score/min")]
    public string GetLowestCreditScore(
        [Description("What is the lowest credit score for the agent?")] string? agent = null,
        int? year = null)
    {
        string result = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var data = FilterByAgentAndYear(svc, agent, year)
                       .Where(t => t.CreditScore.HasValue)
                       .Select(t => t.CreditScore!.Value);

            result = data.Any() ? data.Min().ToString("F2") : "N/A";
        }

        return $"The lowest credit score is: {result}";
    }



    // ESCROW-RELATED TOOLS

    [McpServerTool]
    [Description("Get number of loans for a specific escrow company")]
    [HttpGet("/loans/total/{escrowCompany}")]
    public string GetLoansByEscrow(
        [Description("What are the loans for a specific escrow company?")] string escrowCompany,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string result = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var data = Filter(svc, new[] { agent! }, year, from, to)
                       .Where(t => t.EscrowCompany != null && t.EscrowCompany.Equals(escrowCompany, StringComparison.OrdinalIgnoreCase));

            result = data.Any() ? data.Count().ToString() : "0";
        }

        return $"The number of loans for {escrowCompany} is: {result}";
    }


    [McpServerTool]
    [Description("Get all escrow companies")]
    [HttpGet("/escrow-companies")]
    public string GetAllEscrowCompanies(
        [Description("What are the names of all escrow companies")] string dummy = "")
    {
        string result = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var companies = svc.GetLoanTransactions().Result
                              .Select(t => t.EscrowCompany)
                              .Where(c => !string.IsNullOrEmpty(c))
                              .Distinct()
                              .ToList();

            result = companies.Any() ? string.Join(", ", companies) : "none found";
        }

        return $"The escrow companies are: {result}";
    }


    [McpServerTool]
    [Description("Get transactions for a specific escrow company")]
    [HttpGet("/loans/{escrowCompany}")]
    public string GetTransactionsByEscrowCompany(
        [Description("List the transactions made by a specific escrow company")] string escrowCompany)
    {
        string result = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var transactions = svc.GetByEscrowCompany(escrowCompany)
                                  .Select(t => new EscrowTransactionDto
                                  {
                                      LoanTransID = t.LoanTransID,
                                      AgentName = t.AgentName,
                                      LoanAmount = t.LoanAmount,
                                      SubjectCity = t.SubjectCity,
                                      SubjectState = t.SubjectState
                                  }).ToList();

            result = transactions.Any()
                ? JsonSerializer.Serialize(transactions)
                : "no transactions found";
        }

        return $"The transactions for {escrowCompany} are: {result}";
    }
    [McpServerTool]
    [Description("Get top Escrow Companies ranked by number of transactions")]
    [HttpGet("/top-escrow-companies")]
    public string GetTopEscrowCompanies(
        [Description("What are the top escrow companies")] int top = 10)
    {
        string names = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            names = "not available right now";
        }
        else
        {
            var data = svc.GetLoanTransactions().Result
                          .Where(t => !string.IsNullOrEmpty(t.EscrowCompany));

            var result = data.GroupBy(t => t.EscrowCompany)
                             .OrderByDescending(g => g.Count())
                             .Take(top)
                             .Select(g => new { EscrowCompany = g.Key, Transactions = g.Count() });

            List<TopEscrowCompanyResult> results = JsonSerializer.Deserialize<List<TopEscrowCompanyResult>>(JsonSerializer.Serialize(result))!;

            names = results.Select(r => r.EscrowCompany + " with " + r.Transactions + " transactions")
                           .Aggregate((a, b) => a + ", " + b);
        }

        return $"The top {top} escrow companies are: {names}";
    }

    [McpServerTool]
    [Description("Get Escrow Company statistics (total loans, average, highest, lowest loan amounts)")]
    [HttpGet("/loans/statistics/{escrowCompany}")]
    public string GetEscrowCompanyStats(
        [Description("What are the total loans and loan amount statistics for a specific escrow company?")] string escrowCompany)
    {
        string resultText = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            resultText = "not available right now";
        }
        else
        {
            var loans = svc.GetByEscrowCompany(escrowCompany)
                           .Where(t => t.LoanAmount.HasValue)
                           .ToList();

            if (!loans.Any())
            {
                resultText = $"The escrow company {escrowCompany} has no loans.";
            }
            else
            {
                var amounts = loans.Select(t => t.LoanAmount!.Value);
                EscrowCompanyStatsResult stats = new EscrowCompanyStatsResult
                {
                    TotalLoans = loans.Count,
                    AverageLoanAmount = amounts.Average(),
                    HighestLoanAmount = amounts.Max(),
                    LowestLoanAmount = amounts.Min()
                };

                resultText = $"The escrow company {escrowCompany} has {stats.TotalLoans} loans with an average loan amount of {stats.AverageLoanAmount:F2}, " +
                             $"highest loan amount of {stats.HighestLoanAmount:F2}, and lowest loan amount of {stats.LowestLoanAmount:F2}.";
            }
        }

        return resultText;
    }

    // OTHER TOOLS

    [McpServerTool]
    [Description("Get total number of transactions for a lender")]
    [HttpGet("/loans/total/{lender}")]
    public string GetTotalTransactionsByLender(
        [Description("How many transactions did this lender make?")] string lender,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string resultText = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            resultText = "not available right now";
        }
        else
        {
            var count = Filter(svc, new[] { agent! }, year, from, to)
                        .Count(t => t.LenderName != null && t.LenderName.Equals(lender, StringComparison.OrdinalIgnoreCase));

            resultText = $"The total number of transactions for lender {lender} is: {count}";
        }

        return resultText;
    }


    [McpServerTool]
    [Description("What are the names of all title companies?")]
    [HttpGet("/title-companies")]
    public string GetAllTitleCompanies()
    {
        string resultText = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            resultText = "not available right now";
        }
        else
        {
            var companies = svc.GetAllTitleCompanies();
            if (companies == null || !companies.Any())
            {
                resultText = "No title companies found";
            }
            else
            {
                resultText = "The names of all title companies are: " + string.Join(", ", companies);
            }
        }

        return resultText;
    }


    [McpServerTool]
    [Description("Get transactions for a specific title company")]
    [HttpGet("/loans/{titleCompany}")]
    public string GetTransactionsByTitleCompany(
        [Description("Which transactions were made by this title company?")] string titleCompany,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        string resultText = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            resultText = "not available right now";
        }
        else
        {
            var data = Filter(svc, new[] { agent! }, year, from, to)
                       .Where(t => t.TitleCompany != null && t.TitleCompany.Equals(titleCompany, StringComparison.OrdinalIgnoreCase))
                       .Select(t => new TransactionDto
                       {
                           LoanTransID = t.LoanTransID,
                           AgentName = t.AgentName,
                           LoanAmount = t.LoanAmount,
                           LoanDate = t.DateAdded
                       })
                       .ToList();

            if (!data.Any())
            {
                resultText = $"No transactions found for the title company {titleCompany}";
            }
            else
            {
                var serialized = JsonSerializer.Serialize(data);
                resultText = $"The transactions for the title company {titleCompany} are: {serialized}";
            }
        }

        return resultText;
    }

    [McpServerTool]
    [Description("Get 1099 for an agent for a specific year")]
    [HttpGet("/1099/{agent}/{year}")]
    public string GetAgent1099(
        [Description("What is the 1099 for this agent for a specific year?")] string agent,
        int year)
    {
        string resultText = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            resultText = "not available right now";
        }
        else
        {
            var amount = svc.GetAgent1099(agent, year);
            resultText = $"The 1099 for {agent} for the year {year} is: {amount:F2}";
        }

        return resultText;
    }


    [McpServerTool]
    [Description("Get lender statistics (total loans, average, highest, lowest loan amounts)")]
    [HttpGet("/loans/statistics/{lender}")]
    public string GetLenderStats(
        [Description("What are the total loans and loan amount statistics for this lender?")] string lender)
    {
        string resultText = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            resultText = "not available right now";
        }
        else
        {
            var statsData = svc.GetLenderStats(lender);

            if (statsData.totalLoans == 0)
            {
                resultText = $"The lender {lender} has no loans.";
            }
            else
            {
                LenderStatsResult stats = new LenderStatsResult
                {
                    TotalLoans = statsData.totalLoans,
                    AverageLoanAmount = statsData.avgAmount,
                    HighestLoanAmount = statsData.maxAmount,
                    LowestLoanAmount = statsData.minAmount
                };

                resultText = $"The lender {lender} has {stats.TotalLoans} loans with an average loan amount of {stats.AverageLoanAmount:F2}, " +
                             $"highest loan amount of {stats.HighestLoanAmount:F2}, and lowest loan amount of {stats.LowestLoanAmount:F2}.";
            }
        }

        return resultText;
    }





    //HELPERS
    private static IEnumerable<LoanTransaction> Filter(
        ILoanTransactionService svc,
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
    ILoanTransactionService svc,
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
        ILoanTransactionService svc,
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
