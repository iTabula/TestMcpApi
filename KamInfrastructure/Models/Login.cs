namespace KamInfrastructure.Models
{
    public class LoginRequest
    {
        public string userName { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
    public class LoginResponse
    {
        public string id { get; set; }
        public User user { get; set; } = null!;
        public string jwtToken { get; set; } = string.Empty;
        public string refreshToken { get; set; } = string.Empty;
        public List<string> featureKeys { get; set; } = null!;
        public bool status { get; set; } = false;
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string role { get; set; } = string.Empty;

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
