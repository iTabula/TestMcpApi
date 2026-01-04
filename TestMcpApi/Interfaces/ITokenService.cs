using Microsoft.AspNetCore.Mvc;

namespace TestMcpApi.Interfaces
{
    public interface ITokenService
    {

        string GenerateJwtToken(string username, string userId, List<string> UserFeatures);
        public string GenerateRefreshToken();
        public Task<IActionResult> RefreshToken(string JwtToken, string RefreshToken);

    }
}
