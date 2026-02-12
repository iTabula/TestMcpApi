using KamHttp.Services;
using KamInfrastructure.Models;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace KamMobile.Services;

public class AuthenticationService
{
    private readonly IUserService _userService;
    private readonly ILogger<AuthenticationService> _logger;
    
    private bool _isAuthenticated = false;
    private LoginResponse? _currentLoginResponse = null;
    
    public bool IsAuthenticated => _isAuthenticated;
    public string? AccessToken => _currentLoginResponse?.jwtToken;
    public string? RefreshToken => _currentLoginResponse?.refreshToken;
    public User? CurrentUser => _currentLoginResponse?.user;
    public string? Role => _currentLoginResponse?.role;
    public List<string> FeatureKeys => _currentLoginResponse?.featureKeys ?? new List<string>();
    public string? UserId => CurrentUser?.UserId.ToString();

    public AuthenticationService(IUserService userService, ILogger<AuthenticationService> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Login attempt with empty credentials");
                return false;
            }

            _logger.LogInformation("Attempting login for user: {Username}", username);
            
            // Call the API to validate login
            var response = await _userService.LoginUser(username, password);

            if (response == null || !response.status || response.user == null || string.IsNullOrEmpty(response.jwtToken))
            {
                _logger.LogWarning("Login failed for user: {Username}", username);
                _isAuthenticated = false;
                _currentLoginResponse = null;
                return false;
            }

            // Store the response
            _currentLoginResponse = response;
            _isAuthenticated = true;

            // Persist credentials securely
            await SecureStorage.SetAsync("access_token", response.jwtToken);
            await SecureStorage.SetAsync("refresh_token", response.refreshToken);
            await SecureStorage.SetAsync("user_id", response.user.UserId.ToString());
            await SecureStorage.SetAsync("username", username);
            await SecureStorage.SetAsync("role", response.role ?? "");
            await SecureStorage.SetAsync("user_email", response.user.Email ?? "");
            await SecureStorage.SetAsync("user_name", $"{response.user.FirstName} {response.user.LastName}");

            // Store feature keys
            if (response.featureKeys != null && response.featureKeys.Any())
            {
                await SecureStorage.SetAsync("feature_keys", string.Join(",", response.featureKeys));
            }

            _logger.LogInformation("Login successful for user: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during login for user: {Username}", username);
            _isAuthenticated = false;
            _currentLoginResponse = null;
            return false;
        }
    }

    public async Task<bool> RestoreSessionAsync()
    {
        try
        {
            var accessToken = await SecureStorage.GetAsync("access_token");
            var userId = await SecureStorage.GetAsync("user_id");
            var username = await SecureStorage.GetAsync("username");

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(userId))
            {
                // Reconstruct the login response from stored data
                _currentLoginResponse = new LoginResponse
                {
                    jwtToken = accessToken,
                    refreshToken = await SecureStorage.GetAsync("refresh_token") ?? "",
                    role = await SecureStorage.GetAsync("role") ?? "",
                    user = new User
                    {
                        UserId = int.Parse(userId),
                        Email = await SecureStorage.GetAsync("user_email") ?? "",
                        FirstName = (await SecureStorage.GetAsync("user_name"))?.Split(' ')[0] ?? "",
                        LastName = (await SecureStorage.GetAsync("user_name"))?.Split(' ').LastOrDefault() ?? ""
                    },
                    featureKeys = (await SecureStorage.GetAsync("feature_keys"))?.Split(',').ToList() ?? new List<string>(),
                    status = true
                };

                _isAuthenticated = true;
                _logger.LogInformation("Session restored for user: {Username}", username);
                return true;
            }

            _isAuthenticated = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception restoring session");
            _isAuthenticated = false;
            _currentLoginResponse = null;
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            _isAuthenticated = false;
            _currentLoginResponse = null;

            // Clear all stored credentials
            SecureStorage.Remove("access_token");
            SecureStorage.Remove("refresh_token");
            SecureStorage.Remove("user_id");
            SecureStorage.Remove("username");
            SecureStorage.Remove("role");
            SecureStorage.Remove("user_email");
            SecureStorage.Remove("user_name");
            SecureStorage.Remove("feature_keys");

            _logger.LogInformation("User logged out successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during logout");
        }
    }
}