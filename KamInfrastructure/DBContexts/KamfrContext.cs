using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using KamInfrastructure.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.SqlServer; // Add this using directive

namespace KamInfrastructure.DBContexts;

public partial class KamfrContext : DbContext
{
    private readonly IConfiguration _configuration;
    private readonly string connectionString = string.Empty;
    public KamfrContext()
    {
    }

    public KamfrContext(DbContextOptions<KamfrContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
        connectionString = _configuration.GetConnectionString("DefaultConnection");
    }

    public virtual DbSet<AllUserRole> AllUserRoles { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer(connectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<AllUserRole>(entity =>
        {
            entity.ToTable("ALL_UserRoles");
            entity.HasKey(e => e.UserRoleId);

            entity.Property(e => e.UserRoleId).HasColumnName("UserRoleID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.BasicRoleId).HasColumnName("BasicRoleID");
            entity.Property(e => e.HasCustomFeatures);
        });


        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.RoleId);

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.Role1)
                  .HasColumnName("Role")
                  .HasMaxLength(50);
        });


        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.UserName);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.DateAdded).HasColumnType("datetime");
            entity.Property(e => e.DateModified).HasColumnType("datetime");
            entity.Property(e => e.Status);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
