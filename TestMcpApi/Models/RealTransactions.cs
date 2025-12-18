using System;

namespace TestMcpApi.Models
{
    public class RealTransaction
    {
        public string? RealTransID { get; set; }
        public DateTime? ActualClosedDate { get; set; }
        public DateTime? EstimatedClosingDate { get; set; }
        public DateTime? DateAdded { get; set; }

        // Agent info
        public string? AgentName { get; set; }
        public string? AddedBy { get; set; }

        // Client info
        public string? ClientFirstName { get; set; }
        public string? ClientMiddleName { get; set; }
        public string? ClientLastName { get; set; }
        public string? ClientPhone { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientAddress { get; set; }
        public string? ClientCity { get; set; }
        public string? ClientState { get; set; }
        public string? ClientPostalCode { get; set; }

        // Property info
        public string? SubjectAddress { get; set; }
        public string? SubjectCity { get; set; }
        public string? SubjectState { get; set; }
        public string? SubjectPostalCode { get; set; }

        public string? PropType { get; set; }
        public string? TransactionType { get; set; }

        // Real estate specific types
        public string? RealType { get; set; }
        public string? RealSubType { get; set; }
        public decimal? RealTerm { get; set; }
        public decimal? RealAmount { get; set; }

        // Financial info
        public decimal? AppraisedValue { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? LTV { get; set; }
        public decimal? InterestRate { get; set; }

        // Lender info
        public string? LenderName { get; set; }
        public string? LenderContact { get; set; }
        public string? LenderPhone { get; set; }
        public string? LenderEmail { get; set; }

        // Title / Appraisal / Escrow
        public string? TitleCompany { get; set; }
        public string? TitleCoPhone { get; set; }
        public string? AppraisalCompany { get; set; }
        public string? AppraisalCoPhone { get; set; }
        public string? EscrowCompany { get; set; }
        public string? EscrowCoPhone { get; set; }
        public string? EscrowOfficer { get; set; }
        public string? EscrowOfficerEmail { get; set; }
        public string? EscrowOfficerPhone { get; set; }
        public string? EscrowNumber { get; set; }
        public string? EscrowMethodSendType { get; set; }

        // Commission / fees
        public DateTime? CommReceivedDate { get; set; }
        public decimal? ActualCommKamFromEscrow { get; set; }
        public decimal? KamBrokerFee { get; set; }
        public decimal? DirectDepositFee { get; set; }
        public decimal? OtherFee { get; set; }

        public string? CommPaidMethod { get; set; }
        public DateTime? CommPaidDate { get; set; }

        // Bank info
        public string? IncomingBank { get; set; }
        public string? OutgoingBank { get; set; }
        public decimal? AmountRetainedByKam { get; set; }
        public decimal? AmountPaidToKamAgent { get; set; }

        // Status / flags
        public string? ActiveStatus { get; set; }
        public string? AppraisalDoneStatus { get; set; }
        public string? CreditReportRan { get; set; }
        public string? WhoPaidForCreditReport { get; set; }
        public string? WhoPulledCreditReport { get; set; }

        // Real estate specific fields
        public string? TransType { get; set; }
        public string? PartyPresented { get; set; }
        public decimal? Price { get; set; }
        public string? ClientType { get; set; }
        public string? FinanceInfo { get; set; }
        public int? CARForms { get; set; }
        public string? NMLSNumber { get; set; }

        // Home inspection
        public string? HomeInspectionName { get; set; }
        public string? HomeInspectionDone { get; set; }
        public string? HomeInspectionPhone { get; set; }
        public string? HomeInspectionEmail { get; set; }
        public string? HomeInspectionNotes { get; set; }

        // Pest inspection
        public string? PestInspectionName { get; set; }
        public string? PestInspectionDone { get; set; }
        public string? PestInspectionPhone { get; set; }
        public string? PestInspectionEmail { get; set; }
        public string? PestInspectionNotes { get; set; }

        // TC info
        public string? TCFlag { get; set; }
        public int? TC { get; set; }
        public decimal? TCFees { get; set; }

        // Payment / bank / delivery
        public DateTime? ExpectedDate { get; set; }
        public string? PayableTo { get; set; }
        public string? AgentAddress { get; set; }
        public string? BankName { get; set; }
        public string? AccountName { get; set; }
        public string? RoutingNumber { get; set; }
        public string? AccountNumber { get; set; }
        public decimal? ProcessorAmount { get; set; }
        public string? Notes { get; set; }
        public DateTime? ClearDate { get; set; }
        public decimal? CheckAmount { get; set; }
        public decimal? MailingFee { get; set; }
    }
}
