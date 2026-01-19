namespace KamInfrastructure.Models
{
    public partial class AllUserRole
    {
        public long UserRoleId { get; set; }

        public long UserId { get; set; }

        public long BasicRoleId { get; set; }

        public short? HasCustomFeatures { get; set; }
    }

}
