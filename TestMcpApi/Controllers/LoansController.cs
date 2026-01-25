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
public class LoansController : ControllerBase
{
    private readonly ILoanTransactionService svc;
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LoansController(ILoanTransactionService loanTransactionService, IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        svc = loanTransactionService;
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        _httpContextAccessor = httpContextAccessor;
    }

    [McpServerTool, Description("Retrieves the current call ID")]
    [HttpGet("/loans/call_id")]
    public async Task<string> GetCallContext(string query)
    {
        // Access the current HttpContext
        var context = _httpContextAccessor.HttpContext;

        // Extract the Call ID sent by Vapi
        if (context != null && context.Request.Headers.TryGetValue("X-Call-Id", out var callId))
        {
            // Use the callId for logging or database lookups
            //Console.WriteLine($"Processing tool for Call ID: {callId}");

            //bool AddToVapiCalls = new UserService().AddCallToVapiCallsAsync(call: new VapiCall
            //{
            //    CallId = callId,
            //    UserId = 999,
            //    UserRole = "Senior Agent",
            //    Phone = "8583449999",
            //    CreatedOn = DateTime.UtcNow,
            //    LastUpdatedOn = DateTime.UtcNow,
            //    IsAuthenticated = 0
            //}).Result;

            return $"Success: Data for call {callId} retrieved.";
        }

        return "Call ID not found in request headers.";
    }


