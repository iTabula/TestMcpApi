USE [KAMFR]
GO

DROP VIEW IF EXISTS [dbo].[RealTransactionsView]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[RealTransactionsView] AS

SELECT 
    r.RealTransID,
    r.ActualClosedDate,
    r.EstimatedClosingDate,
    r.DateAdded,

    -- AGENT + ADDED BY
    au.FirstName + ' ' + au.LastName AS AgentName,
    ad.FirstName + ' ' + ad.LastName AS AddedBy,

    -- CLIENT INFO
    r.ClientFirstName,
    r.ClientMiddleName,
    r.ClientLastName,
    r.ClientPhone,
    r.ClientEmail,
    r.ClientAddress,
    r.ClientCity,
    r.ClientState,
    r.ClientPostalCode,

    -- PROPERTY INFO
    r.SubjectAddress,
    r.SubjectCity,
    r.SubjectState,
    r.SubjectPostalCode,

    pt.PropType,
    tt.TransactionType,

    -- REAL ESTATE SPECIFIC TYPES
    rt.RealType,
    rst.RealSubType,
    r.RealTerm,
    r.RealAmount,

    -- FINANCIAL INFO
    r.AppraisedValue,
    r.PurchasePrice,
    r.LTV,
    r.InterestRate,

    -- LENDER INFO
    ln.CompanyName AS LenderName,
    ln.FirstName + ' ' + ln.LastName AS LenderContact,
    ln.WorkPhone1 AS LenderPhone,
    ln.Email AS LenderEmail,

    -- TITLE / APPRAISAL / ESCROW
    r.TitleCompany,
    r.TitleCoPhone,

    r.AppraisalCompany,
    r.AppraisalCoPhone,

    r.EscrowCompany,
    r.EscrowCoPhone,
    r.EscrowOfficer,
    r.EscrowOfficerEmail,
    r.EscrowOfficerPhone,
    r.EscrowNumber,

    ems.EscrowMethodSendType,

    -- COMMISSION / FEES
    r.CommReceivedDate,
    r.ActualCommKamFromEscrow,
    r.KamBrokerFee,
    r.DirectDepositFee,
    r.OtherFee,

    cpm.CommPaidMethod,
    r.CommPaidDate,

    -- BANK INFO
    ib.AccountName AS IncomingBank,
    ob.AccountName AS OutgoingBank,

    r.AmountRetainedByKam,
    r.AmountPaidToKamAgent,

    -- CASE FIELD TRANSLATIONS
    CASE 
        WHEN r.Active = 0 THEN 'Not Submitted'
        WHEN r.Active = 1 THEN 'Submitted'
        ELSE 'Unknown'
    END AS ActiveStatus,

    CASE 
        WHEN r.AppraisalDone = 0 THEN 'No'
        WHEN r.AppraisalDone = 1 THEN 'Yes'
        ELSE 'Unknown'
    END AS AppraisalDoneStatus,

    CASE 
        WHEN r.CreditReportRan = 0 THEN 'No'
        WHEN r.CreditReportRan = 1 THEN 'Yes'
        ELSE 'Unknown'
    END AS CreditReportRan,

    CASE 
        WHEN r.WhoPaidForCreditReportID IS NULL OR r.WhoPaidForCreditReportID = 0 THEN 'Not Selected'
        WHEN r.WhoPaidForCreditReportID = 1 THEN 'Agent'
        WHEN r.WhoPaidForCreditReportID = 2 THEN 'Client'
        WHEN r.WhoPaidForCreditReportID = 3 THEN 'Lender'
        ELSE 'Unknown'
    END AS WhoPaidForCreditReport,

    CASE 
        WHEN r.WhoPulledCreditReport IS NULL OR r.WhoPulledCreditReport = 0 THEN 'Not Selected'
        WHEN r.WhoPulledCreditReport = 1 THEN 'Own Account'
        WHEN r.WhoPulledCreditReport = 2 THEN 'KAM''s Account'
        ELSE 'Unknown'
    END AS WhoPulledCreditReport,

    -- REAL ESTATE SPECIFIC FIELDS
    r.TransType,
    r.PartyPresented,
    r.Price,
    r.ClientType,
    r.FinanceInfo,
    r.CARForms,
    r.NMLSNumber,

    -- HOME INSPECTION
    r.HomeInspectionName,
    CASE WHEN r.HomeInspectionDone = 1 THEN 'Yes' ELSE 'No' END AS HomeInspectionDone,
    r.HomeInspectionPhone,
    r.HomeInspectionEmail,
    r.HomeInspectionNotes,

    -- PEST INSPECTION
    r.PestInspectionName,
    CASE WHEN r.PestInspectionDone = 1 THEN 'Yes' ELSE 'No' END AS PestInspectionDone,
    r.PestInspectionPhone,
    r.PestInspectionEmail,
    r.PestInspectionNotes,

    -- TC INFO
    r.TCFlag,
    r.TC,
    r.TCFees,

    -- PAYMENT / BANK / DELIVERY
    r.ExpectedDate,
    r.PayableTo,
    r.AgentAddress,
    r.BankName,
    r.AccountName,
    r.RoutingNumber,
    r.AccountNumber,

    r.ProcessorAmount,
    r.Notes,
    r.ClearDate,
    r.CheckAmount,
    r.MailingFee

FROM [KAMFR].[dbo].[RealTransactions] r

LEFT JOIN [KAMFR].[dbo].[Users] au 
    ON au.UserID = r.AgentUserID

LEFT JOIN [KAMFR].[dbo].[Users] ad
    ON ad.UserID = r.AddedBy

LEFT JOIN [KAMFR].[dbo].[Lenders] ln
    ON ln.LenderID = r.LenderID

LEFT JOIN [KAMFR].[dbo].[PropTypes] pt
    ON pt.PropTypeID = r.PropTypeID

LEFT JOIN [KAMFR].[dbo].[TransactionTypes] tt
    ON tt.TransactionTypeID = r.TransactionTypeID

LEFT JOIN [KAMFR].[dbo].[RealTypes] rt
    ON rt.RealTypeID = r.RealTypeID

LEFT JOIN [KAMFR].[dbo].[RealSubTypes] rst
    ON rst.RealSubTypeID = r.RealSubTypeID

LEFT JOIN [KAMFR].[dbo].[EscrowMethodSendTypes] ems
    ON ems.EscrowMethodSendTypeID = r.EscrowMethodSendTypeID

LEFT JOIN [KAMFR].[dbo].[CommPaidMethods] cpm
    ON cpm.CommPaidMethodID = r.CommPaidMethodID

LEFT JOIN [KAMFR].[dbo].[BankAccount] ib
    ON ib.BankAccountID = r.IncomingBankID

LEFT JOIN [KAMFR].[dbo].[BankAccount] ob
    ON ob.BankAccountID = r.OutgoingBankID
GO
