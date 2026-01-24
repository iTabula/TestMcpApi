namespace TestMcpApi.Models
{
    public class VapiCall
    {
        public int Id { get; set; }
        public string CallId { get; set; }
        public string? Phone { get; set; }
        public int? UserId { get; set; }
        public string? UserRole { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastUpdatedOn { get; set; }
        public int IsAuthenticated { get; set; }

    }
}
