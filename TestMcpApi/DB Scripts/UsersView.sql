USE [KAMFR]
GO

/****** Object:  View [dbo].[UsersView]    Script Date: 1/24/2026 3:47:26 AM ******/
DROP VIEW [dbo].[UsersView]
GO

/****** Object:  View [dbo].[UsersView]    Script Date: 1/24/2026 3:47:26 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[UsersView]
AS
SELECT u.UserID, ur.BasicRoleID AS RoleID, r.Role, u.UserName, u.FirstName, u.MiddleName, u.LastName, u.FirstName + ' ' + u.LastName AS Name, u.FirstName + ' ' + u.MiddleName + ' ' + u.LastName AS FullName, u.CompanyName, u.Address, u.City, u.State, u.PostalCode, u.CountryID, u.DOB, u.Phone, u.Email, u.Status, u.Sex, u.DateAdded, u.AddedBy, u.DateModified, u.ModifiedBy, u.LicenseNumber, u.LicensingEntity, u.NMLSID, u.BrokerFeeLoans, 
         u.BrokerFeeRealEstate, u.Notes, u.BusinessName, u.BusinessTaxID, u.BusinessAddress, u.BusinessCity, u.BusinessState, u.BusinessPostalCode, u.BusinessEmail, u.BankingInstitution, u.BankAccountNameUnder, u.BankAccountNumber, u.PhotoPath, u.OfficialID, RIGHT(u.SSN, 4) AS SSN, RIGHT(u.SSN, 4) AS SocialSecurityNumber, u.SSN AS HiddenSSN, u.BrokerOn1003, u.ProspectStatusID, u.ForwardMortgages, u.ReverseMortgages, 
         u.ResidentialRealEstateSales, u.ResidentialRealEstateLeases, u.CommercialLoans, u.CommercialRealEstateSales, u.CommercialRealEstateLeases, u.BankedLoans, u.BrokerOfRecord, u.IncomingWireFeeFromEscrow, u.OutgoingWireFeeToAgent, u.CashiersCheckFeeToAgent, u.OvernightDeliveryToAgent, u.WalkInBankDepositForAgent, u.EOFee, u.LoanReq, u.SendCRMInfoTo, u.NoInfo, u.WebsitesLinks, u.SocialMediaLinks, u.Phone2, u.Email2, u.SendSms
FROM   dbo.Users AS u LEFT OUTER JOIN
         dbo.ALL_UserRoles AS ur ON ur.UserID = u.UserID LEFT OUTER JOIN
         dbo.Roles AS r ON r.RoleID = ur.BasicRoleID
GO
