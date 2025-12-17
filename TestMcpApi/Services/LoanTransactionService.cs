using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Http;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using TestMcpApi.Models;

namespace TestMcpApi.Services;      

public class LoanTransactionService : ILoanTransactionService
{
    private readonly List<LoanTransaction> _data = new();
    private string _errorLoadCsv = string.Empty;

    public string ErrorLoadCsv => _errorLoadCsv;

    public LoanTransactionService()
    {
        _errorLoadCsv = LoadData();
    }
    private string LoadData()
    {
        try
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            var configuration = builder.Build();
            var connStr = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connStr))
                return "Connection string 'DefaultConnection' not found in appsettings.json";

            using SqlConnection db = new SqlConnection(connStr);
            const string sql = "SELECT * FROM LoanTransactionsView ORDER BY 1 DESC";
            var list = db.Query<LoanTransaction>(sql).AsList();
            _data.AddRange(list);

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"Error loading data from database: {ex.Message}";
        }
    }

    public Task<List<LoanTransaction>> GetLoanTransactions()
    {
        return Task.FromResult(_data);
    }
    public Task<LoanTransaction?> GetLoanTransactionById(string loanTransID)
    {
        var transaction = _data.FirstOrDefault(t => t.LoanTransID == loanTransID);
        return Task.FromResult(transaction);
    }
    public IEnumerable<LoanTransaction> GetByAgent(string agent)
        => _data.Where(t => t.AgentName != null &&
                            t.AgentName.Contains(agent, StringComparison.OrdinalIgnoreCase));

    public LoanTransaction? GetByLoanNumber(string loanId)
        => _data.FirstOrDefault(t => t.LoanTransID == loanId);

    public IEnumerable<LoanTransaction> GetByState(string state)
        => _data.Where(t => t.SubjectState.Equals(state, StringComparison.OrdinalIgnoreCase));

    public string? GetLender(string loanId)
        => GetByLoanNumber(loanId)?.LenderName;

    public decimal? GetLTV(string loanId)
        => GetByLoanNumber(loanId)?.LTV;

    public string? GetSubjectAddress(string loanId)
        => GetByLoanNumber(loanId)?.SubjectAddress;

    public string GetMostPopularZip()
        => _data.GroupBy(t => t.SubjectPostalCode)
                .OrderByDescending(g => g.Count())
                .First().Key!;

    public IEnumerable<object> GetTopCities(int top = 10)
        => _data.GroupBy(t => t.SubjectCity)
                .OrderByDescending(g => g.Count())
                .Take(top)
                .Select(g => new { City = g.Key, Count = g.Count() });
    public int GetTotalTransactionsByAgent(string agent)
=> _data.Count(t => t.AgentName != null &&
                    t.AgentName.Equals(agent, StringComparison.OrdinalIgnoreCase));

    public int GetTotalTransactionsByLender(string lender)
        => _data.Count(t => t.LenderName != null &&
                            t.LenderName.Equals(lender, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<object> GetTopAgents(int top = 10)
        => _data.GroupBy(t => t.AgentName)
                .OrderByDescending(g => g.Count())
                .Take(top)
                .Select(g => new { Agent = g.Key, Transactions = g.Count() });

    public IEnumerable<string> GetAllAgents(bool sortByName = true, bool descending = false)
    {
        var agents = _data.Select(t => t.AgentName)
                        .Where(a => !string.IsNullOrEmpty(a))
                        .Distinct();

        if (!sortByName)
            agents = agents.OrderBy(a => _data.Count(t => t.AgentName == a));

        return descending ? agents.Reverse() : agents;
    }

    public IEnumerable<string> GetAllTitleCompanies()
        => _data.Select(t => t.TitleCompany)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()!;

    public IEnumerable<LoanTransaction> GetByTitleCompany(string titleCompany)
        => _data.Where(t => t.TitleCompany != null &&
                            t.TitleCompany.Equals(titleCompany, StringComparison.OrdinalIgnoreCase));

    // Loans filtered by agent(s) and/or year(s)
    public IEnumerable<LoanTransaction> GetLoansByAgentsAndYears(
        IEnumerable<string>? agents = null,
        IEnumerable<int>? years = null)
    {
        var data = _data.AsEnumerable();
        if (agents != null && agents.Any())
            data = data.Where(t => t.AgentName != null && agents.Contains(t.AgentName, StringComparer.OrdinalIgnoreCase));
        if (years != null && years.Any())
            data = data.Where(t => t.DateAdded.HasValue && years.Contains(t.DateAdded.Value.Year));
        return data;
    }

    // Loans filtered by date range
    public IEnumerable<LoanTransaction> GetLoansByDateRange(DateTime? from = null, DateTime? to = null)
    {
        var data = _data.AsEnumerable();
        if (from.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value >= from.Value);
        if (to.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value <= to.Value);
        return data;
    }

    // 1099 for an agent in a specific year (sum of KamBrokerFee)
    public decimal GetAgent1099(string agent, int year)
    {
        return _data
            .Where(t => t.AgentName != null &&
                        t.AgentName.Equals(agent, StringComparison.OrdinalIgnoreCase) &&
                        t.DateAdded.HasValue &&
                        t.DateAdded.Value.Year == year)
            .Sum(t => t.KamBrokerFee ?? 0m);
    }

    public IEnumerable<LoanTransaction> GetByEscrowCompany(string escrowCompany)
    => _data.Where(t => t.EscrowCompany != null &&
                        t.EscrowCompany.Equals(escrowCompany, StringComparison.OrdinalIgnoreCase));

    // Lender-specific info: total loans, average loan amount, highest/lowest loan amounts
    public (int totalLoans, decimal avgAmount, decimal maxAmount, decimal minAmount) GetLenderStats(string lender)
    {
        var loans = _data.Where(t => t.LenderName != null && t.LenderName.Equals(lender, StringComparison.OrdinalIgnoreCase) && t.LoanAmount.HasValue).ToList();
        if (!loans.Any())
            return (0, 0, 0, 0);

        var amounts = loans.Select(t => t.LoanAmount!.Value);
        return (loans.Count, amounts.Average(), amounts.Max(), amounts.Min());
    }
}



   
