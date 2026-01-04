using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public interface IUserService
    {
        string ErrorLoadCsv { get; }

        Task<List<User>> GetUsers(string AccessToken);
        Task<User> GetUserById(string AccessToken, string UserId);
        Task<User> GetUserByEmail(string AccessToken, string Email);

        //Task<LoginResponse> LoginUser(string UserName, string Password);
        //Task<User> CreateUser(string AccessToken, User user);
        //Task<User> UpdateUser(string AccessToken, User user, int UserId);
        //Task<List<User>> GetUsersBelongToGroup(string AccessToken, string groupId);
    }
}
