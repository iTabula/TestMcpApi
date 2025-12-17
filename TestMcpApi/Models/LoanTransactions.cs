using System.Text.Json.Serialization;

namespace TestMcpApi.Models
{
    public class LoanTransaction
    {
        public string? LoanTransID { get; set; }
        public DateTime? ActualClosedDate { get; set; }
        public DateTime? DateAdded { get; set; }
        public string? AgentName { get; set; }
        public string? AddedBy { get; set; }
        public string? BorrowerFirstName { get; set; }
        public string? BorrowerLastName { get; set; }
        public string? BorrowerPhone { get; set; }
        public string? BorrowerEmail { get; set; }
        public string? SubjectAddress { get; set; }
        public string? SubjectCity { get; set; }
        public string? SubjectState { get; set; }
        public string? SubjectPostalCode { get; set; }
        public string? PropType { get; set; }
        public string? TransactionType { get; set; }
        public string? MortgageType { get; set; }
        public string? BrokeringType { get; set; }
        public string? LoanType { get; set; }
        public decimal? LoanTerm { get; set; }
        public decimal? LoanAmount { get; set; }
        public decimal? AppraisedValue { get; set; }
        public string? AppraisalCompany { get; set; }
        public string? AppraisalCoPhone { get; set; }
        public decimal? LTV { get; set; }
        public decimal? InterestRate { get; set; }
        public string? TitleCompany { get; set; }
        public string? TitleCoPhone { get; set; }
        public decimal? CreditScore { get; set; }
        public string? EscrowCompany { get; set; }
        public string? EscrowCoPhone { get; set; }
        public string? EscrowOfficer { get; set; }
        public string? EscrowOfficerEmail { get; set; }
        public string? EscrowOfficerPhone { get; set; }
        public string? EscrowNumber { get; set; }
        public string? EscrowMethodSendType { get; set; }
        public DateTime? CommReceivedDate { get; set; }
        public decimal? ActualCommKamFromEscrow { get; set; }
        public decimal? KamBrokerFee { get; set; }
        public string? CommPaidMethod { get; set; }
        public DateTime? CommPaidDate { get; set; }
        public string? IncomingBank { get; set; }
        public string? OutgoingBank { get; set; }
        public decimal? AmountRetainedByKam { get; set; }
        public decimal? AmountPaidToKamAgent { get; set; }
        public decimal? OtherFee { get; set; }
        public decimal? DirectDepositFee { get; set; }
        public string? LenderName { get; set; }
        public string? LenderContact { get; set; }
        public string? LenderPhone { get; set; }
        public string? LenderEmail { get; set; }
        public string? Active { get; set; }
        public string? AppraisalDone { get; set; }
        public string? CreditReportRan { get; set; }
        public string? WhoPaidForCreditReport { get; set; }
        public string? WhoPulledCreditReport { get; set; }
    }
}
