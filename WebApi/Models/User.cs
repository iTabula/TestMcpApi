namespace WebApi.Models
{
    public partial class User
    {
        public int UserId { get; set; }

        public string UserName { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? Phone { get; set; }

        public string Email { get; set; } = null!;

        public string? Password { get; set; }

        public DateTime DateAdded { get; set; }

        public int? AddedBy { get; set; }

        public DateTime? DateModified { get; set; }

        public int? ModifiedBy { get; set; }

        public int Status { get; set; }
    }

    public partial class UserPartial
    {
        public User? User { get; set; }
        public string RoleName { get; set; } = null!;
    }
}
