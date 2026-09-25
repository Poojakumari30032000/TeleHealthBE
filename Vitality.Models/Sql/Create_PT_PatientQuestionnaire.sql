-- ============================================================================
-- TEL-57 - patient questionnaire assignment and on-demand completion.
-- Style follows Vitality.Models/Sql/Create_PT_PatientTreatmentSoapNote.sql
-- Target database compatibility level is 150.
-- ============================================================================

-- 1. The assignment: one questionnaire given to one patient.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PT_PatientQuestionnaire]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PT_PatientQuestionnaire] (
        [PatientQuestionnaireId]   BIGINT IDENTITY(1,1) NOT NULL,
        [PatientId]                BIGINT         NOT NULL,
        [QuestionnaireId]          BIGINT         NOT NULL,

        -- Which clinic assigned it. Drives the facility-specific wording held
        -- in SYS_QuestionnaireFacilityJson, and scopes admin visibility.
        [FacilityId]               BIGINT         NULL,

        -- Optional link when the assignment belongs to a treatment, so the
        -- existing intake flow can be represented here without losing that tie.
        [PatientTreatmentId]       BIGINT         NULL,

        [Status]                   NVARCHAR(32)   NOT NULL DEFAULT 'Assigned',

        [AssignedBy]               BIGINT         NULL,
        [AssignedDate]             DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [DueDate]                  DATETIME       NULL,
        [StartedDate]              DATETIME       NULL,
        [SubmittedDate]            DATETIME       NULL,

        -- Partial progress. Today the half-finished intake lives only in the
        -- browser's localStorage, so it is lost on another device or a clear.
        [DraftJson]                NVARCHAR(MAX)  NULL,
        [DraftSavedDate]           DATETIME       NULL,

        -- The questionnaire definition as it stood when assigned. Without this
        -- an edit changes the form under a patient who is midway through it.
        [QuestionnaireJsonSnapshot] NVARCHAR(MAX) NULL,

        [Guid]                     NVARCHAR(50)   NULL,
        [IsActive]                 BIT            NULL DEFAULT 1,
        [CreatedBy]                BIGINT         NULL,
        [CreatedDate]              DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy]               BIGINT         NULL,
        [ModifiedDate]             DATETIME       NULL,

        CONSTRAINT [PK_PT_PatientQuestionnaire] PRIMARY KEY ([PatientQuestionnaireId]),
        CONSTRAINT [FK_PT_PatientQuestionnaire_PT_Patients]
            FOREIGN KEY ([PatientId])          REFERENCES [dbo].[PT_Patients] ([PatientId]),
        CONSTRAINT [FK_PT_PatientQuestionnaire_SYS_Questionnaires]
            FOREIGN KEY ([QuestionnaireId])    REFERENCES [dbo].[SYS_Questionnaires] ([QuestionnaireId]),
        CONSTRAINT [FK_PT_PatientQuestionnaire_PT_PatientTreatments]
            FOREIGN KEY ([PatientTreatmentId]) REFERENCES [dbo].[PT_PatientTreatments] ([PatientTreatmentId]),
        CONSTRAINT [CK_PT_PatientQuestionnaire_Status]
            CHECK ([Status] IN ('Assigned','InProgress','Submitted','Cancelled','Expired'))
    );

    CREATE INDEX [IX_PT_PatientQuestionnaire_PatientId_Status]
        ON [dbo].[PT_PatientQuestionnaire] ([PatientId], [Status]) INCLUDE ([AssignedDate], [SubmittedDate]);

    CREATE INDEX [IX_PT_PatientQuestionnaire_QuestionnaireId]
        ON [dbo].[PT_PatientQuestionnaire] ([QuestionnaireId]);

    CREATE INDEX [IX_PT_PatientQuestionnaire_FacilityId]
        ON [dbo].[PT_PatientQuestionnaire] ([FacilityId]);

    -- Stops the same questionnaire being assigned twice while one is still
    -- outstanding. A repeat assignment after submission is still allowed.
    CREATE UNIQUE INDEX [UX_PT_PatientQuestionnaire_OpenAssignment]
        ON [dbo].[PT_PatientQuestionnaire] ([PatientId], [QuestionnaireId])
        WHERE [Status] IN ('Assigned','InProgress') AND [IsActive] = 1;
END
GO

-- 2. The answers for an assignment.
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PT_PatientQuestionnaireAnswer]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PT_PatientQuestionnaireAnswer] (
        [PatientQuestionnaireAnswerId] BIGINT IDENTITY(1,1) NOT NULL,
        [PatientQuestionnaireId]       BIGINT        NOT NULL,

        -- Field id from the questionnaire JSON, so an answer can still be tied
        -- back to its field when the label has since been reworded.
        [FieldKey]                     NVARCHAR(128) NULL,

        [Question]                     NVARCHAR(MAX) NULL,
        [Answer]                       NVARCHAR(MAX) NULL,
        [OtherText]                    NVARCHAR(MAX) NULL,
        [Type]                         NVARCHAR(50)  NULL,
        [ConsentHtml]                  NVARCHAR(MAX) NULL,
        [DisplayOrder]                 INT           NULL,

        [CreatedBy]                    BIGINT        NULL,
        [CreatedDate]                  DATETIME      NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_PT_PatientQuestionnaireAnswer] PRIMARY KEY ([PatientQuestionnaireAnswerId]),
        CONSTRAINT [FK_PT_PatientQuestionnaireAnswer_PT_PatientQuestionnaire]
            FOREIGN KEY ([PatientQuestionnaireId])
            REFERENCES [dbo].[PT_PatientQuestionnaire] ([PatientQuestionnaireId])
    );

    CREATE INDEX [IX_PT_PatientQuestionnaireAnswer_PatientQuestionnaireId]
        ON [dbo].[PT_PatientQuestionnaireAnswer] ([PatientQuestionnaireId], [DisplayOrder]);
END
GO

-- Rollback. Answers first - the FK depends on the parent.
-- IF OBJECT_ID(N'[dbo].[PT_PatientQuestionnaireAnswer]', N'U') IS NOT NULL
--     DROP TABLE [dbo].[PT_PatientQuestionnaireAnswer];
-- GO
-- IF OBJECT_ID(N'[dbo].[PT_PatientQuestionnaire]', N'U') IS NOT NULL
--     DROP TABLE [dbo].[PT_PatientQuestionnaire];
-- GO
