using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public interface IRealTransactionService
    {
        string ErrorLoadCsv { get; }

        Task<List<RealTransaction>> GetRealTransactions();
        Task<RealTransaction?> GetRealTransactionById(string realTransID);

        IEnumerable<RealTransaction> GetByAgent(string agent);
        RealTransaction? GetByPropertyAddress(string subjectAddress);
        IEnumerable<RealTransaction> GetByState(string state);

        string? GetLender(string subjectAddress);
        decimal? GetLTV(string subjectAddress);
        string? GetSubjectAddressById(string realTransId);

        string GetMostPopularZip();
        IEnumerable<object> GetTopCities(int top = 10);
        IEnumerable<object> GetTopAgents(int top = 10);

        int GetTotalTransactionsByAgent(string agent);
        int GetTotalTransactionsByLender(string lender);

        IEnumerable<string> GetAllAgents(bool sortByName = true, bool descending = false);
        IEnumerable<string> GetAllTitleCompanies();
        IEnumerable<RealTransaction> GetByTitleCompany(string titleCompany);

        IEnumerable<RealTransaction> GetTransactionsByAgentsAndYears(IEnumerable<string>? agents = null, IEnumerable<int>? years = null);
        IEnumerable<RealTransaction> GetTransactionsByDateRange(DateTime? from = null, DateTime? to = null);

        decimal GetAgent1099(string agent, int year);

        IEnumerable<RealTransaction> GetByEscrowCompany(string escrowCompany);

        (int totalTransactions, decimal avgAmount, decimal maxAmount, decimal minAmount) GetLenderStats(string lender);

        IEnumerable<RealTransaction> GetByTransType(string transType);
        IEnumerable<RealTransaction> GetByPartyPresented(string partyPresented);
        IEnumerable<RealTransaction> GetByClientType(string clientType);
        IEnumerable<RealTransaction> GetByFinanceInfo(string financeInfo);
        IEnumerable<RealTransaction> GetByPriceRange(decimal minPrice, decimal maxPrice);
        IEnumerable<RealTransaction> GetByCARForms(int forms);
        IEnumerable<RealTransaction> GetByNMLSNumber(string nmlsNumber);
        IEnumerable<RealTransaction> GetWithHomeInspectionDone(bool done);
        IEnumerable<RealTransaction> GetByHomeInspectionName(string inspectorName);
        IEnumerable<RealTransaction> GetWithPestInspectionDone(bool done);
        IEnumerable<RealTransaction> GetByPestInspectionName(string inspectorName);
        IEnumerable<RealTransaction> GetByTCFlag(string flag);
        IEnumerable<RealTransaction> GetByTCNumber(int tcNumber);
        IEnumerable<RealTransaction> GetByTCFeesRange(decimal minFee, decimal maxFee);
        IEnumerable<RealTransaction> GetByPayableTo(string payableTo);
        IEnumerable<RealTransaction> GetByRoutingNumber(string routingNumber);

        string? GetTransType(string subjectAddress);
        string? GetPartyPresented(string subjectAddress);
        decimal? GetPrice(string subjectAddress);
        string? GetClientType(string subjectAddress);
        string? GetFinanceInfo(string subjectAddress);
        int? GetCARForms(string subjectAddress);
        string? GetNMLSNumber(string subjectAddress);

        (decimal min, decimal max, decimal avg) GetPriceStats();
        (decimal min, decimal max, decimal avg) GetTCFeesStats();
        (decimal min, decimal max, decimal avg) GetRealAmountStats();

        HomeInspectionInfo? GetHomeInspectionInfo(string subjectAddress);
        PestInspectionInfo? GetPestInspectionInfo(string subjectAddress);
        TCInfo? GetTCInfo(string subjectAddress);
        PaymentInfo? GetPaymentInfo(string subjectAddress);
    }
}
