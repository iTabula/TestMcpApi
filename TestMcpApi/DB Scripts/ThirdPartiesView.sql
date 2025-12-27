USE [KAMFR]
GO

DROP VIEW IF EXISTS [dbo].[ThirdPartiesView]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[ThirdPartiesView] AS

SELECT
    tp.ThirdPartiesID,

    -- =========================
    -- Core Third Party Info
    -- =========================
    tp.Name,
    tp.Purpose,
    tp.Website,
    tp.Username,

    -- Notes
    tp.Notes,

    -- =========================
    -- Visibility / Flags
    -- =========================
    CASE
        WHEN tp.AdminViewOnly = 1 THEN 'Yes'
        WHEN tp.AdminViewOnly = 0 THEN 'No'
        ELSE 'Unknown'
    END AS AdminViewOnly,

    -- =========================
    -- Audit Info
    -- =========================
    tp.LastUpdatedOn,
    u.FirstName + ' ' + u.LastName AS LastUpdatedBy

FROM [dbo].[ThirdParties] tp

LEFT JOIN [dbo].[Users] u
    ON u.UserID = tp.LastUpdatedBy
GO
