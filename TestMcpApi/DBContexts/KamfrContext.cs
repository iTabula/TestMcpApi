using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TestMcpApi.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace TestMcpApi.DBContexts;

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
    public virtual DbSet<Lender> Lenders { get; set; }
    public virtual DbSet<LoanTransaction> LoanTransactions { get; set; }
    public virtual DbSet<RealTransaction> RealTransactions { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<ThirdParty> ThirdParties { get; set; }
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


        modelBuilder.Entity<Lender>(entity =>
        {
            entity.ToView("LendersView");
            entity.HasNoKey();

            entity.Property(e => e.LenderID).HasColumnName("LenderID");

            entity.Property(e => e.CompanyName).HasMaxLength(255);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(255);
            entity.Property(e => e.LastName).HasMaxLength(255);
            entity.Property(e => e.LenderContact).HasMaxLength(255);

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.City).HasMaxLength(255);
            entity.Property(e => e.State).HasMaxLength(2);
            entity.Property(e => e.PostalCode).HasMaxLength(10);

            entity.Property(e => e.WorkPhone1).HasMaxLength(20);
            entity.Property(e => e.WorkPhone2).HasMaxLength(20);
            entity.Property(e => e.Cell).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Website).HasMaxLength(500);

            entity.Property(e => e.MinimumComp).HasMaxLength(255);
            entity.Property(e => e.MaximumComp).HasMaxLength(255);
            entity.Property(e => e.LenderPaidComp).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BrokerCode).HasMaxLength(50);

            entity.Property(e => e.Notes);
            entity.Property(e => e.ProcessorNotes);

            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.VAApproved).HasMaxLength(10);

            entity.Property(e => e.DateAdded).HasColumnType("datetime");
            entity.Property(e => e.AddedBy).HasMaxLength(100);
            entity.Property(e => e.LastUpdatedOn).HasColumnType("datetime");
            entity.Property(e => e.LastUpdatedBy).HasMaxLength(100);

            // Calculated / UI-only fields
            entity.Ignore(e => e.TotalLoanTransactions);
            entity.Ignore(e => e.TotalLoanAmount);
            entity.Ignore(e => e.LastTransactionDate);
        });


        modelBuilder.Entity<LoanTransaction>(entity =>
        {
            entity.ToView("LoanTransactionsView");
            entity.HasNoKey();

            entity.Property(e => e.LoanTransID).HasColumnName("LoanTransID");
            entity.Property(e => e.AgentName).HasMaxLength(255);
            entity.Property(e => e.AddedBy).HasMaxLength(255);

            entity.Property(e => e.BorrowerFirstName).HasMaxLength(255);
            entity.Property(e => e.BorrowerLastName).HasMaxLength(255);
            entity.Property(e => e.BorrowerPhone).HasMaxLength(50);
            entity.Property(e => e.BorrowerEmail).HasMaxLength(255);

            entity.Property(e => e.SubjectAddress).HasMaxLength(255);
            entity.Property(e => e.SubjectCity).HasMaxLength(255);
            entity.Property(e => e.SubjectState).HasMaxLength(2);
            entity.Property(e => e.SubjectPostalCode).HasMaxLength(10);

            entity.Property(e => e.LoanAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AppraisedValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LTV).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InterestRate).HasColumnType("decimal(18,3)");

            entity.Property(e => e.ActualCommKamFromEscrow).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AmountPaidToKamAgent).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AmountRetainedByKam).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DirectDepositFee).HasColumnType("decimal(18,2)");
            entity.Property(e => e.OtherFee).HasColumnType("decimal(18,2)");
        });


        modelBuilder.Entity<RealTransaction>(entity =>
        {
            entity.ToView("RealTransactionsView");
            entity.HasNoKey();

            entity.Property(e => e.RealTransID).HasColumnName("RealTransID");
            entity.Property(e => e.AgentName).HasMaxLength(255);
            entity.Property(e => e.AddedBy).HasMaxLength(255);

            entity.Property(e => e.ClientFirstName).HasMaxLength(255);
            entity.Property(e => e.ClientLastName).HasMaxLength(255);
            entity.Property(e => e.ClientPhone).HasMaxLength(50);
            entity.Property(e => e.ClientEmail).HasMaxLength(255);

            entity.Property(e => e.SubjectAddress).HasMaxLength(255);
            entity.Property(e => e.SubjectCity).HasMaxLength(255);
            entity.Property(e => e.SubjectState).HasMaxLength(2);
            entity.Property(e => e.SubjectPostalCode).HasMaxLength(10);

            entity.Property(e => e.RealAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LTV).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InterestRate).HasColumnType("decimal(18,3)");

            entity.Property(e => e.TCFees).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AmountPaidToKamAgent).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AmountRetainedByKam).HasColumnType("decimal(18,2)");
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

        modelBuilder.Entity<ThirdParty>(entity =>
        {
            entity.ToView("ThirdPartiesView");
            entity.HasNoKey();

            entity.Property(e => e.ThirdPartiesID).HasColumnName("ThirdPartiesID");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Purpose).HasMaxLength(50);
            entity.Property(e => e.Website).HasMaxLength(500);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.Notes);
            entity.Property(e => e.AdminViewOnly).HasMaxLength(20);
            entity.Property(e => e.LastUpdatedOn).HasColumnType("datetime");
            entity.Property(e => e.LastUpdatedBy).HasMaxLength(100);
        });


        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.UserGuid);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.DateAdded).HasColumnType("datetime");
            entity.Property(e => e.DateModified).HasColumnType("datetime");
            entity.Property(e => e.Active);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
