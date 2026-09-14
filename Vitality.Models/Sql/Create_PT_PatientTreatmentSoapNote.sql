-- SOAP notes by treatment (multiple per treatment). No appointment fields.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PT_PatientTreatmentSoapNote]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PT_PatientTreatmentSoapNote] (
        [SoapNoteId] BIGINT IDENTITY(1,1) NOT NULL,
        [PatientTreatmentId] BIGINT NOT NULL,
        [PatientId] BIGINT NULL,
        [ProviderId] BIGINT NULL,
        [FacilityId] INT NULL,
        [Subjective] NVARCHAR(MAX) NULL,
        [Objective] NVARCHAR(MAX) NULL,
        [Assessment] NVARCHAR(MAX) NULL,
        [Plan] NVARCHAR(MAX) NULL,
        [AllergiesJson] NVARCHAR(MAX) NULL,
        [SignaturePath] NVARCHAR(512) NULL,
        [Signature] NVARCHAR(MAX) NULL,
        [SignedBy] NVARCHAR(256) NULL,
        [SignedAt] DATETIME NULL,
        [Status] NVARCHAR(32) NULL,
        [IsActive] BIT NULL DEFAULT 1,
        [CreatedBy] BIGINT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] BIGINT NULL,
        [ModifiedDate] DATETIME NULL,
        CONSTRAINT [PK_PT_PatientTreatmentSoapNote] PRIMARY KEY ([SoapNoteId]),
        CONSTRAINT [FK_PT_PatientTreatmentSoapNote_PT_PatientTreatment] FOREIGN KEY ([PatientTreatmentId]) REFERENCES [dbo].[PT_PatientTreatments] ([PatientTreatmentId])
    );
    CREATE INDEX [IX_PT_PatientTreatmentSoapNotes_PatientTreatmentId] ON [dbo].[PT_PatientTreatmentSoapNote] ([PatientTreatmentId]);
END
GO
