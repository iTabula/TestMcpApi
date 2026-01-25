USE [KAMFR]
GO

/****** Object:  Table [dbo].[VapiCalls]    Script Date: 1/24/2026 2:15:27 PM ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VapiCalls]') AND type in (N'U'))
DROP TABLE IF EXISTS [dbo].[VapiCalls]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[VapiCalls](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[CallId] [nvarchar](250) NOT NULL,
	[Phone] [nvarchar](50) NULL,
	[UserId] [int] NULL,
	[UserRole] [nvarchar](50) NULL,
	[CreatedOn] [datetime] NOT NULL,
	[LastUpdatedOn] [datetime] NOT NULL,
	[IsAuthenticated] [smallint] NOT NULL,
 CONSTRAINT [PK_VapiCalls] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
-- Add unique constraint here:
 CONSTRAINT [UQ_VapiCalls_CallId] UNIQUE NONCLUSTERED 
(
	[CallId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO



