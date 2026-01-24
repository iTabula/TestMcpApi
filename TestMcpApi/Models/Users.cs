namespace TestMcpApi.Models
{
    using System;

    public class Users
    {
        public int UserID { get; set; }
        public int? AddedBy { get; set; }
        public string? Address { get; set; }
        public string? BankAccountNameUnder { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankedLoans { get; set; }
        public string? BankingInstitution { get; set; }
        public string? BrokerFeeLoans { get; set; }
        public string? BrokerFeeRealEstate { get; set; }
        public string? BrokerOfRecord { get; set; }
        public int BrokerOn1003 { get; set; } // Not null in schema
        public string? BusinessAddress { get; set; }
        public string? BusinessCity { get; set; }
        public string? BusinessEmail { get; set; }
        public string? BusinessName { get; set; }
        public string? BusinessPostalCode { get; set; }
        public string? BusinessState { get; set; }
        public string? BusinessTaxID { get; set; }
        public string? CashiersCheckFeeToAgent { get; set; }
        public string? City { get; set; }
        public string? CommercialLoans { get; set; }
        public string? CommercialRealEstateLeases { get; set; }
        public string? CommercialRealEstateSales { get; set; }
        public string? CompanyName { get; set; }
        public string? CountryID { get; set; }
        public DateTime DateAdded { get; set; } // Not null in schema
        public DateTime? DateModified { get; set; }
        public string? DOB { get; set; }
        public string? Email { get; set; }
        public string? Email2 { get; set; }
        public string? EOFee { get; set; }
        public string? FirstName { get; set; } // Marked 0 (Not Null) in schema, but set to nullable string? per request
        public string? ForwardMortgages { get; set; }
        public string? FullName { get; set; }
        public string? HiddenSSN { get; set; }
        public string? IncomingWireFeeFromEscrow { get; set; }
        public string? LastName { get; set; }
        public string? LicenseNumber { get; set; }
        public string? LicensingEntity { get; set; }
        public string? LoanReq { get; set; }
        public string? MiddleName { get; set; }
        public int? ModifiedBy { get; set; }
        public string? Name { get; set; }
        public string? NMLSID { get; set; }
        public short? NoInfo { get; set; }
        public string? Notes { get; set; }
        public string? OfficialID { get; set; }
        public string? OutgoingWireFeeToAgent { get; set; }
        public string? OvernightDeliveryToAgent { get; set; }
        public string? Phone { get; set; }
        public string? Phone2 { get; set; }
        public string? PhotoPath { get; set; }
        public string? PostalCode { get; set; }
        public long? ProspectStatusID { get; set; }
        public string? ResidentialRealEstateLeases { get; set; }
    }

}
