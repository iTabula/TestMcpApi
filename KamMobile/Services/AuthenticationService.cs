namespace KamMobile.Services;

public class AuthenticationService
{
    // Dummy credentials - not hooked up to any API
    private const string DUMMY_USERNAME = "admin";
    private const string DUMMY_PASSWORD = "password";
    
    private bool _isAuthenticated = false;
    
    public bool IsAuthenticated => _isAuthenticated;
    
    public Task<bool> LoginAsync(string username, string password)
    {
        _isAuthenticated = username == DUMMY_USERNAME && password == DUMMY_PASSWORD;
        return Task.FromResult(_isAuthenticated);
    }
    
    public Task LogoutAsync()
    {
        _isAuthenticated = false;
        return Task.CompletedTask;
    }
}