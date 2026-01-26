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
    [Description("Get contact information for a specific agent")]
    [HttpGet("/loans/agent-contact-info/{agent}")]
    public string AgentContactInfo(
        [Description("What's the contact info of agent?")] string agent,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
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
            return "not available right now";
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
            });

        if (data == null || data.Count() == 0)
            return $"No transactions found for agent {agent} using the selected filters.";

        // Step 6: Present data
        List<LoanTransactionResult> results = data.ToList();
        string transactions = results.Select(r => 
            $"Loan #{r.LoanTransID}, Agent: {r.AgentName}, Borrower: {r.BorrowerName}, " +
            $"Lender: {r.LenderName}, Title Company: {r.TitleCompany}, " +
            $"Loan Amount: {r.LoanAmount}, Loan Type: {r.LoanType}, Loan Term: {r.LoanTerm}, " +
            $"Phone: {r.PhoneNumber}, Address: {r.Address}, City: {r.City}, " +
            $"Subject City: {r.SubjectCity}, Subject State: {r.SubjectState}, " +
            $"Active: {r.Active}, Date Added: {r.DateAdded}")
            .Aggregate((a, b) => a + ", " + b);
        return $"The transactions made by {agent}, during the year {year} are: {transactions}";
    }

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
                        })
                        .ToList();

            if (!loans.Any())
                result = "No open loans found";
            else
            {
                result = string.Join(", ", loans.Select(l =>
                    $"Loan #{l.LoanTransID}, Agent: {l.AgentName}, Borrower: {l.BorrowerName}, " +
                    $"Lender: {l.LenderName}, Title Company: {l.TitleCompany}, " +
                    $"Loan Term: {l.LoanTerm}, Loan Amount: {l.LoanAmount}, Loan Type: {l.LoanType}, " +
                    $"Address: {l.Address}, City: {l.City}, State: {l.SubjectState}, " +
                    $"Active: {l.Active}, Date Added: {l.DateAdded}"));
            }
        }

        // Step 6: Present data
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
            result = "not available right now";
        }
        else
        {
            var zip = GetMostPopularValueFiltered(svc, t => t.SubjectPostalCode, agent, year, from, to);
            result = string.IsNullOrEmpty(zip) ? "N/A" : zip;
        }

        // Step 6: Present data
        return $"The most popular ZIP code is: {result}";
    }


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
    [Description("Get the most popular type for a given category (Property, Transaction, Mortgage, or Loan)")]
    [HttpGet("/loans/most-popular-type/{category}")]
    public string GetMostPopularType(
        [Description("What is the most popular (Property, Transaction, Mortgage, or Loan) type?")] string category,
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
            Func<LoanTransaction, string?> selector = normalizedCategory switch
            {
                "property" => t => t.PropType,
                "transaction" => t => t.TransactionType,
                "mortgage" => t => t.MortgageType,
                "loan" => t => t.LoanType,
                _ => throw new ArgumentException($"Invalid category '{category}'. Valid options are: Property, Transaction, Mortgage, or Loan.")
            };

            var data = Filter(svc, agent, year, from, to)
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

        // Step 6: Present data
        return $"The most popular escrow company is: {company}";
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
    [Description("Get top Escrow Companies ranked by number of transactions")]
    [HttpGet("/loans/top-escrow-companies")]
    public string GetTopEscrowCompanies(
        [Description("What are the top escrow companies")] int top = 10,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
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
    [Description("What are the names of all title companies?")]
    [HttpGet("/loans/title-companies")]
    public string GetAllTitleCompanies(
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
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
    [Description("Get lender statistics (total loans, average, highest, lowest loan amounts)")]
    [HttpGet("/loans/lender-statistics/{lender}")]
    public string GetLenderStats(
        [Description("What are the total loans and loan amount statistics for this lender?")] string lender,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
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
    [Description("Get all information for a specific property address")]
    [HttpGet("/loans/property-info/{address}")]
    public string GetPropertyAddressInfo(
        [Description("Get complete information about this property address")]
        string address,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
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
    [Description("Get comprehensive loan statistics including loan amounts and credit scores")]
    [HttpGet("/loans/statistics")]
    public string GetLoansStatistics(
        [Description("Get statistics for all loans or filter by agent name")] string? agent = null,
        [Description("Filter by specific year")] int? year = null,
        [Description("user_id")] int user_id = 0,
        [Description("user_role")] string user_role = "unknown",
        [Description("token")] string token = "unknown",
        [Description("name")] string name = "unknown")
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
