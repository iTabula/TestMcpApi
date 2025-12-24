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

public class LenderService : ILenderService
{
    private readonly List<Lender> _data = new();
    private string _errorLoadCsv = string.Empty;

    public string ErrorLoadCsv => _errorLoadCsv;

    public LenderService()
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
            const string sql = "SELECT * FROM LendersView ORDER BY 1 DESC";
            var list = db.Query<Lender>(sql).AsList();
            _data.AddRange(list);

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"Error loading data from database: {ex.Message}";
        }
    }

    public Task<List<Lender>> GetLenders()
        => Task.FromResult(_data);

    public Task<Lender?> GetLenderById(int lenderId)
        => Task.FromResult(_data.FirstOrDefault(l => l.LenderID == lenderId));

    public IEnumerable<Lender> GetByCompany(string companyName)
        => _data.Where(l => !string.IsNullOrEmpty(l.CompanyName) &&
                            l.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Lender> GetByName(string name)
        => _data.Where(l => !string.IsNullOrEmpty(l.LenderContact) &&
                            l.LenderContact.Contains(name, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Lender> GetVAApproved()
        => _data.Where(l => l.VAApproved == "Yes");

    public IEnumerable<Lender> GetActiveLenders()
        => _data.Where(l => l.Status == "Active");

    public int GetTotalLenders() => _data.Count;

    public decimal? GetAverageMinComp()
    {
        var valid = _data
            .Select(l => decimal.TryParse(l.MinimumComp, out var val) ? val : (decimal?)null)
            .Where(v => v.HasValue)
            .ToList();
        return valid.Any() ? valid.Average() : null;
    }

    public decimal? GetAverageMaxComp()
    {
        var valid = _data
            .Select(l => decimal.TryParse(l.MaximumComp, out var val) ? val : (decimal?)null)
            .Where(v => v.HasValue)
            .ToList();
        return valid.Any() ? valid.Average() : null;
    }

    public IEnumerable<string> GetAllCompanies(bool sortByName = true, bool descending = false)
    {
        var companies = _data.Select(l => l.CompanyName)
                             .Where(c => !string.IsNullOrEmpty(c))
                             .Distinct();

        if (!sortByName)
            companies = companies.OrderBy(c => _data.Count(l => l.CompanyName == c));

        return descending ? companies.Reverse() : companies;
    }

    public IEnumerable<string> GetAllContacts()
        => _data.Select(l => l.LenderContact)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct();

    public IEnumerable<Lender> GetByCity(string city)
        => _data.Where(l => !string.IsNullOrEmpty(l.City) &&
                            l.City.Equals(city, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Lender> GetByState(string state)
        => _data.Where(l => !string.IsNullOrEmpty(l.State) &&
                            l.State.Equals(state, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Lender> GetByDateRange(DateTime? from = null, DateTime? to = null)
    {
        var data = _data.AsEnumerable();
        if (from.HasValue)
            data = data.Where(l => l.DateAdded >= from.Value);
        if (to.HasValue)
            data = data.Where(l => l.DateAdded <= to.Value);
        return data;
    }

    public IEnumerable<Lender> GetWithNotes()
        => _data.Where(l => !string.IsNullOrEmpty(l.Notes) || !string.IsNullOrEmpty(l.ProcessorNotes));

    public IEnumerable<Lender> GetByWebsite(string website)
        => _data.Where(l => !string.IsNullOrEmpty(l.Website) &&
                            l.Website.Contains(website, StringComparison.OrdinalIgnoreCase));
}




