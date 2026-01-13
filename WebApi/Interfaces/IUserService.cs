using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApi.Models;

namespace WebApi.Services
{
    public interface IUserService
    {
        Task<List<UserPartial>> GetUsers(string AccessToken);
        Task<List<User>> GetUsersBelongToGroup(string AccessToken, string groupId);
        Task<UserPartial> GetUser(string AccessToken, string UserId);
        Task<User> GetUserByEmail(string AccessToken, string Email);
        Task<LoginResponse> LoginUser(string UserName, string Password);
        Task<User> CreateUser(string AccessToken, User user);
        Task<User> UpdateUser(string AccessToken, User user, int UserId);
    }
}
