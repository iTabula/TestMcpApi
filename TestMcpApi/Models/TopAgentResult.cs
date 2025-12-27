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
        public decimal LoanTerm { get; set; }
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
        public int TotalTransactions { get; set; }
        public decimal? AvgAmount { get; set; }
        public decimal MaxAmount { get; set; }
        public decimal MinAmount { get; set; }
        public decimal VARatio { get; set; }

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
        public int? TC { get; set; }
        public decimal? Fees { get; set; }
    }

    public class PaymentInfo
    {
        public DateTime? ExpectedDate { get; set; }
        public string? PayableTo { get; set; }
        public string? AgentAddress { get; set; }
        public decimal? ProcessorAmount { get; set; }
        public decimal? CheckAmount { get; set; }
        public decimal? MailingFee { get; set; }
        public string? RoutingNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime? ClearDate { get; set; }
    }
    public class RealTransactionDto
    {
        public string? RealTransID { get; set; }
        public string? ClientFullName { get; set; }
        public string? AgentName { get; set; }
        public string? SubjectAddress { get; set; }
        public string? TransactionType { get; set; }
        public decimal? RealAmount { get; set; }
        public DateTime? ActualClosedDate { get; set; }
    }
    public class EscrowInfo
    {
        public string? Company { get; set; }
        public string? Phone { get; set; }
        public string? Officer { get; set; }
        public string? OfficerEmail { get; set; }
        public string? OfficerPhone { get; set; }
        public string? EscrowNumber { get; set; }
        public string? MethodSendType { get; set; }
    }
    public class TitleCompanyInfo
    {
        public string? Company { get; set; }
        public string? Phone { get; set; }
    }
    public class AppraisalCompanyInfo
    {
        public string? Company { get; set; }
        public string? Phone { get; set; }
    }
    public class BankInfo
    {
        public string? IncomingBank { get; set; }
        public string? OutgoingBank { get; set; }
        public string? BankName { get; set; }
        public string? AccountName { get; set; }
        public string? RoutingNumber { get; set; }
        public string? AccountNumber { get; set; }
        public decimal? AmountRetainedByKam { get; set; }
        public decimal? AmountPaidToKamAgent { get; set; }
    }
    public class TopLenderResult
    {
        public string? Lender { get; set; }
        public int Transactions { get; set; }
    }
    public class LenderStateResult
    {
        public string? CompanyName { get; set; }
        public string? LenderContact { get; set; }
        public string? City { get; set; }
    }
    public class LenderCompanyResult
    {
        public string? LenderContact { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
    public class VALenderResult
    {
        public string? CompanyName { get; set; }
        public string? LenderContact { get; set; }
    }
    public class LenderUsernameResult
    {
        public string? CompanyName { get; set; }
        public string? LenderContact { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
    public class TopLenderCityResult
    {
        public string City { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    public class TopLenderStateResult
    {
        public string State { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    public class LenderVAStateStats
    {
        public string State { get; set; } = string.Empty;
        public int Total { get; set; }
        public int VAApproved { get; set; }
        public double Ratio { get; set; }
    }
    public class TopThirdPartyResult
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ThirdPartyContactResult
    {
        public string Name { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Website { get; set; }
    }


}
