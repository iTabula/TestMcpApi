using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public interface ILenderService
    {
        string ErrorLoadCsv { get; }

        Task<List<Lender>> GetLenders();

        Task<Lender?> GetLenderById(int lenderId);

        IEnumerable<Lender> GetByName(string name);

        IEnumerable<Lender> GetByCompany(string companyName);

        IEnumerable<Lender> GetVAApproved();

        IEnumerable<Lender> GetActiveLenders();
        int GetTotalLenders();
        decimal? GetAverageMinComp();
        decimal? GetAverageMaxComp();
        IEnumerable<string> GetAllCompanies(bool sortByName = true, bool descending = false);
        IEnumerable<string> GetAllContacts();
        IEnumerable<Lender> GetByCity(string city);
        IEnumerable<Lender> GetByState(string state);
        IEnumerable<Lender> GetByDateRange(DateTime? from = null, DateTime? to = null);
        IEnumerable<Lender> GetByWebsite(string website);
    }
}
