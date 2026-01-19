using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KamInfrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KamInfrastructure.Interfaces;

namespace KamInfrastructure.Services
{
    public interface IUnitOfWork
    {
        DbContext GetDbContext();

        IRepository<User> Users { get; }
        DbSet<User> UsersTable { get; }

        IRepository<AllUserRole> AllUserRoles { get; }
        DbSet<AllUserRole> AllUserRolesTable { get; }

        IRepository<Role> Roles { get; }
        DbSet<Role> RolesTable { get; }

        void Commit();
    }

}
