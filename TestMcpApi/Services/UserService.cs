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

public class UserService : IUserService
{
    private readonly List<Users> _data = new();
    private string _errorLoadData = string.Empty;

    public string ErrorLoadData => _errorLoadData;

    public UserService()
    {
        _errorLoadData = LoadData();
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
            const string sql = "SELECT * FROM Users ORDER BY DateAdded DESC";
            var list = db.Query<Users>(sql).AsList();
            _data.AddRange(list);

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"Error loading data from database: {ex.Message}";
        }
    }

    public Task<List<Users>> GetUsers()
    {
        return Task.FromResult(_data);
    }

    public Task<Users?> GetUserById(int userId)
    {
        var user = _data.FirstOrDefault(u => u.AddedBy == userId);
        return Task.FromResult(user);
    }

    public IEnumerable<Users> GetByName(string name)
        => _data.Where(u => (u.FullName != null && u.FullName.Contains(name, StringComparison.OrdinalIgnoreCase)) ||
                            (u.FirstName != null && u.FirstName.Contains(name, StringComparison.OrdinalIgnoreCase)) ||
                            (u.LastName != null && u.LastName.Contains(name, StringComparison.OrdinalIgnoreCase)));

    public Users? GetByEmail(string email)
        => _data.FirstOrDefault(u => (u.Email != null && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) ||
                                     (u.Email2 != null && u.Email2.Equals(email, StringComparison.OrdinalIgnoreCase)));
    public Task<Users?> GetByPhone(string phoneNumber) 
        => Task.FromResult(_data.FirstOrDefault(u => (u.Phone != null && u.Phone.Equals(phoneNumber, StringComparison.OrdinalIgnoreCase))));

    public IEnumerable<Users> GetByCity(string city)
        => _data.Where(u => (u.City != null && u.City.Equals(city, StringComparison.OrdinalIgnoreCase)) ||
                            (u.BusinessCity != null && u.BusinessCity.Equals(city, StringComparison.OrdinalIgnoreCase)));

    public IEnumerable<Users> GetByState(string state)
        => _data.Where(u => (u.CountryID != null && u.CountryID.Equals(state, StringComparison.OrdinalIgnoreCase)) ||
                            (u.BusinessState != null && u.BusinessState.Equals(state, StringComparison.OrdinalIgnoreCase)));

    public string? GetUserPhone(int userId)
        => GetUserById(userId).Result?.Phone;

    public string? GetUserAddress(int userId)
        => GetUserById(userId).Result?.Address;

    public string? GetBusinessName(int userId)
        => GetUserById(userId).Result?.BusinessName;

    public IEnumerable<object> GetTopCities(int top = 10)
        => _data.Where(u => !string.IsNullOrEmpty(u.City))
                .GroupBy(u => u.City)
                .OrderByDescending(g => g.Count())
                .Take(top)
                .Select(g => new { City = g.Key, Count = g.Count() });

    public IEnumerable<object> GetTopBusinesses(int top = 10)
        => _data.Where(u => !string.IsNullOrEmpty(u.BusinessName))
                .GroupBy(u => u.BusinessName)
                .OrderByDescending(g => g.Count())
                .Take(top)
                .Select(g => new { Business = g.Key, Users = g.Count() });

    public int GetTotalUsersByCity(string city)
        => _data.Count(u => u.City != null && u.City.Equals(city, StringComparison.OrdinalIgnoreCase));

    public int GetTotalUsersByState(string state)
        => _data.Count(u => u.CountryID != null && u.CountryID.Equals(state, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> GetAllBusinessNames(bool sortByName = true, bool descending = false)
    {
        var businesses = _data.Select(u => u.BusinessName)
                              .Where(b => !string.IsNullOrEmpty(b))
                              .Distinct();

        if (!sortByName)
            businesses = businesses.OrderBy(b => _data.Count(u => u.BusinessName == b));

        return descending ? businesses.Reverse() : businesses;
    }

    public IEnumerable<string> GetAllLicensingEntities()
        => _data.Select(u => u.LicensingEntity)
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct()!;

    public IEnumerable<Users> GetByLicensingEntity(string entity)
        => _data.Where(u => u.LicensingEntity != null &&
                            u.LicensingEntity.Equals(entity, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Users> GetUsersByDateRange(DateTime? from = null, DateTime? to = null)
    {
        var data = _data.AsEnumerable();
        if (from.HasValue)
            data = data.Where(u => u.DateAdded >= from.Value);
        if (to.HasValue)
            data = data.Where(u => u.DateAdded <= to.Value);
        return data;
    }

    public IEnumerable<Users> GetByNMLSID(string nmlsId)
        => _data.Where(u => u.NMLSID != null &&
                            u.NMLSID.Equals(nmlsId, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Users> GetByCompany(string companyName)
        => _data.Where(u => u.CompanyName != null &&
                            u.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));

    public (int totalUsers, int withNMLS, int withLicense) GetUserStats()
    {
        var total = _data.Count;
        var withNMLS = _data.Count(u => !string.IsNullOrEmpty(u.NMLSID));
        var withLicense = _data.Count(u => !string.IsNullOrEmpty(u.LicenseNumber));
        return (total, withNMLS, withLicense);
    }
}