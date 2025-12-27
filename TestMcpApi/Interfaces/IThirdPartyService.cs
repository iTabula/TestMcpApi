using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public interface IThirdPartyService
    {
        string ErrorLoadCsv { get; }
        Task<List<ThirdParty>> GetThirdParties();
        Task<ThirdParty?> GetThirdPartyById(int thirdPartyId);

        IEnumerable<ThirdParty> GetByName(string name);
        IEnumerable<ThirdParty> GetByPurpose(string purpose);
        IEnumerable<ThirdParty> GetByWebsite(string website);
        ThirdParty? GetByUsername(string username);

        IEnumerable<ThirdParty> GetAdminViewOnly();
        IEnumerable<ThirdParty> GetWithNotes();

        int GetTotalThirdParties();
        IEnumerable<string> GetAllPurposes();
        string GetMostPopularPurpose();
        IEnumerable<ThirdParty> GetByDateRange(DateTime? from = null, DateTime? to = null);
    }
}
