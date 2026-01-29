using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Phonix;
using System;
using System.ComponentModel;
using System.Text.Json;
using TestMcpApi.Helpers;
using TestMcpApi.Models;
using TestMcpApi.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

    [McpServerTool, Description("Retrieves the current call ID from the request headers for tracking and logging purposes. " +
        "Allows identifying the specific call session when the tool is invoked through a phone call. " +
        "Use this when debugging or tracking call-specific data. " +
        "Relevant for questions like: what is my call ID, show me the current call information, or what is the session ID.")]
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
    [Description("Retrieves the total count of transactions for a specific agent by agent name. " +
        "Allows finding how many deals, sales, or loan transactions an agent has handled. " +
        "Supports fuzzy name matching and phonetic search for agent identification. " +
        "Use this when the user asks how many deals, transactions, loans, or closings an agent has." +
        "Relevant for questions like: how many deals, number of transactions, total loans closed,how active an agent is, or how many closings an agent has.")]
    [HttpGet("/loans/agent-no-transactions")]
    public string GetNumTransactionsForAgent(
        [Description("Name of the agent to search for (supports fuzzy and phonetic matching)")] string agent_name = "unknown",
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
    [Description("Retrieves contact information (phone number and email) for a specific agent by agent name. " +
        "Allows finding how to reach or communicate with an agent. " +
        "Supports fuzzy name matching and phonetic search for agent identification. " +
        "Use this when the user asks for contact details, phone number, email, or how to reach an agent. " +
        "Relevant for questions like: what's the phone number for, how do I contact, what's the email address for, or how can I reach an agent.")]
    [HttpGet("/loans/agent-contact-info/{agent}")]
    public string AgentContactInfo(
        [Description("Name of the agent whose contact information to retrieve (supports fuzzy and phonetic matching)")] string agent,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        var allUsers = new UserService().GetUsers().Result;

        // Step 2: Match phonetics for agent
        var matchedAgent = Common.MatchPhonetic(allUsers, agent, u => u.Name ?? string.Empty);

        // Step 3: Get user related to phonetic results
        if (matchedAgent != null)
        {
            agent = matchedAgent.Name ?? agent;
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);

            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        var agentUser = allUsers.FirstOrDefault(u =>
            u.Name != null &&
            u.Name.Equals(agent, StringComparison.OrdinalIgnoreCase));

        if (agentUser == null)
        {
            return $"No contact information found for agent {agent}.";
        }

        string phone = string.IsNullOrWhiteSpace(agentUser.Phone) ? "Not available" : Common.FormatPhoneNumber(agentUser.Phone);
        string email = string.IsNullOrWhiteSpace(agentUser.Email) ? "Not available" : agentUser.Email;

        // Step 6: Present data
        return $"Contact information for {agent}: Phone: {phone}, Email: {email}";
    }

    [McpServerTool]
    [Description("Retrieves a ranked list of top-performing agents based on transaction count. " +
        "Use this when the user asks about: top agents, best performers, most active agents, agent rankings, " +
        "best agents for [year], top performers in [year], or who are the top agents. " +
        "Supports optional filtering by year (e.g., 2024, 2025), date range (from/to dates), and top count. " +
        "When year filter is applied, only transactions from that specific year are counted. " +
        "Returns agent names with their transaction counts ranked from highest to lowest. " +
        "Example queries: 'show me the best performers for 2025', 'top 5 agents in 2024', 'who are the most active agents'.")]
    [HttpGet("/loans/top-agents")]
    public string GetTopAgents(
        [Description("Optional filter: Maximum number of top agents to return (default is 5)")] int top = 5,
        [Description("Optional filter: Year to filter transactions by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            
            // Step 2: Match phonetics
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            // Step 3: Get user related to phonetic results
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "not available right now";
        }

        var data = Filter(svc, null, year, from, to).Where(t => !string.IsNullOrWhiteSpace(t.AgentName));

        var result = data.GroupBy(t => t.AgentName, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(g => g.Count())
                        .Take(top)
                        .Select(g => new { Agent = g.Key, Transactions = g.Count() });

        if (result.Count() == 0)
            return "There are no agent transactions available for the selected filters.";

        // Step 6: Present data
        List<TopAgentResult> results = JsonSerializer.Deserialize<List<TopAgentResult>>(JsonSerializer.Serialize(result))!;
        string names = results.Select(r => r.Agent + " with " + r.Transactions + " transactions").Aggregate((a, b) => a + ", " + b);
        return $"The top {top} agents for KAM are: {names}";
    }

    [McpServerTool]
    [Description("Retrieves detailed loan transactions for a specific agent by name. " +
        "USE THIS when the user asks: " +
        "'show me transactions for [agent name]', 'transactions for [agent]', " +
        "'list deals for [agent]', 'what loans did [agent] close', " +
        "'get transactions for [agent]', 'show [agent]'s deals', '[agent] transactions'. " +
        "Returns complete transaction details including borrower, lender, loan amounts, property information. " +
        "Supports fuzzy name matching and phonetic search (e.g., 'Maya' matches 'Maya Haffar'). " +
        "Optional filters: year, date range, top count (default 10).")]
    [HttpGet("/loans/agent/{agent}")]
    public string GetTransactionsByAgent(
        [Description("Name of the agent to search for. Supports partial names (e.g., 'Maya'), fuzzy matching (e.g., 'Jon' matches 'John'), and phonetic search (e.g., 'Mya' matches 'Maya'). Can be first name, last name, or full name.")]
        string agent,
        [Description("Optional filter: Maximum number of most recent transactions to return. Default is 10. Specify a higher number to see more results.")] int top = 10,
        [Description("Optional filter: Specific year to filter transactions (e.g., 2024, 2025). When provided, only transactions from that year are returned.")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from (inclusive). Format: YYYY-MM-DD. Use with 'to' parameter for date range filtering.")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to (inclusive). Format: YYYY-MM-DD. Use with 'from' parameter for date range filtering.")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization. Supports fuzzy and phonetic matching for user identification.")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        var allAgents = svc.GetLoanTransactions().Result
            .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
            .Select(lt => new { AgentName = lt.AgentName })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for agent
        var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

        // Step 3: Get user related to phonetic results
        if (matchedAgent != null)
        {
            agent = matchedAgent.AgentName ?? agent;
        }
        else
        {
            // Try direct partial match as fallback
            var partialMatch = allAgents
                .FirstOrDefault(a => a.AgentName != null &&
                    a.AgentName.Contains(agent, StringComparison.OrdinalIgnoreCase));

            if (partialMatch != null)
            {
                agent = partialMatch.AgentName ?? agent;
            }
        }

        // Step 1-3 for name parameter (if provided)
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
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "Transaction data is not available right now. Please try again later.";
        }

        var data = Filter(svc, agent, year, from, to)
            .Where(t => !string.IsNullOrWhiteSpace(t.LoanTransID))
            .Where(t => t.LoanAmount.HasValue)
            .Take(top)
            .Select(g => new LoanTransactionResult
            {
                LoanTransID = g.LoanTransID,
                AgentName = g.AgentName,
                LoanAmount = g.LoanAmount,
                LoanType = g.LoanType,
                LoanTerm = g.LoanTerm,
                BorrowerName = $"{g.BorrowerFirstName} {g.BorrowerLastName}".Trim(),
                LenderName = g.LenderName,
                TitleCompany = g.TitleCompany,
                PhoneNumber = g.AgentPhone,
                Address = g.SubjectAddress,
                City = g.SubjectCity,
                SubjectCity = g.SubjectCity,
                SubjectState = g.SubjectState,
                Active = g.Active,
                DateAdded = g.DateAdded?.ToString("yyyy-MM-dd")
            })
            .ToList();

        if (data == null || data.Count == 0)
            return $"No transactions found for agent '{agent}' with the selected filters.";

        // Step 6: Present data
        string yearInfo = year.HasValue ? $" in {year}" : "";
        string filterInfo = from.HasValue || to.HasValue ?
            $" from {from?.ToString("yyyy-MM-dd") ?? "start"} to {to?.ToString("yyyy-MM-dd") ?? "end"}" : "";

        string transactions = string.Join("; ", data.Select(r =>
            $"Loan #{r.LoanTransID}: Borrower: {r.BorrowerName}, " +
            $"Lender: {r.LenderName}, " +
            $"Amount: ${r.LoanAmount:N2}, " +
            $"Type: {r.LoanType}, " +
            $"Term: {r.LoanTerm}, " +
            $"Title: {r.TitleCompany}, " +
            $"Property: {r.Address}, {r.City}, {r.SubjectState}, " +
            $"Date: {r.DateAdded}"));

        return $"Found {data.Count} transaction{(data.Count != 1 ? "s" : "")} for {agent}{yearInfo}{filterInfo}: {transactions}";
    }

    [McpServerTool]
    [Description("Retrieves all loan transactions that have not been closed yet (no actual closed date). " +
        "Allows viewing pending or in-progress loans that are still active. " +
        "Supports optional filtering by year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When year filter is applied, only open loans added in that specific year are returned. " +
        "When date range filters are applied, only open loans added within the from/to date range are included. " +
        "Use this when the user asks about pending loans, active deals, or unclosed transactions. " +
        "Relevant for questions like: which loans are still open, show me pending transactions, what deals haven't closed yet, or list active loans.")]
    [HttpGet("/loans/open")]
    public string GetOpenLoans(
        [Description("Optional filter: Maximum number of open loans to return (default is 10)")] int top = 10,
        [Description("Name of the agent to search for. Supports partial names (e.g., 'Maya'), fuzzy matching (e.g., 'Jon' matches 'John'), and phonetic search (e.g., 'Mya' matches 'Maya'). Can be first name, last name, or full name.")] string agent = null,
        [Description("Optional filter: Year to filter loans by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter loans from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter loans to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetLoanTransactions().Result
                .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
                .Select(lt => new { AgentName = lt.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);
            
            // Step 3: Get user related to phonetic results
            if (matchedAgent != null)
            {
                agent = matchedAgent.AgentName;
            }
            else
            {
                // Try direct partial match as fallback
                var partialMatch = allAgents
                    .FirstOrDefault(a => a.AgentName != null && 
                        a.AgentName.Contains(agent, StringComparison.OrdinalIgnoreCase));
            
                if (partialMatch != null)
                {
                    agent = partialMatch.AgentName;
                }
            }
        }

        // Step 1-3 for name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            
            // Step 2: Match phonetics
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            // Step 3: Get user related to phonetic results
            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        var loans = new List<LoanTransactionResult>();

        // Step 5: Get data if authorized
        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "Transaction data is not available right now. Please try again later.";
        }
        else
        {
            loans = Filter(svc, agent, year, from, to)
                        .Where(t => !string.IsNullOrWhiteSpace(t.LoanTransID) && t.ActualClosedDate == null)
                        .Take(top)
                        .Select(t => new LoanTransactionResult
                        {
                            LoanTransID = t.LoanTransID,
                            AgentName = t.AgentName,
                            LoanAmount = t.LoanAmount,
                            LoanType = t.LoanType,
                            LoanTerm = t.LoanTerm,
                            BorrowerName = $"{t.BorrowerFirstName} {t.BorrowerLastName}".Trim(),
                            LenderName = t.LenderName,
                            TitleCompany = t.TitleCompany,
                            PhoneNumber = t.AgentPhone,
                            Address = t.SubjectAddress,
                            City = t.SubjectCity,
                            SubjectCity = t.SubjectCity,
                            SubjectState = t.SubjectState,
                            Active = t.Active,
                            DateAdded = t.DateAdded?.ToString("yyyy-MM-dd")
                        })
                        .ToList();

            if (!loans.Any())
            {
                string agentInfo = !string.IsNullOrEmpty(agent) ? $" for agent {agent}" : "";
                result = $"No open loans found{agentInfo} with the selected filters.";
            }
            else
            {
                result = string.Join(", ", loans.Select(l =>
                    $"Loan #{l.LoanTransID}: Agent: {l.AgentName}, Borrower: {l.BorrowerName}, " +
                    $"Lender: {l.LenderName}, Title: {l.TitleCompany}, " +
                    $"Amount: ${l.LoanAmount:N2}, Type: {l.LoanType}, Term: {l.LoanTerm}, " +
                    $"Property: {l.Address}, {l.City}, {l.SubjectState}, " +
                    $"Status: {l.Active}, Added: {l.DateAdded}"));
            }
        }

        // Step 6: Present data
        string agentFilterInfo = !string.IsNullOrEmpty(agent) ? $" for {agent}" : "";
        return $"Found {loans?.Count ?? 0} open loan{(loans?.Count != 1 ? "s" : "")}{agentFilterInfo}: {result}";
    }



    //POPULARITY TOOLS

    [McpServerTool]
    [Description("Retrieves the most frequently occurring ZIP code in loan transactions, or a ranked list of top ZIP codes. " +
        "Allows identifying popular areas or neighborhoods for property transactions. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), and date range (from/to dates). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions from that specific year are counted. " +
        "'what's the most popular ZIP code', 'most common ZIP code', " +
        "'which ZIP code has the most deals', 'top ZIP code', " +
        "'most popular area by ZIP', 'ZIP code with most transactions', " +
        "'most popular ZIP code in [year]', 'most common ZIP in [year]'. " +
        "Returns the ZIP code with the highest transaction count. " +
        "Supports optional filtering by agent name, year, and date range." +
        "When date range filters are applied, only transactions within the from/to date range are included. " +
        "Use this when the user asks about popular ZIP codes, most common areas, or top locations for transactions. " +
        "Relevant for questions like: what's the most popular ZIP code, which area has the most deals, show me top ZIP codes, or where are most properties located.")]
    [HttpGet("/loans/top-zips")]
    public string GetMostPopularZip(
        [Description("Optional filter: Maximum number of top ZIP codes to return (default is 1 for most popular)")] int top = 1,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetLoanTransactions().Result
                .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
                .Select(lt => new { AgentName = lt.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);
            
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
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        string result = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            result = "Transaction data is not available right now. Please try again later.";
        }
        else
        {
            var data = Filter(svc, agent, year, from, to)
               .Where(t => !string.IsNullOrEmpty(t.SubjectPostalCode))
               .Where(t => t.SubjectPostalCode != "NULL" && 
                          t.SubjectPostalCode != "N/A" &&
                          t.SubjectPostalCode != "n/a" &&
                          !t.SubjectPostalCode.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                          !t.SubjectPostalCode.Equals("n/a", StringComparison.OrdinalIgnoreCase));

            var zipResult = data.GroupBy(t => t.SubjectPostalCode, StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(g => g.Count())
                      .FirstOrDefault();
        
            if (zipResult == null || zipResult.Count() == 0)
            {
                string agentInfo = !string.IsNullOrEmpty(agent) ? $" for agent {agent}" : "";
                string yearInfo = year.HasValue ? $" in {year}" : "";
                result = $"No ZIP code data available{agentInfo}{yearInfo} with the selected filters.";
            }
            else
            {
                result = $"{zipResult.Key} with {zipResult.Count()} transaction{(zipResult.Count() != 1 ? "s" : "")}";
            }
        }

        // Step 6: Present data
        return $"The most popular ZIP code is: {result}";
    }


    [McpServerTool]
    [Description("Retrieves a ranked list of cities with the highest number of loan transactions. " +
        "Allows identifying the most active cities for property transactions and market trends. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name, year (specific year), date range (from/to dates), and top count (maximum number of results). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions from that specific year are counted. " +
        "When date range filters are applied, only transactions within the from/to date range are included. " +
        "Use this when the user asks about popular cities, most active markets, or top locations for transactions. " +
        "Relevant for questions like: which cities have the most deals, what are the top cities, show me the most active markets, or where are most transactions happening.")]
    [HttpGet("/loans/top-cities")]
    public string GetTopCities(
        [Description("Optional filter: Maximum number of top cities to return (default is 10)")] int top = 10,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetLoanTransactions().Result
                .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
                .Select(lt => new { AgentName = lt.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);
            
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
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
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
                             .Select(g => new { City = g.Key, State = g.First().SubjectState, Transactions = g.Count() });

            if (!result.Any() || result.Count() == 0)
            {
                return "Result Not Available";
            }

            List<TopCityResult> results = JsonSerializer.Deserialize<List<TopCityResult>>(JsonSerializer.Serialize(result))!;

            names = results.Select(r => $"{r.City}, {r.State} with {r.Transactions} transactions")
                           .Aggregate((a, b) => a + ", " + b);
        }

        // Step 6: Present data
        return $"The {top} cities with the highest number of transactions are: {names}";
    }



    [McpServerTool]
    [Description("Retrieves the most popular type for a category: Property, Transaction, Mortgage, or Loan. " +
        "USE THIS when the user asks: " +
        "'what's the most popular loan type', 'most common loan type', " +
        "'what's the most popular property type', 'which property type is most common', " +
        "'most common property type', 'most popular transaction type', " +
        "'what's the most popular mortgage type', 'most frequent loan type'. " +
        "Category must be one of: Property, Transaction, Mortgage, or Loan. " +
        "Automatically detects category from query: " +
        "- 'loan type' or 'loan' -> Loan category " +
        "- 'property type' or 'property' -> Property category " +
        "- 'transaction type' or 'transaction' -> Transaction category " +
        "- 'mortgage type' or 'mortgage' -> Mortgage category")]
    [HttpGet("/loans/most-popular-type/{category}")]
    public string GetMostPopularType(
        [Description("Type category to analyze. Must be one of: 'Property', 'Transaction', 'Mortgage', or 'Loan'. " +
            "For loan queries use 'Loan', for property queries use 'Property', " +
            "for transaction queries use 'Transaction', for mortgage queries use 'Mortgage'.")]
        string category,
        [Description("Optional filter: Name of the agent to filter transactions by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter transactions by (e.g., 2024, 2025)")] int? year = null,
        [Description("Optional filter: Start date to filter transactions from (inclusive)")] DateTime? from = null,
        [Description("Optional filter: End date to filter transactions to (inclusive)")] DateTime? to = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetLoanTransactions().Result
                .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
                .Select(lt => new { AgentName = lt.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);
            
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
            
            // Step 2: Match phonetics
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            // Step 3: Get user related to phonetic results
            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        string type = "";
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            type = "Transaction data is not available right now. Please try again later.";
        }
        else
        {
            // Normalize the category parameter
            string normalizedCategory = category.Trim().ToLower();

            // Determine which field to query based on category
            Func<LoanTransaction, string?> selector = normalizedCategory switch
            {
                "property" => t => t.PropType,
                "transaction" => t => t.TransactionType,
                "mortgage" => t => t.MortgageType,
                "loan" => t => t.LoanType,
                _ => throw new ArgumentException($"Invalid category '{category}'. Valid options are: Property, Transaction, Mortgage, or Loan.")
            };

            var data = Filter(svc, agent, year, from, to)
                .Where(t => !string.IsNullOrWhiteSpace(selector(t)))
                .Where(t => selector(t) != "NULL" && 
                           selector(t) != "-- Select --" && 
                           !selector(t)!.StartsWith("--"));

            var result = data.GroupBy(t => selector(t), StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(g => g.Count())
                             .Take(1)
                             .Select(g => new { Type = g.Key, Transactions = g.Count() })
                             .FirstOrDefault();

            if (result == null || result.Transactions == 0)
            {
                string agentInfo = !string.IsNullOrEmpty(agent) ? $" for agent {agent}" : "";
                string yearInfo = year.HasValue ? $" in {year}" : "";
                return $"No {normalizedCategory} type data available{agentInfo}{yearInfo} with the selected filters.";
            }

            type = $"{result.Type} with {result.Transactions} transaction{(result.Transactions != 1 ? "s" : "")}";
        }

        // Step 6: Present data
        return $"The most popular {category.ToLower()} type is: {type}";
    }


    [McpServerTool]
    [Description("Retrieves loan transactions handled by a specific escrow company. " +
        "USE THIS when the user asks: " +
        "'show me transactions for [escrow company]', 'transactions for [escrow]', " +
        "'what deals did [escrow company] handle', 'list deals for [escrow]', " +
        "'show [escrow company]'s transactions', '[escrow] deals', " +
        "'transactions handled by [escrow company]', 'get [escrow] transactions'. " +
        "DO NOT use this for title companies - only for escrow companies. " +
        "Returns transaction details for the specified escrow company. " +
        "Supports fuzzy name matching and phonetic search for company identification.")]
    [HttpGet("/loans/ecrowCompany/{escrowCompany}")]
    public string GetTransactionsByEscrowCompany(
        [Description("Name of the escrow company to search for. Supports partial names (e.g., 'Sun Escrow'), " +
            "fuzzy matching, and phonetic search. Can be full or partial company name.")]
        string escrowCompany,
        [Description("Optional filter: Maximum number of transactions to return (default is 10)")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on escrowCompany parameter
        var allEscrowCompanies = svc.GetLoanTransactions().Result
        .Where(lt => !string.IsNullOrWhiteSpace(lt.EscrowCompany))
        .Select(lt => new { EscrowCompany = lt.EscrowCompany })
        .Distinct()
        .ToList();

        // Step 2: Match phonetics for escrow company
        var matchedEscrow = Common.MatchPhonetic(allEscrowCompanies, escrowCompany, e => e.EscrowCompany ?? string.Empty);
        
        // Step 3: Get company related to phonetic results
        if (matchedEscrow != null)
        {
            escrowCompany = matchedEscrow.EscrowCompany ?? escrowCompany;
        }
        else
        {
            // Try direct partial match as fallback
            var partialMatch = allEscrowCompanies
                .FirstOrDefault(e => e.EscrowCompany != null && 
                    e.EscrowCompany.Contains(escrowCompany, StringComparison.OrdinalIgnoreCase));
        
            if (partialMatch != null)
            {
                escrowCompany = partialMatch.EscrowCompany ?? escrowCompany;
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
        var authError = Common.CheckAdminAuthorization(_httpContextAccessor, user_id, user_role, token);
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
            var transactions = svc.GetByEscrowCompany(escrowCompany)
     .Where(t => !string.IsNullOrEmpty(t.LoanTransID))
                  .Take(top)
                  .Select(t => new LoanTransactionResult
                  {
                      LoanTransID = t.LoanTransID,
                      AgentName = t.AgentName,
                      LoanAmount = t.LoanAmount,
                      LoanType = t.LoanType,
                      LoanTerm = t.LoanTerm,
                      BorrowerName = $"{t.BorrowerFirstName} {t.BorrowerLastName}".Trim(),
                      LenderName = t.LenderName,  // ADDED
                      TitleCompany = t.TitleCompany,  // ADDED
                      PhoneNumber = t.AgentPhone,
                      Address = t.SubjectAddress,
                      City = t.SubjectCity,
                      SubjectCity = t.SubjectCity,
                      SubjectState = t.SubjectState,
                      Active = t.Active,
                      DateAdded = t.DateAdded?.ToString("yyyy-MM-dd")
                  }).ToList();

            if (!transactions.Any())
            {
                result = "no transactions found";
            }
            else
            {
                result = string.Join(", ", transactions.Select(r =>
                    $"Loan #{r.LoanTransID}, Agent: {r.AgentName}, Borrower: {r.BorrowerName}, " +
                    $"Lender: {r.LenderName}, Title Company: {r.TitleCompany}, " +  // ADDED
                    $"Loan Term: {r.LoanTerm}, Loan Amount: {r.LoanAmount}, Loan Type: {r.LoanType}, " +
                    $"Address: {r.Address}, City: {r.City}, State: {r.SubjectState}, " +
                    $"Active: {r.Active}, Date Added: {r.DateAdded}"));
            }
        }

        // Step 6: Present data
        return $"The transactions for {escrowCompany} are: {result}";
    }

    [McpServerTool]
    [Description("Retrieves a ranked list of escrow companies by transaction count. " +
        "USE THIS when the user asks: " +
        "'what are the top escrow companies', 'top escrow companies', " +
        "'most used escrow companies', 'which escrow companies do we use most', " +
        "'best escrow companies', 'most active escrow companies', " +
        "'rank escrow companies', 'escrow companies by volume', " +
        "'show me the top escrow firms'. " +
        "Returns escrow companies ranked by number of transactions. " +
        "Supports optional top count parameter (default 10).")]
    [HttpGet("/loans/top-escrow-companies")]
    public string GetTopEscrowCompanies(
        [Description("Optional filter: Maximum number of top escrow companies to return (default is 10)")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            
            // Step 2: Match phonetics
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            // Step 3: Get user related to phonetic results
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

        // Step 6: Present data
        return $"The top {top} escrow companies are: {names}";
    }

    //Include top tile companies, number of transaction
    [McpServerTool]
    [Description("Retrieves a complete list of all title company names in the system. " +
        "Allows viewing all title companies that have been involved in transactions. " +
        "No filtering options available - returns all unique title company names. " +
        "Use this when the user asks about title companies, available title services, or title company directory. " +
        "Relevant for questions like: what title companies do we work with, list all title companies, show me available title companies, or what are the names of title companies.")]
    [HttpGet("/loans/title-companies")]
    public string GetAllTitleCompanies(
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            
            // Step 2: Match phonetics
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            // Step 3: Get user related to phonetic results
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

        // Step 6: Present data
        return resultText;
    }


    [McpServerTool]
    [Description("Retrieves the IRS Form 1099 total commission amount for a specific agent for a given tax year. " +
        "Allows calculating total taxable income earned by an agent from commissions. " +
        "Supports fuzzy name matching and phonetic search for agent identification. " +
        "Year parameter is required and specifies the tax year for the 1099 calculation. " +
        "Use this when the user asks about agent earnings for KAM, agent revenue contribution, or agent performance metrics. " +
        "Relevant for questions like: how much did this agent make for KAM, what were the agent's earnings, show me agent revenue for the year, or what's the agent's contribution to company earnings.")]
    [HttpGet("/loans/1099/{agent}/{year}")]
    public string GetAgent1099(
        [Description("Name of the agent whose 1099 to retrieve (supports fuzzy and phonetic matching)")] string agent,
        [Description("Tax year for the 1099 calculation (e.g., 2024, 2025)")] int year,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        var allAgents = svc.GetLoanTransactions().Result
            .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
            .Select(lt => new { AgentName = lt.AgentName })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for agent
        var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);
        
        // Step 3: Get user related to phonetic results
        if (matchedAgent != null)
        {
            agent = matchedAgent.AgentName ?? agent;
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

        // Step 6: Present data
        return resultText;
    }


    [McpServerTool]
    [Description("Retrieves comprehensive loan statistics for a specific lender including total loan count and loan amount analytics. " +
        "Allows analyzing lender performance with metrics like average, highest, and lowest loan amounts. " +
        "Supports fuzzy name matching and phonetic search for lender identification. " +
        "Returns total loans, average loan amount, maximum loan amount, and minimum loan amount. " +
        "Use this when the user asks about lender statistics, lender performance, or lender loan analytics. " +
        "Relevant for questions like: show me lender statistics, what are the loan amounts for this lender, how is this lender performing, or give me lender analytics.")]
    [HttpGet("/loans/lender-statistics/{lender}")]
    public string GetLenderStats(
        [Description("Name of the lender whose statistics to retrieve (supports fuzzy and phonetic matching)")] string lender,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on lender parameter
        var allLenders = svc.GetLoanTransactions().Result
            .Where(lt => !string.IsNullOrWhiteSpace(lt.LenderName))
            .Select(lt => new { LenderName = lt.LenderName })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for lender
        var matchedLender = Common.MatchPhonetic(allLenders, lender, l => l.LenderName ?? string.Empty);
        
        // Step 3: Get lender related to phonetic results
        if (matchedLender != null)
        {
            lender = matchedLender.LenderName ?? lender;
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

        // Step 6: Present data
        return resultText;
    }

    [McpServerTool]
    [Description("Retrieves complete property information for a specific address including agent, borrower, lender, and loan details. " +
        "Allows looking up comprehensive transaction data associated with a property address. " +
        "Returns agent name, borrower name, lender name, title company, loan ID, loan term, city, and state. " +
        "Use this when the user asks about property information, address details, or property transaction history. " +
        "Relevant for questions like: show me information for this address, what are the details for this property, who handled this property, or lookup property information.")]
    [HttpGet("/loans/property-info/{address}")]
    public string GetPropertyAddressInfo(
        [Description("Property address to look up (exact or partial match)")]
        string address,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on name parameter
        if (name != "unknown" && !string.IsNullOrWhiteSpace(name))
        {
            var allUsers = new UserService().GetUsers().Result;
            
            // Step 2: Match phonetics
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            // Step 3: Get user related to phonetic results
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
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
            return "The data is not available right now.";

        var loan = svc.GetLoanTransactions().Result
                      .FirstOrDefault(t =>
                          !string.IsNullOrEmpty(t.SubjectAddress) &&
                          t.SubjectAddress.Equals(address, StringComparison.OrdinalIgnoreCase));

        if (loan == null)
        {
            return $"No property information found for address '{address}'.";
        }

        // Step 6: Present data with all required fields
        var borrowerName = $"{loan.BorrowerFirstName} {loan.BorrowerLastName}".Trim();
        var agentName = loan.AgentName ?? "Not available";
        var lenderName = loan.LenderName ?? "Not available";
        var titleCompany = loan.TitleCompany ?? "Not available";
        var loanId = loan.LoanTransID ?? "Not available";
        var loanTerm = loan.LoanTerm?.ToString() ?? "Not available";
        var city = loan.SubjectCity ?? "Not available";
        var state = loan.SubjectState ?? "Not available";

        return $"Property information for '{address}': " +
               $"Agent Name: {agentName}, " +
               $"Borrower Name: {borrowerName}, " +
               $"Lender Name: {lenderName}, " +
               $"Title Company: {titleCompany}, " +
               $"Loan ID: {loanId}, " +
               $"Loan Term: {loanTerm}, " +
               $"City: {city}, " +
               $"State: {state}";
    }

    [McpServerTool]
    [Description("Retrieves comprehensive loan portfolio statistics including loan amounts and credit score analytics. " +
        "Allows analyzing overall loan performance with metrics for both loan amounts and borrower credit scores. " +
        "Supports fuzzy name matching and phonetic search for agent identification when agent filter is used. " +
        "Supports optional filtering by agent name (specific agent) and year (specific year). " +
        "When agent filter is applied, only transactions for that agent are analyzed. " +
        "When year filter is applied, only transactions from that specific year are included. " +
        "Returns average, highest, and lowest values for both loan amounts and credit scores. " +
        "Use this when the user asks about loan statistics, portfolio analytics, or overall loan performance. " +
        "Relevant for questions like: show me loan statistics, what are the average loan amounts, give me portfolio analytics, or what are the credit score statistics.")]
    [HttpGet("/loans/statistics")]
    public string GetLoansStatistics(
        [Description("Optional filter: Name of the agent to filter statistics by (supports fuzzy and phonetic matching)")] string? agent = null,
        [Description("Optional filter: Year to filter statistics by (e.g., 2024, 2025)")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        if (!string.IsNullOrEmpty(agent))
        {
            var allAgents = svc.GetLoanTransactions().Result
                .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
                .Select(lt => new { AgentName = lt.AgentName })
                .Distinct()
                .ToList();

            // Step 2: Match phonetics for agent
            var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);
            
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
            var matchedUser = Common.MatchPhonetic(allUsers, name, u => u.Name ?? string.Empty);
            
            if (matchedUser != null)
            {
                name = matchedUser.Name ?? name;
                user_role = matchedUser.Role ?? user_role;
            }
        }

        // Step 4: Authorization
        var authError = Common.CheckSpecificAuthorization(_httpContextAccessor, agent, name, user_id, user_role, token, out string effectiveAgent);
        if (authError != null)
            return authError;

        agent = effectiveAgent;

        // Step 5: Get data if authorized
        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            return "Loan statistics are not available right now";
        }

        var filteredData = FilterByAgentAndYear(svc, agent, year);
        
        // Calculate loan amount statistics
        var loanAmounts = filteredData
            .Where(t => t.LoanAmount.HasValue)
            .Select(t => t.LoanAmount!.Value)
            .ToList();

        string avgLoan = loanAmounts.Any() ? loanAmounts.Average().ToString("F2") : "N/A";
        string maxLoan = loanAmounts.Any() ? loanAmounts.Max().ToString("F2") : "N/A";
        string minLoan = loanAmounts.Any() ? loanAmounts.Min().ToString("F2") : "N/A";

        // Calculate credit score statistics
        var creditScores = filteredData
            .Where(t => t.CreditScore.HasValue)
            .Select(t => t.CreditScore!.Value)
            .ToList();

        string avgCredit = creditScores.Any() ? creditScores.Average().ToString("F2") : "N/A";
        string maxCredit = creditScores.Any() ? creditScores.Max().ToString("F2") : "N/A";
        string minCredit = creditScores.Any() ? creditScores.Min().ToString("F2") : "N/A";

        // Step 6: Present data
        string filterInfo = !string.IsNullOrEmpty(agent) ? $" for {agent}" : "";
        filterInfo += year.HasValue ? $" in {year}" : "";

        return $"Loan statistics{filterInfo}: " +
               $"Average loan amount: {avgLoan}, " +
               $"Highest loan amount: {maxLoan}, " +
               $"Lowest loan amount: {minLoan}, " +
               $"Average credit score: {avgCredit}, " +
               $"Highest credit score: {maxCredit}, " +
               $"Lowest credit score: {minCredit}";
    }

    [McpServerTool]
    [Description("Retrieves the total earnings (commission amount) an agent generated for KAM in a specific year along with transaction count. " +
       "Allows calculating agent contribution to company revenue including both commission totals and deal volume. " +
       "Supports fuzzy name matching and phonetic search for agent identification. " +
       "Year parameter is required and specifies the tax/earnings year to calculate. " +
       "Returns both the total commission amount earned and the number of transactions completed. " +
       "Use this when the user asks about agent earnings for KAM, agent revenue contribution, or agent performance metrics. " +
       "Relevant for questions like: how much did this agent make for KAM, what were the agent's earnings, show me agent revenue for the year, or what's the agent's contribution to company earnings.")]
    [HttpGet("/loans/agent-kam-earnings/{agent}/{year}")]
    public string GetAgentKamEarnings(
       [Description("Name of the agent whose earnings to retrieve (supports fuzzy and phonetic matching)")] string agent,
       [Description("Tax/earnings year to calculate (e.g., 2024, 2025)")] int year,
       [Description("user_id")] int user_id = 0,
       [Description("user_role")] string user_role = "unknown",
       [Description("token")] string token = "unknown",
       [Description("Optional filter: Name of the user making the request for authorization (supports fuzzy and phonetic matching)")] string name = "unknown")
    {
        // Step 1: Get data for phonetic matching on agent parameter
        var allAgents = svc.GetLoanTransactions().Result
            .Where(lt => !string.IsNullOrWhiteSpace(lt.AgentName))
            .Select(lt => new { AgentName = lt.AgentName })
            .Distinct()
            .ToList();

        // Step 2: Match phonetics for agent
        var matchedAgent = Common.MatchPhonetic(allAgents, agent, a => a.AgentName ?? string.Empty);

        // Step 3: Get user related to phonetic results
        if (matchedAgent != null)
        {
            agent = matchedAgent.AgentName ?? agent;
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
        string resultText = "";

        if (!string.IsNullOrEmpty(svc.ErrorLoadCsv))
        {
            resultText = "not available right now";
        }
        else
        {
            var transactions = Filter(svc, agent, year, null, null)
                .Where(t => t.LoanAmount.HasValue)
                .ToList();

            if (!transactions.Any())
            {
                resultText = $"{agent} had no transactions for KAM in {year}.";
            }
            else
            {
                int numTransactions = transactions.Count;
                decimal totalCommission = svc.GetAgent1099(agent, year);

                resultText = $"{agent} made ${totalCommission:N2} for KAM in {year} with {numTransactions} transaction{(numTransactions != 1 ? "s" : "")}.";
            }
        }

        // Step 6: Present data
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
        var data = svc.GetLoanTransactions().Result
            .OrderByDescending(t => t.DateAdded).AsEnumerable();

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
