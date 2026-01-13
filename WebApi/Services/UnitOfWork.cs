using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WebApi.DBContexts;
using WebApi.Interfaces;
using WebApi.Models;

namespace WebApi.Services
{
    public class UnitOfWork : IUnitOfWork
    {

        private KamfrContext _dbContext;
        private GenericRepository<User> _users;
        private GenericRepository<AllUserRole> _allUserRoles;
        private GenericRepository<Role> _roles;

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

        public void Commit()
        {
            _dbContext.SaveChanges();
        }
    }
}
