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

public class RealTransactionService : IRealTransactionService
{
    private readonly List<RealTransaction> _data = new();
    private string _errorLoadCsv = string.Empty;
    public string ErrorLoadCsv => _errorLoadCsv;

    public RealTransactionService()
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
            const string sql = "SELECT * FROM RealTransactionsView ORDER BY RealTransID DESC";
            var list = db.Query<RealTransaction>(sql).AsList();
            _data.AddRange(list);

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"Error loading data from database: {ex.Message}";
        }
    }

    public Task<List<RealTransaction>> GetRealTransactions() => Task.FromResult(_data);
    public Task<RealTransaction?> GetRealTransactionById(string realTransID)
        => Task.FromResult(_data.FirstOrDefault(t => t.RealTransID == realTransID));

    public IEnumerable<RealTransaction> GetByAgent(string agent)
        => _data.Where(t => t.AgentName != null &&
                            t.AgentName.Contains(agent, StringComparison.OrdinalIgnoreCase));

    public RealTransaction? GetByPropertyAddress(string subjectAddress)
    {
        return _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                         t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<RealTransaction> GetByState(string state)
        => _data.Where(t => t.SubjectState != null && t.SubjectState.Equals(state, StringComparison.OrdinalIgnoreCase));

    public string? GetLender(string subjectAddress)
        => GetByPropertyAddress(subjectAddress)?.LenderName;

    public decimal? GetLTV(string subjectAddress)
        => GetByPropertyAddress(subjectAddress)?.LTV;

    public string? GetSubjectAddressById(string realTransId)
        => _data.FirstOrDefault(t => t.RealTransID == realTransId)?.SubjectAddress;

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

    public IEnumerable<RealTransaction> GetByTitleCompany(string titleCompany)
        => _data.Where(t => t.TitleCompany != null &&
                            t.TitleCompany.Equals(titleCompany, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetTransactionsByAgentsAndYears(
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

    public IEnumerable<RealTransaction> GetTransactionsByDateRange(DateTime? from = null, DateTime? to = null)
    {
        var data = _data.AsEnumerable();
        if (from.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value >= from.Value);
        if (to.HasValue)
            data = data.Where(t => t.DateAdded.HasValue && t.DateAdded.Value <= to.Value);
        return data;
    }

    public decimal GetAgent1099(string agent, int year)
        => _data
            .Where(t => t.AgentName != null &&
                        t.AgentName.Equals(agent, StringComparison.OrdinalIgnoreCase) &&
                        t.DateAdded.HasValue &&
                        t.DateAdded.Value.Year == year)
            .Sum(t => t.KamBrokerFee ?? 0m);

    public IEnumerable<RealTransaction> GetByEscrowCompany(string escrowCompany)
        => _data.Where(t => t.EscrowCompany != null &&
                            t.EscrowCompany.Equals(escrowCompany, StringComparison.OrdinalIgnoreCase));

    public (int totalTransactions, decimal avgAmount, decimal maxAmount, decimal minAmount) GetLenderStats(string lender)
    {
        var transactions = _data.Where(t => t.LenderName != null &&
                                            t.LenderName.Equals(lender, StringComparison.OrdinalIgnoreCase) &&
                                            t.RealAmount.HasValue).ToList();
        if (!transactions.Any())
            return (0, 0, 0, 0);

        var amounts = transactions.Select(t => t.RealAmount!.Value);
        return (transactions.Count, amounts.Average(), amounts.Max(), amounts.Min());
    }

    public IEnumerable<RealTransaction> GetByTransType(string transType)
    => _data.Where(t => t.TransType != null &&
                        t.TransType.Equals(transType, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetByPartyPresented(string partyPresented)
        => _data.Where(t => t.PartyPresented != null &&
                            t.PartyPresented.Equals(partyPresented, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetByClientType(string clientType)
        => _data.Where(t => t.ClientType != null &&
                            t.ClientType.Equals(clientType, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetByFinanceInfo(string financeInfo)
        => _data.Where(t => t.FinanceInfo != null &&
                            t.FinanceInfo.Equals(financeInfo, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetByPriceRange(decimal minPrice, decimal maxPrice)
        => _data.Where(t => t.Price.HasValue && t.Price.Value >= minPrice && t.Price.Value <= maxPrice);

    public IEnumerable<RealTransaction> GetByCARForms(int forms)
        => _data.Where(t => t.CARForms.HasValue && t.CARForms.Value == forms);

    public IEnumerable<RealTransaction> GetByNMLSNumber(string nmlsNumber)
        => _data.Where(t => t.NMLSNumber != null &&
                            t.NMLSNumber.Equals(nmlsNumber, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetWithHomeInspectionDone(bool done)
        => _data.Where(t => t.HomeInspectionDone != null &&
                            ((done && t.HomeInspectionDone.Equals("Yes", StringComparison.OrdinalIgnoreCase)) ||
                             (!done && t.HomeInspectionDone.Equals("No", StringComparison.OrdinalIgnoreCase))));

    public IEnumerable<RealTransaction> GetByHomeInspectionName(string inspectorName)
        => _data.Where(t => t.HomeInspectionName != null &&
                            t.HomeInspectionName.Contains(inspectorName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetWithPestInspectionDone(bool done)
        => _data.Where(t => t.PestInspectionDone != null &&
                            ((done && t.PestInspectionDone.Equals("Yes", StringComparison.OrdinalIgnoreCase)) ||
                             (!done && t.PestInspectionDone.Equals("No", StringComparison.OrdinalIgnoreCase))));

    public IEnumerable<RealTransaction> GetByPestInspectionName(string inspectorName)
        => _data.Where(t => t.PestInspectionName != null &&
                            t.PestInspectionName.Contains(inspectorName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetByTCFlag(string flag)
        => _data.Where(t => t.TCFlag != null &&
                            t.TCFlag.Equals(flag, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetByTCNumber(int tcNumber)
        => _data.Where(t => t.TC.HasValue && t.TC.Value == tcNumber);

    public IEnumerable<RealTransaction> GetByTCFeesRange(decimal minFee, decimal maxFee)
        => _data.Where(t => t.TCFees.HasValue && t.TCFees.Value >= minFee && t.TCFees.Value <= maxFee);

    public IEnumerable<RealTransaction> GetByPayableTo(string payableTo)
        => _data.Where(t => t.PayableTo != null &&
                            t.PayableTo.Equals(payableTo, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<RealTransaction> GetByRoutingNumber(string routingNumber)
        => _data.Where(t => t.RoutingNumber != null &&
                            t.RoutingNumber.Equals(routingNumber, StringComparison.OrdinalIgnoreCase));

    public string? GetTransType(string subjectAddress)
    => _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                 t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase))
            ?.TransType;

    public string? GetPartyPresented(string subjectAddress)
        => _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                     t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase))
                ?.PartyPresented;

    public decimal? GetPrice(string subjectAddress)
        => _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                     t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase))
                ?.Price;

    public string? GetClientType(string subjectAddress)
        => _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                     t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase))
                ?.ClientType;

    public string? GetFinanceInfo(string subjectAddress)
        => _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                     t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase))
                ?.FinanceInfo;

    public int? GetCARForms(string subjectAddress)
        => _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                     t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase))
                ?.CARForms;

    public string? GetNMLSNumber(string subjectAddress)
        => _data.FirstOrDefault(t => t.SubjectAddress != null &&
                                     t.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase))
                ?.NMLSNumber;

    public (decimal min, decimal max, decimal avg) GetPriceStats()
    {
        var prices = _data.Where(t => t.Price.HasValue).Select(t => t.Price!.Value);
        return (prices.Min(), prices.Max(), prices.Average());
    }

    public (decimal min, decimal max, decimal avg) GetTCFeesStats()
    {
        var fees = _data.Where(t => t.TCFees.HasValue).Select(t => t.TCFees!.Value);
        return (fees.Min(), fees.Max(), fees.Average());
    }

    public (decimal min, decimal max, decimal avg) GetRealAmountStats()
    {
        var amounts = _data.Where(t => t.RealAmount.HasValue).Select(t => t.RealAmount!.Value);
        return (amounts.Min(), amounts.Max(), amounts.Average());
    }

    public HomeInspectionInfo? GetHomeInspectionInfo(string subjectAddress)
    {
        var t = _data.FirstOrDefault(x => x.SubjectAddress != null &&
                                          x.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase));
        if (t == null) return null;

        return new HomeInspectionInfo
        {
            Name = t.HomeInspectionName,
            Done = t.HomeInspectionDone,
            Phone = t.HomeInspectionPhone,
            Email = t.HomeInspectionEmail,
            Notes = t.HomeInspectionNotes
        };
    }

    public PestInspectionInfo? GetPestInspectionInfo(string subjectAddress)
    {
        var t = _data.FirstOrDefault(x => x.SubjectAddress != null &&
                                          x.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase));
        if (t == null) return null;

        return new PestInspectionInfo
        {
            Name = t.PestInspectionName,
            Done = t.PestInspectionDone,
            Phone = t.PestInspectionPhone,
            Email = t.PestInspectionEmail,
            Notes = t.PestInspectionNotes
        };
    }

    public TCInfo? GetTCInfo(string subjectAddress)
    {
        var t = _data.FirstOrDefault(x => x.SubjectAddress != null &&
                                          x.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase));
        if (t == null) return null;

        return new TCInfo
        {
            Flag = t.TCFlag,
            TC = t.TC,
            Fees = t.TCFees
        };
    }

    public PaymentInfo? GetPaymentInfo(string subjectAddress)
    {
        var t = _data.FirstOrDefault(x => x.SubjectAddress != null &&
                                          x.SubjectAddress.Equals(subjectAddress, StringComparison.OrdinalIgnoreCase));
        if (t == null) return null;

        return new PaymentInfo
        {
            PayableTo = t.PayableTo,
            RoutingNumber = t.RoutingNumber
        };
    }



}




