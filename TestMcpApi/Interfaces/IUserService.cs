using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public interface IUserService
    {
        string ErrorLoadData { get; }

        Task<List<Users>> GetUsers();
        Task<Users?> GetUserById(int userId);

        IEnumerable<Users> GetByName(string name);
        Users? GetByEmail(string email);
        IEnumerable<Users> GetByCity(string city);
        IEnumerable<Users> GetByState(string state);

        string? GetUserPhone(int userId);
        string? GetUserAddress(int userId);
        string? GetBusinessName(int userId);

        IEnumerable<object> GetTopCities(int top = 10);
        IEnumerable<object> GetTopBusinesses(int top = 10);

        int GetTotalUsersByCity(string city);
        int GetTotalUsersByState(string state);

        IEnumerable<string> GetAllBusinessNames(bool sortByName = true, bool descending = false);
        IEnumerable<string> GetAllLicensingEntities();
        IEnumerable<Users> GetByLicensingEntity(string entity);

        IEnumerable<Users> GetUsersByDateRange(DateTime? from = null, DateTime? to = null);
        IEnumerable<Users> GetByNMLSID(string nmlsId);

        IEnumerable<Users> GetByCompany(string companyName);

        (int totalUsers, int withNMLS, int withLicense) GetUserStats();
    }
}