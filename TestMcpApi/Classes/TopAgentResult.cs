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
}