    [McpServerTool]
    [Description("What's exact number of transactions for agent?")]
    [HttpGet("/loans/agent-no-transactions")]
    public string GetNumTransactionsForAgent(
    [Description("the agent name for field AgentName")] string agent_name = "unknown",
    [Description("user_id")] int user_id = 0,
    [Description("user_role")] string user_role = "unknown",
    [Description("token")] string token = "unknown")
    {
        //Step 1: Get the data First
        // Proceed with the tool execution for Admin users
        var data = svc.GetLoanTransactions().Result.AsEnumerable();
        var agentCounts = data
            .Where(lt => lt.AgentName != null)
            .GroupBy(lt => lt.AgentName)
            .Select(g => new
            {
                AgentName = g.Key,
                AgentID = g.First().AgentID,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        if (agentCounts.Count() == 0)
            return "There are no agent transactions available for the selected filters.";

        //Try to find the name based on input
        var result = agentCounts
            .OrderBy(x => Common.CalculateLevenshteinDistance(agent_name, x.AgentName))
            .ThenByDescending(x => x.Count) // Optional: Tie-breaker using the highest count
            .FirstOrDefault();

        if (result == null)
        {
            var searchCode = Common.GetSoundex(agent_name); // Implementation from previous example

            result = agentCounts
                .Where(x => Common.GetSoundex(x.AgentName) == searchCode) // Filter for identical sounds
                .OrderByDescending(x => Common.CalculateSoundexDifference(searchCode, Common.GetSoundex(x.AgentName)))
                .FirstOrDefault();
        }

        if (result == null)
        {
            var doubleMetaphone = new DoubleMetaphone();
            string searchKey = doubleMetaphone.BuildKey(agent_name);

            // 2. Perform the search
            result = agentCounts
                .OrderBy(x =>
                {
                    // Generate the phonetic key for each item in the list
                    string itemKey = doubleMetaphone.BuildKey(x.AgentName);

                    // Calculate distance between the phonetic keys
                    // (Closer phonetic keys = smaller distance)
                    return Common.CalculateLevenshteinDistance(searchKey, itemKey);
                })
                .FirstOrDefault();
        }

        if (result == null)
            return "I could not find an agent with this name.";

        // Check if call coming form Web/Mobile app then you should have all three parameters with correct values because user logged in
        if (user_id != 0 && user_role != "unknown" && token != "unknown")
        {
            if (!user_role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return "Access denied. Only users with Admin role can access this information.";
            }
        }
        else
        {
            //Coming here from a live call to VAPI phone number
            //Get the call details from HTTP Headers Context
            var context = _httpContextAccessor.HttpContext;

            //// Extract the Call ID sent by Vapi
            if (context != null && context.Request.Headers.TryGetValue("X-Call-Id", out var callId))
            {
                //Coming here from a live call to VAPI phone number
                VapiCall vapiCall = new UserService().GetCurrentVapiCallAsync(CallId: callId).Result;
                if (vapiCall != null)
                {
                    if (vapiCall.IsAuthenticated == 0)
                    {
                        return "Access denied. You are not authenticated yet!";
                    }

                    if (vapiCall.UserId != int.Parse(result.AgentID.ToString()) && vapiCall.UserRole.ToLower().Trim() != "admin")
                    {
                        return "Access denied. you do not have permissions to lookup transactions for another Agent!";
                    }
                }
                else
                {
                    return "Access denied. Call details not found!";
                }
            }
            else
            {
                return "Access denied. Call ID not found in request headers!";
            }
        }

        return $"{result.AgentName} has {result.Count} transactions";
    }

    [McpServerTool]
    [Description("Get top agents ranked by number of transactions")]
    [HttpGet("/loans/top-agents")]
    public string GetTopAgents(
        [Description("who are the top agents for KAM")] int top = 5,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        // Check if service has errors
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "not available right now";
        }

        // Proceed with the tool execution for Admin users
        var data = Filter(svc, null, year, from, to).Where(t => !string.IsNullOrWhiteSpace(t.AgentName));

        var result = data.GroupBy(t => t.AgentName, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(g => g.Count())
                        .Take(top)
                        .Select(g => new { Agent = g.Key, Transactions = g.Count() });

        if (result.Count() == 0)
            return "There are no agent transactions available for the selected filters.";

        List<TopAgentResult> results = JsonSerializer.Deserialize<List<TopAgentResult>>(JsonSerializer.Serialize(result))!;

        string names = results.Select(r => r.Agent + " with " + r.Transactions + " transactions").Aggregate((a, b) => a + ", " + b);
        return $"The top {top} agents for KAM are: {names}";
    }

    [McpServerTool]
    [Description("List transactions by agent name")]
    [HttpGet("/loans/agent/{agent}")]
    public string GetTransactionsByAgent(
        [Description("List the transactions made by the agent, during the year")]
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
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        string transactions = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            transactions = "not availabale right now";
        }
        var data = Filter(svc, agent, year, from, to)
            .Where(t => !string.IsNullOrWhiteSpace(t.LoanTransID))
            .Where(t => t.LoanAmount.HasValue)
            .Take(top)
            .Select(g => new { ID = g.LoanTransID, LoanAmount = g.LoanAmount, LoanType = g.LoanType, LoanTerm = g.LoanTerm });

        if (data == null || data.Count() == 0)
            return $"No transactions found for agent {agent} using the selected filters.";

        List<TransactionsResult> results = JsonSerializer.Deserialize<List<TransactionsResult>>(JsonSerializer.Serialize(data))!;

        transactions = results.Select(r => "Loan #" + r.ID + ", Loan Amount: " + r.LoanAmount + ", Loan Type: " + r.LoanType + ", Loan Term: " + r.LoanTerm)
            .Aggregate((a, b) => a + ", " + b);
        return $"The transactions made by {agent}, during the year {year} are: {transactions}";
    }

    
    [McpServerTool]
    [Description("Get Agent responsible for a specific property address")]
    [HttpGet("/loans/agent-by-address/{address}")]
    public string GetAgentByAddress(
        [Description("Who is the agent responsible for this property address?")]
        string address,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "The data is not available right now.";

        var loan = svc.GetLoanTransactions().Result
                      .FirstOrDefault(t =>
                          !string.IsNullOrEmpty(t.SubjectAddress) &&
                          t.SubjectAddress.Equals(address, StringComparison.OrdinalIgnoreCase));

        var agent = loan?.AgentName ?? "Not found";

        return $"The agent responsible for the property at '{address}' is: {agent}";
    }

    //Return open loans not submitted yet: Agent Name, Loan number, loan term, borrower name, property address, city, state, 
    [McpServerTool]
    [Description("Get loans that haven't been closed yet")]
    [HttpGet("/loans/open")]
    public string GetOpenLoans(
        [Description("Which loans are still open and haven't been closed yet?")] int top = 10,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var loans = Filter(svc, null, year, from, to)
                        .Where(t => !string.IsNullOrWhiteSpace(t.LoanTransID) && t.ActualClosedDate == null)
                        .Take(top)
                        .Select(t => new { ID = t.LoanTransID, Agent = t.AgentName, LoanAmount = t.LoanAmount, LoanType = t.LoanType })
                        .ToList();

            if (!loans.Any())
                result = "No open loans found";
            else
            {
                result = loans.Take(top).Select(l =>
                    $"Loan #{l.ID}, Agent: {l.Agent}, Loan Amount: {l.LoanAmount}, Loan Type: {l.LoanType}")
                    .Aggregate((a, b) => a + ", " + b);
            }
        }

        return $"The open loans are: {result}";
    }



    //POPULARITY TOOLS

