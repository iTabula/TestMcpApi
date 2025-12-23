using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public interface ILoanTransactionService
    {
        string ErrorLoadCsv { get; }

        Task<List<LoanTransaction>> GetLoanTransactions();
        Task<LoanTransaction?> GetLoanTransactionById(string loanTransID);

        IEnumerable<LoanTransaction> GetByAgent(string agent);
        LoanTransaction? GetByLoanNumber(string loanId);
        IEnumerable<LoanTransaction> GetByState(string state);

        string? GetLender(string loanId);
        decimal? GetLTV(string loanId);
        string? GetSubjectAddress(string loanId);

        string GetMostPopularZip();
        IEnumerable<object> GetTopCities(int top = 10);
        IEnumerable<object> GetTopAgents(int top = 10);

        int GetTotalTransactionsByAgent(string agent);
        int GetTotalTransactionsByLender(string lender);

        IEnumerable<string> GetAllAgents(bool sortByName = true, bool descending = false);
        IEnumerable<string> GetAllTitleCompanies();
        IEnumerable<LoanTransaction> GetByTitleCompany(string titleCompany);

        IEnumerable<LoanTransaction> GetLoansByAgentsAndYears(IEnumerable<string>? agents = null, IEnumerable<int>? years = null);
        IEnumerable<LoanTransaction> GetLoansByDateRange(DateTime? from = null, DateTime? to = null);

        decimal GetAgent1099(string agent, int year);

        IEnumerable<LoanTransaction> GetByEscrowCompany(string escrowCompany);

        (int totalLoans, decimal avgAmount, decimal maxAmount, decimal minAmount) GetLenderStats(string lender);
    }
}
