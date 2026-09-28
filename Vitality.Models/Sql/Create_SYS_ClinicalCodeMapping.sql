-- ============================================================================
-- TEL-20 - Category, Service and Package mapping to ICD-10 / CPT codes.
-- Style follows Vitality.Models/Sql/Create_SYS_ClinicalCodeSets.sql (TEL-19).
-- Target database compatibility level is 150. Idempotent: safe to re-run.
--
-- One row is one code attached to one target. A target is a Category
-- (dbo.PD_Category), a Service (dbo.SYS_Product) or a Package (dbo.PD_Bundle);
-- the existing product, bundle and invoicing model is extended by pointing at
-- it, not by restating it here.
--
-- TargetId is polymorphic, so it carries no foreign key - which table it means
-- depends on TargetType. The repository resolves and validates the target
-- before writing. The code side is the opposite: it does carry foreign keys,
-- and they are deliberately ON DELETE NO ACTION so that deleting a reference
-- code that a mapping uses fails instead of quietly cascading the mapping away.
--
-- Keying: the durable identity of a mapping is (TargetType, TargetId,
-- CodeSystem, Code). Reference rows are per release, so a mapping keyed on
-- Icd10CodeId alone would stop resolving as soon as the next fiscal year was
-- imported. Icd10CodeId / CptCodeId record which release row the mapping was
-- authored against and carry the delete protection above.
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SYS_ClinicalCodeMapping]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SYS_ClinicalCodeMapping] (
        [ClinicalCodeMappingId] BIGINT IDENTITY(1,1) NOT NULL,

        -- 'Category' -> PD_Category.CategoryId
        -- 'Service'  -> SYS_Product.ProductId
        -- 'Package'  -> PD_Bundle.BundleId
        [TargetType]            NVARCHAR(16)  NOT NULL,
        [TargetId]              BIGINT        NOT NULL,

        -- 'ICD10CM' or 'CPT', matching SYS_CodeSetVersion.CodeSystem.
        [CodeSystem]            NVARCHAR(16)  NOT NULL,

        -- As published, without the dot: 'E1165'. Widest of the two code
        -- columns (ICD-10-CM is NVARCHAR(8), CPT is NVARCHAR(5)).
        [Code]                  NVARCHAR(8)   NOT NULL,

        -- The release row the mapping was authored against. Exactly one is set,
        -- and it must agree with CodeSystem - see CK_..._OneCode below.
        [Icd10CodeId]           BIGINT        NULL,
        [CptCodeId]             BIGINT        NULL,

        -- Raised when an import terminates a code that was already mapped. A
        -- mapping to an already terminated code is refused outright, so this
        -- only ever describes a mapping that was valid when it was made.
        [NeedsReview]           BIT           NOT NULL DEFAULT 0,
        [ReviewReason]          NVARCHAR(200) NULL,

        [IsActive]              BIT           NOT NULL DEFAULT 1,
        [CreatedBy]             BIGINT        NULL,
        [CreatedDate]           DATETIME      NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy]            BIGINT        NULL,
        [ModifiedDate]          DATETIME      NULL,

        CONSTRAINT [PK_SYS_ClinicalCodeMapping] PRIMARY KEY ([ClinicalCodeMappingId]),

        CONSTRAINT [CK_SYS_ClinicalCodeMapping_TargetType]
            CHECK ([TargetType] IN ('Category','Service','Package')),

        CONSTRAINT [CK_SYS_ClinicalCodeMapping_CodeSystem]
            CHECK ([CodeSystem] IN ('ICD10CM','CPT')),

        -- One code reference, and the right one for the code system.
        CONSTRAINT [CK_SYS_ClinicalCodeMapping_OneCode]
            CHECK (([CodeSystem] = 'ICD10CM' AND [Icd10CodeId] IS NOT NULL AND [CptCodeId] IS NULL)
                OR ([CodeSystem] = 'CPT'     AND [CptCodeId]   IS NOT NULL AND [Icd10CodeId] IS NULL)),

        -- NO ACTION is the point, not an oversight: a DELETE of a reference code
        -- that a mapping uses must fail. TEL-20 acceptance criterion 6.
        CONSTRAINT [FK_SYS_ClinicalCodeMapping_SYS_Icd10Code]
            FOREIGN KEY ([Icd10CodeId]) REFERENCES [dbo].[SYS_Icd10Code] ([Icd10CodeId])
            ON DELETE NO ACTION,

        CONSTRAINT [FK_SYS_ClinicalCodeMapping_SYS_CptCode]
            FOREIGN KEY ([CptCodeId]) REFERENCES [dbo].[SYS_CptCode] ([CptCodeId])
            ON DELETE NO ACTION
    );

    -- One mapping of a code to a target. Keyed on the code string, so it holds
    -- across releases. Rows are deactivated rather than deleted, so this covers
    -- inactive rows too and re-adding a removed mapping revives the same row.
    CREATE UNIQUE INDEX [UX_SYS_ClinicalCodeMapping_Target_Code]
        ON [dbo].[SYS_ClinicalCodeMapping] ([TargetType], [TargetId], [CodeSystem], [Code]);

    -- "the codes mapped to this Category / Service / Package" - the read the
    -- encounter screen makes every time a service is selected.
    CREATE INDEX [IX_SYS_ClinicalCodeMapping_Target]
        ON [dbo].[SYS_ClinicalCodeMapping] ([TargetType], [TargetId])
        INCLUDE ([CodeSystem], [Code], [Icd10CodeId], [CptCodeId], [NeedsReview], [IsActive]);

    -- "is this reference code in use by any mapping" - the check that has to be
    -- cheap for the removal guard to be usable on a 70,000 row code set.
    CREATE INDEX [IX_SYS_ClinicalCodeMapping_Icd10CodeId]
        ON [dbo].[SYS_ClinicalCodeMapping] ([Icd10CodeId]) WHERE [Icd10CodeId] IS NOT NULL;

    CREATE INDEX [IX_SYS_ClinicalCodeMapping_CptCodeId]
        ON [dbo].[SYS_ClinicalCodeMapping] ([CptCodeId]) WHERE [CptCodeId] IS NOT NULL;

    -- The review queue: every mapping an import has flagged, across all targets.
    CREATE INDEX [IX_SYS_ClinicalCodeMapping_NeedsReview]
        ON [dbo].[SYS_ClinicalCodeMapping] ([NeedsReview]) WHERE [NeedsReview] = 1;
END
GO

-- Rollback.
-- IF OBJECT_ID(N'[dbo].[SYS_ClinicalCodeMapping]', N'U') IS NOT NULL DROP TABLE [dbo].[SYS_ClinicalCodeMapping];
-- GO
