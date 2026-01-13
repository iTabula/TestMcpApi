using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace WebApi.Interfaces
{
    public interface ITokenService
    {

        string GenerateJwtToken(string username, string userId);
        public string GenerateRefreshToken();
        public Task<IActionResult> RefreshToken(string JwtToken, string RefreshToken);

    }
}
