using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebApi.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;

        public TokenService(IConfiguration configuration, IUnitOfWork unitOfWork)
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
        }

        public string GenerateJwtToken(string username, string userId)
        {
            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.NameIdentifier, userId),
                };


            //Add all the features
            //claims.AddRange(UserFeatures.Select(x => new Claim(x, "true")).ToList());

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtOptions:SigningKey"]!));
            var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var tokeOptions = new JwtSecurityToken(
                issuer: _configuration["JwtOptions:Issuer"]!,
                audience: _configuration["JwtOptions:Issuer"]!,
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToInt32(_configuration["JwtOptions:ExpirationMinutes"]!)),
                signingCredentials: signinCredentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(tokeOptions);
            return tokenString;
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }


        public Task<IActionResult> RefreshToken(string JwtToken, string RefreshToken)
        {

            if (string.IsNullOrEmpty(JwtToken) || string.IsNullOrEmpty(RefreshToken))
            {
                throw new SecurityTokenException("Invalid Request");
                //return BadRequest("Invalid client request");
            }

            var principal = GetPrincipalFromExpiredToken(JwtToken);

            if (principal == null)
            {
                throw new SecurityTokenException("Invalid Token");
                //return BadRequest("Invalid access token or refresh token");
            }

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtOptions:SigningKey"]!));
            var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var tokeOptions = new JwtSecurityToken(
                issuer: _configuration["JwtOptions:Issuer"]!,
                audience: _configuration["JwtOptions:Issuer"]!,
                claims: principal!.Claims,
                expires: DateTime.Now.AddMinutes(Convert.ToInt32(_configuration["JwtOptions:ExpirationMinutes"]!)),
                signingCredentials: signinCredentials
            );

            var NewJwtToken = new JwtSecurityTokenHandler().WriteToken(tokeOptions);
            var NewRefreshToken = GenerateRefreshToken();

            return Task.FromResult<IActionResult>(new ObjectResult(new
            {
                AccessToken = NewJwtToken,
                RefreshToken = NewRefreshToken
            }));
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
        {
            TokenValidationParameters tokenValidationParameters = new TokenValidationParameters
            {
                //ValidateAudience = false,
                //ValidateIssuer = false,
                //ValidateIssuerSigningKey = true,
                //IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtOptions:SigningKey"]!)),
                //ValidateLifetime = false,
                //ClockSkew = TimeSpan.Zero
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true, // This should be true during normal validation
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtOptions:SigningKey"]!)),
                ValidIssuer = _configuration["JwtOptions:Issuer"],
                ValidAudience = _configuration["JwtOptions:Issuer"],
                ClockSkew = TimeSpan.Zero // Optional: set to zero to prevent clock skew issues
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid Token");

            return principal;

        }
    }
}
