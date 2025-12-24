namespace TestMcpApi.Models
{
    public class Lender
    {
        // Identifiers
        public int LenderID { get; set; }

        // Company & Contact
        public string? CompanyName { get; set; }
        public string? Title { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? LenderContact { get; set; }

        // Address
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }

        // Communication
        public string? WorkPhone1 { get; set; }
        public string? WorkPhone2 { get; set; }
        public string? Cell { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }

        // Compensation
        public string? MinimumComp { get; set; }
        public string? MaximumComp { get; set; }
        public decimal? LenderPaidComp { get; set; }
        public string? BrokerCode { get; set; }

        // Notes
        public string? Notes { get; set; }
        public string? ProcessorNotes { get; set; }

        // Status
        public string? Status { get; set; }
        public string? VAApproved { get; set; }

        // Audit
        public DateTime? DateAdded { get; set; }
        public string? AddedBy { get; set; }

        public DateTime? LastUpdatedOn { get; set; }
        public string? LastUpdatedBy { get; set; }

        // Aggregated Loan Stats
        public int TotalLoanTransactions { get; set; }
        public decimal? TotalLoanAmount { get; set; }
        public DateTime? LastTransactionDate { get; set; }
    }

}
