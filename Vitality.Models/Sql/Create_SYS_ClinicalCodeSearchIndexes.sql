-- ============================================================================
-- TEL-21 - indexes for ICD-10-CM / CPT code search (api/ClinicalCodes/searchCodes).
-- Run AFTER Create_SYS_ClinicalCodeSets.sql (TEL-19). Idempotent: safe to re-run.
--
-- No SQL Server Full-Text Search: it is an optional server feature that is not
-- guaranteed on the target instance, and LocalDB cannot run it at all.
--
-- A search first resolves the release in force on the requested date
-- (IX_SYS_CodeSetVersion_System_Effective, from TEL-19), then reads only that
-- release's rows. This index leads on the release and covers every column the
-- search reads, so the whole search is one range seek on (CodeSetVersionId)
-- with no lookups into the base table, however many releases are loaded.
-- Measured on LocalDB with the real FY2026 file loaded twice (196,372 rows):
-- p95 166 ms end to end across 36 representative queries.
-- CPT gets the same index; the table stays empty until CPT is licensed.
-- ============================================================================

IF OBJECT_ID(N'[dbo].[SYS_Icd10Code]', N'U') IS NULL
    RAISERROR (N'dbo.SYS_Icd10Code does not exist. Run Create_SYS_ClinicalCodeSets.sql (TEL-19) first.', 16, 1);
GO

IF OBJECT_ID(N'[dbo].[SYS_Icd10Code]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SYS_Icd10Code]') AND name = N'IX_SYS_Icd10Code_Search')
BEGIN
    CREATE INDEX [IX_SYS_Icd10Code_Search]
        ON [dbo].[SYS_Icd10Code] ([CodeSetVersionId], [Code])
        INCLUDE ([DisplayCode], [ShortDescription], [LongDescription], [IsBillable],
                 [EffectiveDate], [TerminationDate], [IsActive]);
END
GO

IF OBJECT_ID(N'[dbo].[SYS_CptCode]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SYS_CptCode]') AND name = N'IX_SYS_CptCode_Search')
BEGIN
    CREATE INDEX [IX_SYS_CptCode_Search]
        ON [dbo].[SYS_CptCode] ([CodeSetVersionId], [Code])
        INCLUDE ([ShortDescription], [LongDescription], [EffectiveDate], [TerminationDate], [IsActive]);
END
GO

-- Rollback.
-- DROP INDEX IF EXISTS [IX_SYS_CptCode_Search] ON [dbo].[SYS_CptCode];
-- GO
-- DROP INDEX IF EXISTS [IX_SYS_Icd10Code_Search] ON [dbo].[SYS_Icd10Code];
-- GO
