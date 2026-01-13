using System;

namespace TestMcpApi.Models
{
    public class ThirdParty
    {
        public int ThirdPartiesID { get; set; }

        public string? Name { get; set; }
        public string? Purpose { get; set; }
        public string? Website { get; set; }

        public string? Username { get; set; }

        public string? Notes { get; set; }
        public string? AdminViewOnly { get; set; }   // Yes / No / Unknown
        public DateTime? LastUpdatedOn { get; set; }
        public string? LastUpdatedBy { get; set; }
    }
}
