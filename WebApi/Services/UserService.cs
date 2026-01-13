using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services;

public class UserService : IUserService
{
    private readonly IFactoryHttpClient _factoryHttpClient;
    private readonly IConfiguration _configuration;
    private readonly string _WebApiName = string.Empty;
    public UserService(IConfiguration configuration, IFactoryHttpClient factoryHttpClient)
    {
        _factoryHttpClient = factoryHttpClient;
        _configuration = configuration;
        _WebApiName = "MyWebApi";
    }

    public async Task<List<UserPartial>> GetUsers(string AccessToken)
    {
        List<UserPartial> data = await _factoryHttpClient.GetRequest<List<UserPartial>>(_WebApiName,
            $"api/Users", null!, AccessToken);

        return data;
    }
    public async Task<User> GetUserByEmail(string AccessToken, string Email)
    {
        User data = await _factoryHttpClient.GetRequest<User>(_WebApiName,
            $"api/Users/Email/{Email}", null!, AccessToken);
        return data;
    }

    public async Task<List<User>> GetUsersBelongToGroup(string AccessToken, string groupId)
    {

        List<User> result = await _factoryHttpClient.GetRequest<List<User>>(_WebApiName,
            $"api/Users/Group/{groupId}", null!, AccessToken);

        return result;
    }

    public async Task<UserPartial> GetUser(string AccessToken, string UserId)
    {

        UserPartial result = await _factoryHttpClient.GetRequest<UserPartial>(_WebApiName,
            $"api/Users/{UserId}", null!, AccessToken);

        return result;
    }

    public async Task<User> UpdateUser(string AccessToken, User user, int UserId)
    {
        HttpContent httpContent = _factoryHttpClient.SetHttpContent(user);

        User result = await _factoryHttpClient.PostRequest<User>(_WebApiName,
            $"api/Users/Update/{UserId}", httpContent!, AccessToken);

        return result;
    }


    public async Task<User> CreateUser(string AccessToken, User user)
    {
        HttpContent httpContent = _factoryHttpClient.SetHttpContent(user);

        User result = await _factoryHttpClient.PostRequest<User>(_WebApiName,
            $"api/Users/Create", httpContent!, AccessToken);

        return result;
    }

    public async Task<LoginResponse> LoginUser(string UserName, string Password)
    {
        LoginRequest loginRequest = new LoginRequest()
        {
            UserName = UserName,
            Password = Password
        };
        HttpContent httpContent = _factoryHttpClient.SetHttpContent(loginRequest);
        LoginResponse data = await _factoryHttpClient.PostRequest<LoginResponse>(_WebApiName,
            $"api/Users/login", httpContent, "");

        return data;
    }

}




