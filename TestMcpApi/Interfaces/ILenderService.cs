using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public interface ILenderService
    {
        string ErrorLoadCsv { get; }

        Task<List<Lenders>> GetLenders();

        Task<Lenders?> GetLenderById(int lenderId);

        IEnumerable<Lenders> GetByName(string name);

        IEnumerable<Lenders> GetByCompany(string companyName);

        IEnumerable<Lenders> GetVAApproved();

        IEnumerable<Lenders> GetActiveLenders();
        int GetTotalLenders();
        decimal? GetAverageMinComp();
        decimal? GetAverageMaxComp();
        IEnumerable<string> GetAllCompanies(bool sortByName = true, bool descending = false);
        IEnumerable<string> GetAllContacts();
        IEnumerable<Lenders> GetByCity(string city);
        IEnumerable<Lenders> GetByState(string state);
        IEnumerable<Lenders> GetByDateRange(DateTime? from = null, DateTime? to = null);
        IEnumerable<Lenders> GetByWebsite(string website);
    }
}
