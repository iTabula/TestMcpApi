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
    private readonly List<User> _data = new();
    private string _errorLoadCsv = string.Empty;

    public string ErrorLoadCsv => _errorLoadCsv;

    public UserService()
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
            const string sql = "SELECT * FROM Users ORDER BY 1 DESC";
            var list = db.Query<User>(sql).AsList();
            _data.AddRange(list);

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"Error loading data from database: {ex.Message}";
        }
    }

    public Task<List<User>> GetUsers()
        => Task.FromResult(_data);

    public Task<User?> GetUserById(int userId)
        => Task.FromResult(_data.FirstOrDefault(l => l.UserId == userId));

    public Task<User?> GetUserByEmail(string email)
        => Task.FromResult(_data.FirstOrDefault(l => l.Email == email));

}




