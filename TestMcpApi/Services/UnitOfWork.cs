using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TestMcpApi.DBContexts;
using TestMcpApi.Interfaces;
using TestMcpApi.Models;

namespace TestMcpApi.Services
{
    public class UnitOfWork : IUnitOfWork
    {

        private KamfrContext _dbContext;
        private GenericRepository<User> _users;
        private GenericRepository<AllUserRole> _allUserRoles;
        private GenericRepository<Lender> _lenders;
        private GenericRepository<LoanTransaction> _loanTransactions;
        private GenericRepository<RealTransaction> _realTransactions;
        private GenericRepository<Role> _roles;
        private GenericRepository<ThirdParty> _thirdParties;

        public UnitOfWork(KamfrContext dbContext)
        {
            _dbContext = dbContext;
        }

        public DbContext GetDbContext()
        {
            return _dbContext;
        }

        public IRepository<User> Users
        {
            get
            {
                return _users ??
                    (_users = new GenericRepository<User>(_dbContext));
            }
        }
        public DbSet<User> UsersTable
        {
            get
            {
                return _dbContext.Users;
            }
        }

        public IRepository<AllUserRole> AllUserRoles
        {
            get
            {
                return _allUserRoles ??
                    (_allUserRoles = new GenericRepository<AllUserRole>(_dbContext));
            }
        }
        public DbSet<AllUserRole> AllUserRolesTable
        {
            get
            {
                return _dbContext.AllUserRoles;
            }
        }

        public IRepository<Lender> Lenders
        {
            get
            {
                return _lenders ??
                    (_lenders = new GenericRepository<Lender>(_dbContext));
            }
        }
        public DbSet<Lender> LendersTable
        {
            get
            {
                return _dbContext.Lenders;
            }
        }
        public IRepository<LoanTransaction> LoanTransactions
        {
            get
            {
                return _loanTransactions ??
                    (_loanTransactions = new GenericRepository<LoanTransaction>(_dbContext));
            }
        }
        public DbSet<LoanTransaction> LoanTransactionsTable
        {
            get
            {
                return _dbContext.LoanTransactions;
            }
        }
        public IRepository<RealTransaction> RealTransactions
        {
            get
            {
                return _realTransactions ??
                    (_realTransactions = new GenericRepository<RealTransaction>(_dbContext));
            }
        }
        public DbSet<RealTransaction> RealTransactionsTable
        {
            get
            {
                return _dbContext.RealTransactions;
            }
        }
        public IRepository<Role> Roles
        {
            get
            {
                return _roles ??
                    (_roles = new GenericRepository<Role>(_dbContext));
            }
        }
        public DbSet<Role> RolesTable
        {
            get
            {
                return _dbContext.Roles;
            }
        }
        public IRepository<ThirdParty> ThirdParties
        {
            get
            {
                return _thirdParties ??
                    (_thirdParties = new GenericRepository<ThirdParty>(_dbContext));
            }
        }
        public DbSet<ThirdParty> ThirdPartiesTable
        {
            get
            {
                return _dbContext.ThirdParties;
            }
        }

        public void Commit()
        {
            _dbContext.SaveChanges();
        }
    }
}
