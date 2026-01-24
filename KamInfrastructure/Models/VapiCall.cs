using System;
using System.Collections.Generic;
using System.Text;

namespace KamInfrastructure.Models
{
    public class VapiCall
    {
        public int Id { get; set; }
        public Guid CallId { get; set; }
        public string? Phone { get; set; }
        public int? UserId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastUpdatedOn { get; set; }
        public int IsAuthenticated { get; set; }

    }
}
