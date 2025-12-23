USE [KAMFR]
GO

/****** Object:  View [dbo].[LoanTransactionsView]    Script Date: 12/16/2025 10:24:13 PM ******/
DROP VIEW [dbo].[LoanTransactionsView]
GO

/****** Object:  View [dbo].[LoanTransactionsView]    Script Date: 12/16/2025 10:24:13 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE VIEW [dbo].[LoanTransactionsView] AS

SELECT 
    l.LoanTransID, 
    l.ActualClosedDate, 
    l.DateAdded, 
    a.FirstName + ' ' + a.LastName as AgentName, 
    ad.FirstName + ' ' + a.LastName as AddedBy, 
    l.BorrowerFirstName, 
    l.BorrowerLastName, 
    l.BorrowerPhone, 
    l.BorrowerEmail, 
    l.SubjectAddress, 
    l.SubjectCity, 
    l.SubjectState, 
    l.SubjectPostalCode,
    p.PropType, 
    t.TransactionType, 
    m.MortgageType, 
    b.BrokeringType,
    lt.LoanType, 
    l.LoanTerm, 
    l.LoanAmount, 
    l.AppraisedValue, 
    l.AppraisalCompany, 
    l.AppraisalCoPhone, 
    l.LTV, 
    l.InterestRate, 
    l.TitleCompany, 
    l.TitleCoPhone, 
    l.CreditScore,
    l.EscrowCompany, 
    l.EscrowCoPhone, 
    l.EscrowOfficer, 
    l.EscrowOfficerEmail, 
    l.EscrowOfficerPhone, 
    l.EscrowNumber, 
    e.EscrowMethodSendType,
    l.CommReceivedDate, 
    l.ActualCommKamFromEscrow, 
    l.KamBrokerFee, 
    c.CommPaidMethod, 
    l.CommPaidDate, 
    ib.AccountName as IncomingBank, 
    ob.AccountName as OutgoingBank, 
    l.AmountRetainedByKam, 
    l.AmountPaidToKamAgent, 
    l.OtherFee, 
    l.DirectDepositFee,
	ln.CompanyName As LenderName,
	ln.FirstName + ' ' + ln.LastName As LenderContact,
	ln.WorkPhone1 AS LenderPhone,
	ln.Email AS LenderEmail,
    -- CASE FIELDS
    CASE 
        WHEN l.Active = 0 THEN 'Not Submitted'
        WHEN l.Active = 1 THEN 'Submitted'
        ELSE 'Unknown'
    END AS Active,

    CASE 
        WHEN l.AppraisalDone = 0 THEN 'No'
        WHEN l.AppraisalDone = 1 THEN 'Yes'
        ELSE 'Unknown'
    END AS AppraisalDone,

    CASE 
        WHEN l.CreditReportRan = 0 THEN 'No'
        WHEN l.CreditReportRan = 1 THEN 'Yes'
        ELSE 'Unknown'
    END AS CreditReportRan,

    CASE 
        WHEN l.WhoPaidForCreditReportID IS NULL OR l.WhoPaidForCreditReportID = 0 THEN 'Not Selected'
        WHEN l.WhoPaidForCreditReportID = 1 THEN 'Agent'
        WHEN l.WhoPaidForCreditReportID = 2 THEN 'Borrower'
        WHEN l.WhoPaidForCreditReportID = 3 THEN 'Lender'
        ELSE 'Unknown'
    END AS WhoPaidForCreditReport,

    CASE 
        WHEN l.WhoPulledCreditReport IS NULL OR l.WhoPulledCreditReport = 0 THEN 'Not Selected'
        WHEN l.WhoPulledCreditReport = 1 THEN 'Own Account'
        WHEN l.WhoPulledCreditReport = 2 THEN 'KAM''s Account'
        ELSE 'Unknown'
    END AS WhoPulledCreditReport

FROM [KAMFR].[dbo].[LoanTransactions] AS l
LEFT JOIN [KAMFR].[dbo].[Users] AS a ON a.UserID = l.AgentUserID
LEFT JOIN [KAMFR].[dbo].[Lenders] AS ln ON ln.LenderID = l.LenderID
LEFT JOIN [KAMFR].[dbo].[PropTypes] AS p ON p.PropTypeID = l.PropTypeID
LEFT JOIN [KAMFR].[dbo].[TransactionTypes] AS t ON t.TransactionTypeID = l.TransactionTypeID
LEFT JOIN [KAMFR].[dbo].[LoanTypes] AS lt ON lt.LoanTypeID = l.LoanTypeID
LEFT JOIN [KAMFR].[dbo].[EscrowMethodSendTypes] AS e ON e.EscrowMethodSendTypeID = l.EscrowMethodSendTypeID
LEFT JOIN [KAMFR].[dbo].[CommPaidMethods] AS c ON c.CommPaidMethodID = l.CommPaidMethodID
LEFT JOIN [KAMFR].[dbo].[Users] AS ad ON ad.UserID = l.AddedBy
LEFT JOIN [KAMFR].[dbo].[LKP_MortgageType] AS m ON m.MortgageTypeID = l.MortgageTypeID
LEFT JOIN [KAMFR].[dbo].[LKP_BrokeringType] AS b ON b.BrokeringTypeID = l.BrokeringTypeID
LEFT JOIN [KAMFR].[dbo].[BankAccount] AS ib ON ib.BankAccountID = l.IncomingBankID
LEFT JOIN [KAMFR].[dbo].[BankAccount] AS ob ON ob.BankAccountID = l.OutgoingBankID
GO


