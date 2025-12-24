USE [KAMFR]
GO

DROP VIEW IF EXISTS [dbo].[LendersView]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[LendersView] AS

SELECT
    l.LenderID,

    -- Company & Contact Info
    l.CompanyName,
    l.Title,
    l.FirstName,
    l.LastName,
    RTRIM(
        COALESCE(l.FirstName + ' ', '') +
        COALESCE(l.MiddleName + ' ', '') +
        COALESCE(l.LastName, '')
    ) AS LenderContact,

    -- Address
    l.Address,
    l.City,
    l.State,
    l.PostalCode,

    -- Communication
    l.WorkPhone1,
    l.WorkPhone2,
    l.Cell,
    l.Email,
    l.Website,

    -- Compensation
    l.MinimumComp,
    l.MaximumComp,
    l.LenderPaidComp,
    l.BrokerCode,

    -- Notes
    l.Notes,
    l.ProcessorNotes,

    -- Status Fields
    CASE 
        WHEN l.Status = 1 THEN 'Active'
        WHEN l.Status = 0 THEN 'Inactive'
        ELSE 'Unknown'
    END AS Status,

    CASE
        WHEN l.VAApproved = 1 THEN 'Yes'
        WHEN l.VAApproved = 0 THEN 'No'
        ELSE 'Unknown'
    END AS VAApproved,

    -- Audit Info
    l.DateAdded,
    u1.FirstName + ' ' + u1.LastName AS AddedBy,
    l.LastUpdatedOn,
    u2.FirstName + ' ' + u2.LastName AS LastUpdatedBy,

    -- ==========================
    -- Aggregated Loan Statistics
    -- ==========================
    COUNT(lt.LoanTransID) AS TotalLoanTransactions,
    SUM(lt.LoanAmount) AS TotalLoanAmount,
    MAX(lt.ActualClosedDate) AS LastTransactionDate

FROM [dbo].[Lenders] l

LEFT JOIN [dbo].[Users] u1
    ON u1.UserID = l.AddedBy

LEFT JOIN [dbo].[Users] u2
    ON u2.UserID = l.LastUpdatedBy

LEFT JOIN [dbo].[LoanTransactions] lt
    ON lt.LenderID = l.LenderID

GROUP BY
    l.LenderID,
    l.CompanyName,
    l.Title,
    l.FirstName,
    l.MiddleName,
    l.LastName,
    l.Address,
    l.City,
    l.State,
    l.PostalCode,
    l.WorkPhone1,
    l.WorkPhone2,
    l.Cell,
    l.Email,
    l.Website,
    l.MinimumComp,
    l.MaximumComp,
    l.LenderPaidComp,
    l.BrokerCode,
    l.Notes,
    l.ProcessorNotes,
    l.Status,
    l.VAApproved,
    l.DateAdded,
    u1.FirstName,
    u1.LastName,
    l.LastUpdatedOn,
    u2.FirstName,
    u2.LastName
GO
