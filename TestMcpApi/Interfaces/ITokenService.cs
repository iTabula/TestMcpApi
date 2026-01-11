using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace TestMcpApi.Interfaces
{
    public interface ITokenService
    {

        string GenerateJwtToken(string username, string userId, List<string> UserFeatures);
        public string GenerateRefreshToken();
        public Task<IActionResult> RefreshToken(string JwtToken, string RefreshToken);

    }
}
