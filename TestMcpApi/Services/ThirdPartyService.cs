using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TestMcpApi.Models;

namespace TestMcpApi.Services;

public class ThirdPartyService : IThirdPartyService
{
    private readonly List<ThirdParty> _data = new();
    private string _errorLoadCsv = string.Empty;

    public string ErrorLoadCsv => _errorLoadCsv;

    public ThirdPartyService()
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
            const string sql = "SELECT * FROM ThirdPartiesView ORDER BY 1 DESC";

            var list = db.Query<ThirdParty>(sql).AsList();
            _data.AddRange(list);

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"Error loading ThirdParties from database: {ex.Message}";
        }
    }

    // Base Access
    public Task<List<ThirdParty>> GetThirdParties()
        => Task.FromResult(_data);

    public Task<ThirdParty?> GetThirdPartyById(int thirdPartyId)
        => Task.FromResult(_data.FirstOrDefault(t => t.ThirdPartiesID == thirdPartyId));

    // Lookups / Filters
    public IEnumerable<ThirdParty> GetByName(string name)
        => _data.Where(t =>
            !string.IsNullOrEmpty(t.Name) &&
            t.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ThirdParty> GetByPurpose(string purpose)
        => _data.Where(t =>
            !string.IsNullOrEmpty(t.Purpose) &&
            t.Purpose.Equals(purpose, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ThirdParty> GetByWebsite(string website)
        => _data.Where(t =>
            !string.IsNullOrEmpty(t.Website) &&
            t.Website.Contains(website, StringComparison.OrdinalIgnoreCase));

    public ThirdParty? GetByUsername(string username)
        => _data.FirstOrDefault(t =>
            !string.IsNullOrEmpty(t.Username) &&
            t.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    // Flags / Notes
    public IEnumerable<ThirdParty> GetAdminViewOnly()
        => _data.Where(t => t.AdminViewOnly == "Yes");

    public IEnumerable<ThirdParty> GetWithNotes()
        => _data.Where(t =>
            !string.IsNullOrEmpty(t.Notes));

    // Aggregations / Stats
    public int GetTotalThirdParties()
        => _data.Count;

    public IEnumerable<string> GetAllPurposes()
        => _data.Select(t => t.Purpose)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase);

    public string GetMostPopularPurpose()
        => _data.Where(t => !string.IsNullOrEmpty(t.Purpose))
                .GroupBy(t => t.Purpose, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "N/A";

    // Date Filters
    public IEnumerable<ThirdParty> GetByDateRange(DateTime? from = null, DateTime? to = null)
    {
        var data = _data.AsEnumerable();

        if (from.HasValue)
            data = data.Where(t => t.LastUpdatedOn.HasValue && t.LastUpdatedOn.Value >= from.Value);

        if (to.HasValue)
            data = data.Where(t => t.LastUpdatedOn.HasValue && t.LastUpdatedOn.Value <= to.Value);

        return data;
    }
}
