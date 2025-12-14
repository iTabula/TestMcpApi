using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Http;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using CsvHelper.TypeConversion;

namespace TestMcpApi.Services;

public class LoanTransactionService
{
    private readonly List<LoanTransaction> _data = new();
    public LoanTransactionService()
    {
        LoadCsv();
    }
    private void LoadCsv()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "LoanTransactionsData.csv");
        //var path = "LoanTransactionsData.csv";

        if (!File.Exists(path))
            throw new FileNotFoundException($"CSV file not found at {path}");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            Delimiter = ","
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);

        // Setup global nulls for decimals
        csv.Context.TypeConverterOptionsCache.GetOptions<decimal?>().NullValues.Add("");
        csv.Context.TypeConverterOptionsCache.GetOptions<decimal?>().NullValues.Add("NULL");

        // Setup global nulls for datetime
        csv.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().NullValues.Add("");
        csv.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().NullValues.Add("NULL");

        try
        {
            _data.AddRange(csv.GetRecords<LoanTransaction>().ToList());
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to parse CSV file. " +
                                "Make sure the headers match the LoanTransaction properties.", ex);
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

public partial class LoanTransaction
{
    public string? LoanTransID { get; set; }
    public DateTime? ActualClosedDate { get; set; }
    public DateTime? DateAdded { get; set; }
    public string? AgentName { get; set; }
    public string? AddedBy { get; set; }
    public string? BorrowerFirstName { get; set; }
    public string? BorrowerLastName { get; set; }
    public string? BorrowerPhone { get; set; }
    public string? BorrowerEmail { get; set; }
    public string? SubjectAddress { get; set; }
    public string? SubjectCity { get; set; }
    public string? SubjectState { get; set; }
    public string? SubjectPostalCode { get; set; }
    public string? PropType { get; set; }
    public string? TransactionType { get; set; }
    public string? MortgageType { get; set; }
    public string? BrokeringType { get; set; }
    public string? LoanType { get; set; }
    public decimal? LoanTerm { get; set; }
    public decimal? LoanAmount { get; set; }
    public string? LenderName { get; set; }
    public decimal? AppraisedValue { get; set; }
    public string? AppraisalCompany { get; set; }
    public string? AppraisalCoPhone { get; set; }
    public decimal? LTV { get; set; }
    public decimal? InterestRate { get; set; }
    public string? TitleCompany { get; set; }
    public string? TitleCoPhone { get; set; }
    public decimal? CreditScore { get; set; }
    public string? EscrowCompany { get; set; }
    public string? EscrowCoPhone { get; set; }
    public string? EscrowOfficer { get; set; }
    public string? EscrowOfficerEmail { get; set; }
    public string? EscrowOfficerPhone { get; set; }
    public string? EscrowNumber { get; set; }
    public string? EscrowMethodSendType { get; set; }
    public DateTime? CommReceivedDate { get; set; }
    public decimal? ActualCommKamFromEscrow { get; set; }
    public decimal? KamBrokerFee { get; set; }
    public string? CommPaidMethod { get; set; }
    public DateTime? CommPaidDate { get; set; }
    public string? IncomingBank { get; set; }
    public string? OutgoingBank { get; set; }
    public decimal? AmountRetainedByKam { get; set; }
    public decimal? AmountPaidToKamAgent { get; set; }
    public decimal? OtherFee { get; set; }
    public decimal? DirectDepositFee { get; set; }
    public string? Active { get; set; }
    public string? AppraisalDone { get; set; }
    public string? CreditReportRan { get; set; }
    public string? WhoPaidForCreditReport { get; set; }
    public string? WhoPulledCreditReport { get; set; }
}


[JsonSerializable(typeof(List<LoanTransaction>))]
internal sealed partial class LoanTransactionContext : JsonSerializerContext
{

}