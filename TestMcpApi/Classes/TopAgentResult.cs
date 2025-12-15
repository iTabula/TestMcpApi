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
}
