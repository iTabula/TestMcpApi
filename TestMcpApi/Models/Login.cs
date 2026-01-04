namespace TestMcpApi.Models
{
    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    public class LoginResponse
    {
        public User User { get; set; } = null!;
        public string JwtToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public List<string> FeatureKeys { get; set; } = null!;
        public bool Status { get; set; } = false;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

    }
    public class MyJsonResult
    {
        public object? ContentType { get; set; }
        public object? SerializerSettings { get; set; }
        public object? StatusCode { get; set; }
        public ResultValue? Value { get; set; }
    }

    public class ResultValue
    {
        public bool success { get; set; }
        public string? message { get; set; }
    }
}
