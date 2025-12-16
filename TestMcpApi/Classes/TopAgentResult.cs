namespace TestMcpApi.Classes
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
}
