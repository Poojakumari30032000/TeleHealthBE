-- ============================================================================
-- TEL-19 - ICD-10-CM and CPT reference data.
-- Style follows Vitality.Models/Sql/Create_PT_PatientTreatmentSoapNote.sql
-- Target database compatibility level is 150. Idempotent: safe to re-run.
--
-- Versioning model: every published release of a code system (ICD-10-CM
-- FY2026, FY2027, ...) is one SYS_CodeSetVersion row, and its codes are stored
-- against that version. An encounter coded last year therefore still resolves
-- to the code and wording that were current then. Re-importing a version
-- updates its rows in place (unique on version + code), so a re-run never
-- duplicates.
--
-- CPT: schema only. CPT is licensed by the AMA and no CPT data is loaded
-- until a license is confirmed - see TEL-19.
-- ============================================================================

-- 1. One published release of a code system.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SYS_CodeSetVersion]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SYS_CodeSetVersion] (
        [CodeSetVersionId] BIGINT IDENTITY(1,1) NOT NULL,

        -- 'ICD10CM' or 'CPT'.
        [CodeSystem]       NVARCHAR(16)  NOT NULL,

        -- The publisher's name for the release, e.g. 'FY2026' or '2026'.
        [VersionLabel]     NVARCHAR(32)  NOT NULL,

        -- The window this release is in force. ICD-10-CM runs 1 October to
        -- 30 September. TerminationDate is null when no end is known; loading
        -- a later release sets it to the day before that release takes effect.
        [EffectiveDate]    DATE          NOT NULL,
        [TerminationDate]  DATE          NULL,

        [SourceFileName]   NVARCHAR(260) NULL,
        [CodeCount]        INT           NOT NULL DEFAULT 0,
        [ImportedBy]       BIGINT        NULL,
        [ImportedDate]     DATETIME      NULL,

        [IsActive]         BIT           NOT NULL DEFAULT 1,
        [CreatedBy]        BIGINT        NULL,
        [CreatedDate]      DATETIME      NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy]       BIGINT        NULL,
        [ModifiedDate]     DATETIME      NULL,

        CONSTRAINT [PK_SYS_CodeSetVersion] PRIMARY KEY ([CodeSetVersionId]),
        CONSTRAINT [CK_SYS_CodeSetVersion_CodeSystem] CHECK ([CodeSystem] IN ('ICD10CM','CPT')),
        CONSTRAINT [CK_SYS_CodeSetVersion_Dates]
            CHECK ([TerminationDate] IS NULL OR [TerminationDate] >= [EffectiveDate])
    );

    CREATE UNIQUE INDEX [UX_SYS_CodeSetVersion_System_Label]
        ON [dbo].[SYS_CodeSetVersion] ([CodeSystem], [VersionLabel]);

    -- Resolving "the release in force on date X" for a code system.
    CREATE INDEX [IX_SYS_CodeSetVersion_System_Effective]
        ON [dbo].[SYS_CodeSetVersion] ([CodeSystem], [EffectiveDate])
        INCLUDE ([TerminationDate], [IsActive]);
END
GO

-- 2. ICD-10-CM diagnosis codes, one row per code per release.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SYS_Icd10Code]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SYS_Icd10Code] (
        [Icd10CodeId]      BIGINT IDENTITY(1,1) NOT NULL,
        [CodeSetVersionId] BIGINT         NOT NULL,

        -- As published, without the dot: 'E1165'.
        [Code]             NVARCHAR(8)    NOT NULL,
        -- For display and for search by the form people type: 'E11.65'.
        [DisplayCode]      NVARCHAR(9)    NOT NULL,

        [ShortDescription] NVARCHAR(60)   NULL,
        [LongDescription]  NVARCHAR(400)  NOT NULL,

        -- The order file's header flag. A header (category) code such as 'E11'
        -- is not valid on a claim; only billable codes are.
        [IsBillable]       BIT            NOT NULL,

        -- Copied from the release so a code row answers "was this in force on
        -- date X" on its own. TerminationDate is set when a later release
        -- closes this one.
        [EffectiveDate]    DATE           NOT NULL,
        [TerminationDate]  DATE           NULL,
        [IsActive]         BIT            NOT NULL DEFAULT 1,

        [SortOrder]        INT            NULL,
        [CreatedDate]      DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedDate]     DATETIME       NULL,

        CONSTRAINT [PK_SYS_Icd10Code] PRIMARY KEY ([Icd10CodeId]),
        CONSTRAINT [FK_SYS_Icd10Code_SYS_CodeSetVersion]
            FOREIGN KEY ([CodeSetVersionId]) REFERENCES [dbo].[SYS_CodeSetVersion] ([CodeSetVersionId])
    );

    -- The upsert key, and lookup of one code within a release.
    CREATE UNIQUE INDEX [UX_SYS_Icd10Code_Version_Code]
        ON [dbo].[SYS_Icd10Code] ([CodeSetVersionId], [Code]);

    -- A code across releases, e.g. "what did E11.65 mean in FY2025".
    CREATE INDEX [IX_SYS_Icd10Code_Code]
        ON [dbo].[SYS_Icd10Code] ([Code]) INCLUDE ([CodeSetVersionId], [EffectiveDate], [TerminationDate]);
END
GO

-- 3. CPT procedure codes. Same shape; no data until licensed.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SYS_CptCode]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SYS_CptCode] (
        [CptCodeId]        BIGINT IDENTITY(1,1) NOT NULL,
        [CodeSetVersionId] BIGINT         NOT NULL,

        -- Five characters: '99213', or a Category II / III code such as '0001F'.
        [Code]             NVARCHAR(5)    NOT NULL,

        [ShortDescription] NVARCHAR(60)   NULL,
        [LongDescription]  NVARCHAR(1000) NOT NULL,

        [EffectiveDate]    DATE           NOT NULL,
        [TerminationDate]  DATE           NULL,
        [IsActive]         BIT            NOT NULL DEFAULT 1,

        [CreatedDate]      DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedDate]     DATETIME       NULL,

        CONSTRAINT [PK_SYS_CptCode] PRIMARY KEY ([CptCodeId]),
        CONSTRAINT [FK_SYS_CptCode_SYS_CodeSetVersion]
            FOREIGN KEY ([CodeSetVersionId]) REFERENCES [dbo].[SYS_CodeSetVersion] ([CodeSetVersionId])
    );

    CREATE UNIQUE INDEX [UX_SYS_CptCode_Version_Code]
        ON [dbo].[SYS_CptCode] ([CodeSetVersionId], [Code]);

    CREATE INDEX [IX_SYS_CptCode_Code]
        ON [dbo].[SYS_CptCode] ([Code]) INCLUDE ([CodeSetVersionId], [EffectiveDate], [TerminationDate]);
END
GO

-- Search indexes on descriptions (full-text or otherwise) belong to TEL-21,
-- which owns the search API and its latency target.

-- Rollback. Codes first - the FKs depend on the version table.
-- IF OBJECT_ID(N'[dbo].[SYS_CptCode]', N'U') IS NOT NULL DROP TABLE [dbo].[SYS_CptCode];
-- GO
-- IF OBJECT_ID(N'[dbo].[SYS_Icd10Code]', N'U') IS NOT NULL DROP TABLE [dbo].[SYS_Icd10Code];
-- GO
-- IF OBJECT_ID(N'[dbo].[SYS_CodeSetVersion]', N'U') IS NOT NULL DROP TABLE [dbo].[SYS_CodeSetVersion];
-- GO
