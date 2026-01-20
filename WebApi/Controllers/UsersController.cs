using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using KamInfrastructure.Interfaces;
using KamInfrastructure.Services;
using KamHttp.Interfaces;
using KamHttp.Services;
using KamInfrastructure.Models;
using System.Linq;
using Microsoft.Extensions.Logging;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using KamInfrastructure.DBContexts;
using Dapper;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly KamfrContext _context;
        private readonly ILogger<UsersController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly string connectionString = string.Empty;
        public UsersController(ILogger<UsersController> logger, KamfrContext context,
            IUnitOfWork unitOfWork, ITokenService tokenService, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _configuration = configuration;
            connectionString = _configuration.GetConnectionString("DefaultConnection");
        }


        [HttpGet]
        public async Task<List<UserPartial>> GetUsers([FromServices] IMemoryCache cache)
        {
            const string cacheKey = "users";
            using (var connection = new SqlConnection(connectionString))
            {
                if (!cache.TryGetValue(cacheKey, out List<UserPartial>? data))
                {
                    var usersWithRoles = new List<UserPartial>();
                    var sql1 = "SELECT TOP(5) * FROM Users";
                    var x = await connection.QueryAsync<User>(sql1);
                    List<User> users = x.AsList();
                    var sql2 = "SELECT * FROM AllUserRoles";
                    var y = await connection.QueryAsync<AllUserRole>(sql2);
                    List<AllUserRole> userRoles = y.AsList();
                    var sql3 = "SELECT * FROM Roles";
                    var z = await connection.QueryAsync<Role>(sql3);
                    List<Role> roles = z.AsList();

                    foreach (var user in users)
                    {
                        var userRole = userRoles.SingleOrDefault(ur => ur.UserId == user.UserId);
                        var roleName = userRole != null ? roles.SingleOrDefault(r => r.RoleId == userRole.BasicRoleId)?.Role1 : null;

                        usersWithRoles.Add(new UserPartial
                        {
                            User = user,
                            RoleName = roleName!
                        });
                    }
                    data = usersWithRoles;
                    var cacheEntryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    };
                    cache.Set(cacheKey, data, cacheEntryOptions);
                }
                return data!;
            }
        }

        [HttpGet("Email/{email}")]
        public async Task<IActionResult> GetUserByEmail(string email)
        {
            var data =
                await _unitOfWork.UsersTable.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email == email);

            return Ok(data);
        }

        [HttpGet("Group/{groupId}")]
        public async Task<IActionResult> GetUsersBelongToGroup(int groupId)
        {
            var data =
                await _unitOfWork.AllUserRolesTable.AsNoTracking()
                .Where(x => x.BasicRoleId == groupId)
                .Select(x => x.UserId)
                .ToListAsync();

            return Ok(data);
        }

        // Getting details of a specific user
        [HttpGet("{UserId}")]
        public async Task<IActionResult> Details(int UserId)
        {
            if (UserId == null || _context.Users == null)
            {
                return NotFound();
            }

            //Getting user information
            User? user = await _context.Users.AsNoTracking()
                .Select(x => new User()
                {
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    Email = x.Email,
                    Phone = x.Phone,
                    UserId = x.UserId,
                    DateModified = x.DateModified,
                    ModifiedBy = x.ModifiedBy,
                    DateAdded = x.DateAdded,
                    AddedBy = x.AddedBy,
                    Status = x.Status,
                })
               .FirstOrDefaultAsync(x => x.UserId == UserId);

            if (user == null)
            {
                return NotFound();
            }

            //Getting groups this user belongs to         
            List<long> lstUserUserGroups =
                await _unitOfWork.AllUserRolesTable
                .Where(x => x.UserId == UserId)
                .Select(x => x.BasicRoleId)
                .ToListAsync();
            List<string> lstUserGroups = await _context.Roles
                .AsNoTracking().Where(x => lstUserUserGroups.Contains(x.RoleId))
                 .Select(x => x.Role1)
                 .ToListAsync();

            return Ok(new UserPartial()
            {
                User = user,
                RoleName = string.Join(", ", lstUserGroups)
            });
        }

        [HttpPost("Create")]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] User user, [FromServices] IMemoryCache cache)
        {
            if (ModelState.IsValid)
            {
                //Save the UserGroupId in ModifiedBy temporarily to assign default group
                int defaultGroupId = user.ModifiedBy!.Value;

                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
                _context.ChangeTracker.Clear();
                _context.Entry(user).State = EntityState.Modified;

                //Add defaults
                user.DateAdded = DateTime.UtcNow;
                user.DateModified = DateTime.UtcNow;
                user.ModifiedBy = user.AddedBy;

                _context.Add(user);
                await _context.SaveChangesAsync();
                InvalidateCache(cache);

                //Create the user group as well
                if (ModelState.IsValid)
                {
                    AllUserRole userUserGroup = new AllUserRole()
                    {
                        UserId = user.UserId,
                        BasicRoleId = defaultGroupId
                    };
                    _context.Add(userUserGroup);

                    await _context.SaveChangesAsync();
                }

                return Ok(user);
            }
            return BadRequest();
        }


        // POST: Users/Edit/5
        [HttpPost("Update/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Edit(int id, [FromBody] User user, [FromServices] IMemoryCache cache)
        {
            var CurrentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == id);
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
            _context.ChangeTracker.Clear();
            _context.Entry(CurrentUser!).State = EntityState.Modified;
            CurrentUser!.Email = user.Email;
            CurrentUser!.FirstName = user.FirstName;
            CurrentUser!.LastName = user.LastName;
            CurrentUser!.Phone = user.Phone;
            CurrentUser!.DateModified = DateTime.UtcNow;
            CurrentUser!.Email = user.Email;
            CurrentUser!.Password = user.Password;
            CurrentUser!.ModifiedBy = user.ModifiedBy;
            CurrentUser!.Status = user.Status;

            _context.Update(CurrentUser);
            await _context.SaveChangesAsync();
            InvalidateCache(cache);
            return Ok(user);
        }


        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginUser([FromBody] LoginRequest LoginRequest)
        {

            // Validate Username and Password

            //Check for nulls
            if (string.IsNullOrEmpty(LoginRequest.userName) || string.IsNullOrEmpty(LoginRequest.password))
            {
                return Ok(new LoginResponse()
                {
                    id = "",
                    status = false,
                    code = "-1",
                    message = "Incomplete or Empty Credentials!",
                    user = null!,
                    jwtToken = "",
                    refreshToken = "",
                    featureKeys = null!,
                    role = ""
                });
            }

            User user = await _unitOfWork.UsersTable.FirstOrDefaultAsync(x => x.UserName == LoginRequest.userName && x.Password == LoginRequest.password);
            if (user == null || user.Password != LoginRequest.password)
            {
                return Ok(new LoginResponse()
                {
                    id = "",
                    status = false,
                    code = "-1",
                    message = "Invalid UserName or Password!",
                    user = null!,
                    jwtToken = "",
                    refreshToken = "",
                    featureKeys = null!,
                    role = ""
                });
            }

            //User Info
            string email = user.Email;
            int userId = user.UserId;

            var userRoles = await _unitOfWork.AllUserRolesTable.ToListAsync();
            var roles = await _unitOfWork.RolesTable.ToListAsync();
            var userRole = userRoles.SingleOrDefault(ur => ur.UserId == userId);
            var roleName = userRole != null ? roles.SingleOrDefault(r => r.RoleId == userRole.BasicRoleId)?.Role1 : null;


            // Getting the user groups and user features

            //List<long> lstUserGroups =
            //    await _unitOfWork.AllUserRolesTable
            //    .Where(x => x.UserId == userId)
            //    .Select(x => x.BasicRoleId)
            //    .ToListAsync();

            // Generating The User's Token
            string jwtToken = _tokenService.GenerateJwtToken(email, userId.ToString());
            string refreshToken = _tokenService.GenerateRefreshToken();

            LoginResponse loginResponse = new LoginResponse()
            {
                id = userId.ToString(),
                status = true,
                code = "0",
                message = "",
                user = user,
                jwtToken = jwtToken,
                refreshToken = refreshToken,
                featureKeys = new List<string>(),
                role = roleName!
            };

            return Ok(loginResponse);

        }


        // Method to invalidate cache
        private void InvalidateCache(IMemoryCache cache)
        {
            const string cacheKey = "users";
            cache.Remove(cacheKey);
        }
    }
}