    [McpServerTool]
    [Description("Get the most popular ZIP code or get the top zip codes for properties being sold or bought")]
    [HttpGet("/loans/top-zips")]
    public string GetMostPopularZip(
        [Description("Which ZIP code appears most frequently in the loans or what are the top zip codes for properties being sold or bought?")] int top = 1,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var zip = GetMostPopularValueFiltered(svc, t => t.SubjectPostalCode, agent, year, from, to);
            result = string.IsNullOrEmpty(zip) ? "N/A" : zip;
        }

        return $"The most popular ZIP code is: {result}";
    }


    //Return city, state
    [McpServerTool]
    [Description("Get top cities ranked by number of transactions")]
    [HttpGet("/loans/top-cities")]
    public string GetTopCities(
        [Description("Which cities have the highest number of transactions?")] int top = 10,
        [Description("Filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        string names = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            names = "not available right now";
        }
        else
        {
            var data = Filter(svc, agent, year, from, to)
                .Where(t => !string.IsNullOrWhiteSpace(t.SubjectCity));

            var result = data.GroupBy(t => t.SubjectCity, StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(g => g.Count())
                             .Take(top)
                             .Select(g => new { City = g.Key, Transactions = g.Count() });

            if (!result.Any() || result.Count() == 0)
            {
                return "Result Not Available";
            }

            List<TopCityResult> results = JsonSerializer.Deserialize<List<TopCityResult>>(JsonSerializer.Serialize(result))!;

            names = results.Select(r => r.City + " with " + r.Transactions + " transactions")
                           .Aggregate((a, b) => a + ", " + b);
        }

        return $"The {top} cities with the highest number of transactions are: {names}";
    }



    //Get the most popular loans: property type, transaction type, mortgage type, loan type
    [McpServerTool]
    [Description("Get most popular property type")]
    [HttpGet("/loans/top-property-type")]
    public string GetMostPopularPropType(
        [Description("What is the most popular property type?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "not available right now";
        }
        else
        {
            var data = Filter(svc, agent, year, from, to)
                .Where(t => !string.IsNullOrWhiteSpace(t.PropType));

            var result = data.GroupBy(t => t.PropType, StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { PropType = g.Key, Transactions = g.Count() });

            if (!result.Any() || result.Count() == 0)
            {
                return "Result Not Available";
            }

            List<TopPropertyTypeResult> results = JsonSerializer.Deserialize<List<TopPropertyTypeResult>>(JsonSerializer.Serialize(result))!;

            type = results.Select(r => r.PropType + " with " + r.Transactions + " transactions")
                          .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular property type is: {type}";
    }


   
    [McpServerTool]
    [Description("Get most popular title company")]
    [HttpGet("/loans/top-title-company")]
    public string GetMostPopularTitleCompany(
        [Description("What is the most popular title company?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        string company = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            company = "not available right now";
        }
        else
        {
            var data = Filter(svc, agent, year, from, to)
                .Where(t => !string.IsNullOrWhiteSpace(t.TitleCompany));

            var result = data.GroupBy(t => t.TitleCompany, StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { TitleCompany = g.Key, Transactions = g.Count() });

            if (!result.Any() || result.Count() == 0)
            {
                return "Result Not Available";
            }

            List<TopTitleCompanyResult> results = JsonSerializer.Deserialize<List<TopTitleCompanyResult>>(JsonSerializer.Serialize(result))!;
            company = results.Select(r => r.TitleCompany + " with " + r.Transactions + " transactions")
                             .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular title company is: {company}";
    }


    [McpServerTool]
    [Description("Get most popular escrow company")]
    [HttpGet("/loans/top-escrow-company")]
    public string GetMostPopularEscrowCompany(
        [Description("What is the most popular escrow company?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("Filter transactions from this date")] DateTime? from = null,
        [Description("Filter transactions to this date")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        string company = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            company = "not available right now";
        }
        else
        {
            var data = Filter(svc, agent, year, from, to)
                .Where(t => !string.IsNullOrWhiteSpace(t.EscrowCompany));

            var result = data.GroupBy(t => t.EscrowCompany, StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { EscrowCompany = g.Key, Transactions = g.Count() });

            if (!result.Any() || result.Count() == 0)
            {
                return "Result Not Available";
            }

            List<TopEscrowCompanyResult> results = JsonSerializer.Deserialize<List<TopEscrowCompanyResult>>(JsonSerializer.Serialize(result))!;
            company = results.Select(r => r.EscrowCompany + " with " + r.Transactions + " transactions")
                             .Aggregate((a, b) => a + ", " + b);
        }

        return $"The most popular escrow company is: {company}";
    }


    // replace all of them with Give me the statiscs of my loans

    [McpServerTool]
    [Description("Average loan amount (overall, by agent or by year)")]
    [HttpGet("/loans/average")]
    public string GetAverageLoanAmount(
        [Description("What is the average loan amount?")]
        string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

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
        [Description("Filter by specific year")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

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
        [Description("Filter by specific year")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

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
    [HttpGet("/loans/credit-score/average")]
    public string GetAverageCreditScore(
        [Description("What is the average credit score for the agent?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

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
    [HttpGet("/loans/credit-score/max")]
    public string GetHighestCreditScore(
        [Description("What is the highest credit score for the agent?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

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
    [HttpGet("/loans/credit-score/min")]
    public string GetLowestCreditScore(
        [Description("What is the lowest credit score for the agent?")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check agent-specific authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

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



    //Similar to get last transactions for agent
    [McpServerTool]
    [Description("Get transactions for a specific escrow company")]
    [HttpGet("/loans/ecrowCompany/{escrowCompany}")]
    public string GetTransactionsByEscrowCompany(
        [Description("List the transactions made by a specific escrow company")]
        string escrowCompany,
        [Description("Maximum number of transactions to return")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        string result = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "not available right now";
        }
        else
        {
            var transactions = svc.GetByEscrowCompany(escrowCompany)
                 .Where(t => !string.IsNullOrEmpty(t.LoanTransID))
                                  .Take(top)
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
    [HttpGet("/loans/top-escrow-companies")]
    public string GetTopEscrowCompanies(
        [Description("What are the top escrow companies")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

        string names = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            names = "not available right now";
        }
        else
        {
            var data = svc.GetLoanTransactions().Result
                          .Where(t => !string.IsNullOrEmpty(t.EscrowCompany));

            var result = data.GroupBy(t => t.EscrowCompany, StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(g => g.Count())
                             .Take(top)
                             .Select(g => new { EscrowCompany = g.Key, Transactions = g.Count() });

            List<TopEscrowCompanyResult> results = JsonSerializer.Deserialize<List<TopEscrowCompanyResult>>(JsonSerializer.Serialize(result))!;

            names = results.Select(r => r.EscrowCompany + " with " + r.Transactions + " transactions")
                           .Aggregate((a, b) => a + ", " + b);
        }

        return $"The top {top} escrow companies are: {names}";
    }

  
    //Include top tile companies, number of transaction
    [McpServerTool]
    [Description("What are the names of all title companies?")]
    [HttpGet("/loans/title-companies")]
    public string GetAllTitleCompanies(
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

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
    [Description("Get 1099 for an agent for a specific year")]
    [HttpGet("/loans/1099/{agent}/{year}")]
    public string GetAgent1099(
        [Description("What is the 1099 for this agent for a specific year?")] string agent,
        [Description("Year to get 1099")] int year,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

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
    [HttpGet("/loans/lender-statistics/{lender}")]
    public string GetLenderStats(
        [Description("What are the total loans and loan amount statistics for this lender?")] string lender,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
    {
        // Check authorization
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
        if (authError != null)
            return authError;

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
                    TotalTransactions = statsData.totalLoans,
                    AvgAmount = statsData.avgAmount,
                    MaxAmount = statsData.maxAmount,
                    MinAmount = statsData.minAmount
                };

                resultText = $"The lender {lender} has {stats.TotalTransactions} loans with an average loan amount of {stats.AvgAmount:F2}, " +
                             $"highest loan amount of {stats.MaxAmount:F2}, and lowest loan amount of {stats.MinAmount:F2}.";
            }
        }

        return resultText;
    }





    //HELPERS
    private static IEnumerable<LoanTransaction> Filter(
        ILoanTransactionService svc,
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = svc.GetLoanTransactions().Result.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(agent))
        {
            string normAgent = TestMcpApi.Helpers.Common.Normalize(agent);

            data = data.Where(t =>
                t.AgentName != null &&
                TestMcpApi.Helpers.Common.Normalize(t.AgentName).Contains(normAgent, StringComparison.OrdinalIgnoreCase));
        }


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
        string? agent = null,
        int? year = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var data = Filter(svc, agent, year, from, to)
                   .Where(t => !string.IsNullOrEmpty(selector(t)))
                   .Where(t => selector(t) != "NULL");

        var key = data.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault()?.Key ?? "N/A";

        return key;
    }
}
