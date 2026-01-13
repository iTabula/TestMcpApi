using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Azure.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using System.Text.Json;
using WebApi.Services;
using WebApi.Models;

namespace KamWeb.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public IndexModel(ILogger<IndexModel> logger, IUserService userService, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IActionResult> OnGetAsync(string redirectUrl, string PartID)
        {

            //Check for Cookie, if exists, populate username and password
            //Response.Cookies.Delete("email_oc");
            //Response.Cookies.Delete("password_oc");
            //if (Request.Cookies.TryGetValue("email_oc", out var email))
            //{
            //    if (Request.Cookies.TryGetValue("password_oc", out var password))
            //    {
            //        if (string.IsNullOrWhiteSpace(email) == false && string.IsNullOrWhiteSpace(password) == false)
            //        {
            //            string result = await LoginAutomatically(email, password);

            //            //Get out if login failed
            //            if (!string.IsNullOrEmpty(result))
            //            {
            //                return Page();
            //            }

            //            if (string.IsNullOrEmpty(redirectUrl)) redirectUrl = "Main";

            //            if (redirectUrl.ToLower() == "userscan" || redirectUrl.ToLower() == "userpickup")
            //            {
            //                return Redirect($"/{redirectUrl}?PartID={PartID}");
            //            }
            //            else
            //            {
            //                return Redirect($"/{redirectUrl}");
            //            }
            //        }
            //    }
            //}
            return Page();
        }
        public async Task<IActionResult> OnPostLoginUserAsync(string UserName, string Password, bool RememberMe)
        {
            // Check if username or password is empty
            if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
            {
                return new JsonResult(new { success = false, message = "Empty username and/or password" });
            }

            // Call API to Validate Login
            //string PasswordHashed = KAM.Core.Helpers.Hashing.HashPassword(Password);

            string result = await LoginAutomatically(UserName, Password);

            if (!string.IsNullOrEmpty(result))
            {
                return new JsonResult(new { success = false, message = result });
            }

            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow.AddYears(10),
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None,
                IsEssential = true,
                Secure = true,
                HttpOnly = true // Email cannot be accessed by client-side scripts

            };
            Response.Cookies.Append("email_oc", UserName, cookieOptions);
            Response.Cookies.Append("password_oc", Password, cookieOptions);

            // SignIn User with this identity
            return new JsonResult(new { success = true, message = "User Logged in Successfully!", redirectUrl = Url.Page("/Main") });
        }

        public async Task<string> LoginAutomatically(string UserName, string Password)
        {
            LoginResponse response = await _userService.LoginUser(UserName, Password);

            if (response == null || response.Status != true)
            {
                string errorMessage = response == null ? "Invalid credentials" : response.Message;
                return errorMessage;
            }

            // Extract the token and other information from the response
            User user = response.User;
            string AccessToken = response.JwtToken;
            string RefreshToken = response.RefreshToken;
            List<string> features = response.FeatureKeys;
            string Role = response.Role;


            //Create the Identity Claim
            var userIdentity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            userIdentity.AddClaim(new Claim(ClaimTypes.PrimarySid, user.UserId.ToString()));
            userIdentity.AddClaim(new Claim(ClaimTypes.GivenName, UserName));
            userIdentity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
            //userIdentity.AddClaim(new Claim(ClaimTypes.Role, string.Join(',', features)));
            userIdentity.AddClaim(new Claim(ClaimTypes.Role, Role));
            userIdentity.AddClaim(new Claim(ClaimTypes.Authentication, AccessToken));
            userIdentity.AddClaim(new Claim(ClaimTypes.AuthorizationDecision, RefreshToken));

            foreach (string feature in features)
            {
                userIdentity.AddClaim(new Claim(feature, "true"));
                userIdentity.AddClaim(new Claim("Permission", feature));
            }

            var principal = new ClaimsPrincipal(userIdentity);

            try
            {
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        IssuedUtc = DateTime.UtcNow,
                        ExpiresUtc = DateTime.UtcNow.AddHours(1),
                        AllowRefresh = false
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while logging in. {ex.Message}");
                return $"Login Failed!";
            }

            return $"";
        }

    }
}
