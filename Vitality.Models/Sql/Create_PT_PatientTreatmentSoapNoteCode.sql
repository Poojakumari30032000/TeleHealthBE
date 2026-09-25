-- ============================================================================
-- TEL-22 - ICD-10-CM / CPT codes attached to a treatment SOAP note.
-- Style follows Vitality.Models/Sql/Create_PT_PatientTreatmentSoapNote.sql.
-- Run AFTER Create_PT_PatientTreatmentSoapNote.sql and
-- Create_SYS_ClinicalCodeSets.sql (TEL-19). Idempotent: safe to re-run.
--
-- The "encounter" TEL-22 codes against is the treatment SOAP note (decided on
-- TEL-22). One row per code on a note.
--
-- Each row stores the release it was coded against (CodeSetVersionId) and the
-- code row itself (Icd10CodeId or CptCodeId), so an old note always resolves
-- to the code and wording in force when it was coded, even after later
-- releases are loaded. Code, DisplayCode and Description are a snapshot of that
-- row, so the note reads correctly without a join.
--
-- The foreign keys to the code tables are deliberate: TEL-19's import never
-- deletes a code row (it deactivates), and a code in use on a note must not be
-- removable.
-- ============================================================================

IF OBJECT_ID(N'[dbo].[PT_PatientTreatmentSoapNote]', N'U') IS NULL
    RAISERROR (N'dbo.PT_PatientTreatmentSoapNote does not exist. Run Create_PT_PatientTreatmentSoapNote.sql first.', 16, 1);
GO
IF OBJECT_ID(N'[dbo].[SYS_CodeSetVersion]', N'U') IS NULL
    RAISERROR (N'dbo.SYS_CodeSetVersion does not exist. Run Create_SYS_ClinicalCodeSets.sql (TEL-19) first.', 16, 1);
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PT_PatientTreatmentSoapNoteCode]') AND type in (N'U'))
   AND OBJECT_ID(N'[dbo].[PT_PatientTreatmentSoapNote]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[SYS_CodeSetVersion]', N'U') IS NOT NULL
BEGIN
    CREATE TABLE [dbo].[PT_PatientTreatmentSoapNoteCode] (
        [SoapNoteCodeId]   BIGINT IDENTITY(1,1) NOT NULL,
        [SoapNoteId]       BIGINT         NOT NULL,

        -- 'ICD10CM' or 'CPT'.
        [CodeSystem]       NVARCHAR(16)   NOT NULL,
        [CodeSetVersionId] BIGINT         NOT NULL,

        -- Exactly one is set, matching CodeSystem.
        [Icd10CodeId]      BIGINT         NULL,
        [CptCodeId]        BIGINT         NULL,

        -- Snapshot of the code row: 'E1165', 'E11.65', its long description.
        [Code]             NVARCHAR(8)    NOT NULL,
        [DisplayCode]      NVARCHAR(9)    NOT NULL,
        [Description]      NVARCHAR(1000) NOT NULL,

        -- The order the provider listed them in; 1 is the primary code.
        [DisplayOrder]     INT            NOT NULL,

        [CreatedBy]        BIGINT         NULL,
        [CreatedDate]      DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy]       BIGINT         NULL,
        [ModifiedDate]     DATETIME       NULL,

        CONSTRAINT [PK_PT_PatientTreatmentSoapNoteCode] PRIMARY KEY ([SoapNoteCodeId]),
        CONSTRAINT [FK_PT_PatientTreatmentSoapNoteCode_PT_PatientTreatmentSoapNote]
            FOREIGN KEY ([SoapNoteId]) REFERENCES [dbo].[PT_PatientTreatmentSoapNote] ([SoapNoteId]),
        CONSTRAINT [FK_PT_PatientTreatmentSoapNoteCode_SYS_CodeSetVersion]
            FOREIGN KEY ([CodeSetVersionId]) REFERENCES [dbo].[SYS_CodeSetVersion] ([CodeSetVersionId]),
        CONSTRAINT [FK_PT_PatientTreatmentSoapNoteCode_SYS_Icd10Code]
            FOREIGN KEY ([Icd10CodeId]) REFERENCES [dbo].[SYS_Icd10Code] ([Icd10CodeId]),
        CONSTRAINT [FK_PT_PatientTreatmentSoapNoteCode_SYS_CptCode]
            FOREIGN KEY ([CptCodeId]) REFERENCES [dbo].[SYS_CptCode] ([CptCodeId]),
        CONSTRAINT [CK_PT_PatientTreatmentSoapNoteCode_CodeSystem]
            CHECK (([CodeSystem] = 'ICD10CM' AND [Icd10CodeId] IS NOT NULL AND [CptCodeId] IS NULL)
                OR ([CodeSystem] = 'CPT'     AND [CptCodeId] IS NOT NULL AND [Icd10CodeId] IS NULL))
    );

    -- One code once per note. Also serves "all codes on a note".
    CREATE UNIQUE INDEX [UX_PT_PatientTreatmentSoapNoteCode_Note_System_Code]
        ON [dbo].[PT_PatientTreatmentSoapNoteCode] ([SoapNoteId], [CodeSystem], [Code]);

    -- "Which notes use this code row" - for the FKs, and for later reporting.
    -- Deliberately not filtered indexes: those would require QUOTED_IDENTIFIER ON
    -- for every later write to this table, from every client.
    CREATE INDEX [IX_PT_PatientTreatmentSoapNoteCode_Icd10CodeId]
        ON [dbo].[PT_PatientTreatmentSoapNoteCode] ([Icd10CodeId]);
    CREATE INDEX [IX_PT_PatientTreatmentSoapNoteCode_CptCodeId]
        ON [dbo].[PT_PatientTreatmentSoapNoteCode] ([CptCodeId]);
END
GO

-- Rollback.
-- IF OBJECT_ID(N'[dbo].[PT_PatientTreatmentSoapNoteCode]', N'U') IS NOT NULL DROP TABLE [dbo].[PT_PatientTreatmentSoapNoteCode];
-- GO
