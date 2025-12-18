namespace TestMcpApi.Models
{
    public class TopAgentResult
    {
        public string Agent { get; set; }
        public int Transactions { get; set; }
    }

    public class TransactionsResult
    {
        public string ID { get; set; }
        public decimal LoanAmount { get; set; }
        public string LoanType { get; set; }
        public string LoanTerm { get; set; }
    }

    public class LoanSummaryResult
    {
        public string LoanID { get; set; } = string.Empty;
        public string? Agent { get; set; }
        public decimal? LoanAmount { get; set; }
        public string? LoanType { get; set; }
        public string? DateAdded { get; set; }
    }
    public class TopCityResult
    {
        public string City { get; set; }
        public int Transactions { get; set; }
    }
    public class TopPropertyTypeResult
    {
        public string PropType { get; set; }
        public int Transactions { get; set; }
    }
    public class TopTransactionTypeResult
    {
        public string TransactionType { get; set; }
        public int Transactions { get; set; }
    }
    public class TopMortgageTypeResult
    {
        public string MortgageType { get; set; }
        public int Transactions { get; set; }
    }
    public class TopBrokeringTypeResult
    {
        public string BrokeringType { get; set; }
        public int Transactions { get; set; }
    }
    public class TopLoanTypeResult
    {
        public string LoanType { get; set; }
        public int Transactions { get; set; }
    }
    public class TopEscrowMethodResult
    {
        public string EscrowMethod { get; set; }
        public int Transactions { get; set; }
    }
    public class TopTitleCompanyResult
    {
        public string TitleCompany { get; set; }
        public int Transactions { get; set; }
    }
    public class TopEscrowCompanyResult
    {
        public string EscrowCompany { get; set; }
        public int Transactions { get; set; }
    }
    public class EscrowTransactionDto
    {
        public string LoanTransID { get; set; }
        public string AgentName { get; set; }
        public decimal? LoanAmount { get; set; }
        public string SubjectCity { get; set; }
        public string SubjectState { get; set; }
    }
    public class EscrowCompanyStatsResult
    {
        public int TotalLoans { get; set; }
        public decimal AverageLoanAmount { get; set; }
        public decimal HighestLoanAmount { get; set; }
        public decimal LowestLoanAmount { get; set; }
    }
    public class TransactionDto
    {
        public string LoanTransID { get; set; } = "";
        public string AgentName { get; set; } = "";
        public decimal? LoanAmount { get; set; }
        public DateTime? LoanDate { get; set; }
    }
    public class LenderStatsResult
    {
        public int TotalLoans { get; set; }
        public decimal AverageLoanAmount { get; set; }
        public decimal HighestLoanAmount { get; set; }
        public decimal LowestLoanAmount { get; set; }
    }
    public class HomeInspectionInfo
    {
        public string? Name { get; set; }
        public string? Done { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }

    public class PestInspectionInfo
    {
        public string? Name { get; set; }
        public string? Done { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }

    public class TCInfo
    {
        public string? Flag { get; set; }
        public int? Number { get; set; }
        public decimal? Fees { get; set; }
    }

    public class PaymentInfo
    {
        public string? PayableTo { get; set; }
        public string? RoutingNumber { get; set; }
    }


}
