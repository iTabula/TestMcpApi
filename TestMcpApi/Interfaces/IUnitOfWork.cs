using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestMcpApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestMcpApi.Interfaces;

namespace TestMcpApi.Services
{
    public interface IUnitOfWork
    {
        DbContext GetDbContext();

        IRepository<User> Users { get; }
        DbSet<User> UsersTable { get; }

        IRepository<AllUserRole> AllUserRoles { get; }
        DbSet<AllUserRole> AllUserRolesTable { get; }

        IRepository<Lender> Lenders { get; }
        DbSet<Lender> LendersTable { get; }

        IRepository<LoanTransaction> LoanTransactions { get; }
        DbSet<LoanTransaction> LoanTransactionsTable { get; }

        IRepository<RealTransaction> RealTransactions { get; }
        DbSet<RealTransaction> RealTransactionsTable { get; }

        IRepository<Role> Roles { get; }
        DbSet<Role> RolesTable { get; }

        IRepository<ThirdParty> ThirdParties { get; }
        DbSet<ThirdParty> ThirdPartiesTable { get; }

        void Commit();
    }

}
