/* =============================================================================
   RBAC schema and seed data
   TeleHealth / Vitality  -  Role-Based Access Control

   WHAT THIS DOES
     1. Adds role metadata columns to the existing dbo.LK_Roles lookup table.
     2. Creates dbo.SYS_Permission      - the permission catalog (71 codes).
     3. Creates dbo.SYS_RolePermission  - the role -> permission junction.
     4. Seeds the 71 permission codes and 148 role grants, reproducing the
        permissions that were previously hardcoded in the Angular
        PermissionsService, so no existing user's access changes.

   SAFETY
     - Idempotent. Safe to run repeatedly; re-running only refreshes display
       metadata and adds anything missing.
     - Purely additive. No DROP, no DELETE, no column removal, no data loss.
     - Existing rows in LK_Roles keep their RoleId and RoleName untouched.
     - Wrapped in a transaction: any failure rolls the whole thing back.

   HOW TO RUN
     Visual Studio: SQL Server Object Explorer -> right-click the database ->
     New Query -> paste -> Ctrl+Shift+E.  Or:
       sqlcmd -S <server> -d <database> -i Create_RBAC_Tables_And_Seed.sql

   This project has no EF Core migrations - the schema is managed out of band,
   the same way Create_PT_PatientTreatmentSoapNote.sql is. Run this by hand
   against each environment (local, staging, production).
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* -----------------------------------------------------------------------------
   0. Preconditions
   -------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.LK_Roles', 'U') IS NULL
BEGIN
    RAISERROR (N'ABORTED: dbo.LK_Roles does not exist. This script expects the existing role lookup table.', 16, 1);
    SET NOEXEC ON;
END
GO

/* dbo.LK_Roles.RoleId is an IDENTITY column, so the database assigns ids for new
   custom roles and RolesRepo never sets one. The only place an explicit RoleId is
   written is section 2 below, seeding the seven built-in roles at their fixed
   enum-matching values - and that is wrapped in IDENTITY_INSERT.

   Reported here rather than assumed, because the C# side depends on it: if this ever
   prints "not identity", MainContext.Rbac.cs needs
   `entity.Property(e => e.RoleId).ValueGeneratedNever()` and RolesRepo needs to
   allocate ids itself. */
SELECT CASE WHEN COLUMNPROPERTY(OBJECT_ID('dbo.LK_Roles'), 'RoleId', 'IsIdentity') = 1
            THEN N'dbo.LK_Roles.RoleId is IDENTITY - matches what the RBAC code expects.'
            ELSE N'WARNING: dbo.LK_Roles.RoleId is NOT an identity column. Report this - the C# insert path assumes it is.'
       END AS RoleId_Identity_Check;
GO

BEGIN TRANSACTION;
GO

/* -----------------------------------------------------------------------------
   1. Role metadata on the existing lookup table

   LK_Roles today is just (RoleId, RoleName). RBAC needs a description, a
   system-role flag so the seven built-in roles cannot be deleted or renamed,
   and the audit columns the rest of this schema uses. All columns are added
   nullable or with a default, so existing rows stay valid.
   -------------------------------------------------------------------------- */
IF COL_LENGTH('dbo.LK_Roles', 'RoleDescription') IS NULL
    ALTER TABLE dbo.LK_Roles ADD RoleDescription nvarchar(500) NULL;
GO
IF COL_LENGTH('dbo.LK_Roles', 'IsSystemRole') IS NULL
    ALTER TABLE dbo.LK_Roles ADD IsSystemRole bit NOT NULL CONSTRAINT DF_LK_Roles_IsSystemRole DEFAULT (0);
GO
IF COL_LENGTH('dbo.LK_Roles', 'IsDefault') IS NULL
    ALTER TABLE dbo.LK_Roles ADD IsDefault bit NOT NULL CONSTRAINT DF_LK_Roles_IsDefault DEFAULT (0);
GO
IF COL_LENGTH('dbo.LK_Roles', 'IsActive') IS NULL
    ALTER TABLE dbo.LK_Roles ADD IsActive bit NOT NULL CONSTRAINT DF_LK_Roles_IsActive DEFAULT (1);
GO
IF COL_LENGTH('dbo.LK_Roles', 'IsDeleted') IS NULL
    ALTER TABLE dbo.LK_Roles ADD IsDeleted bit NOT NULL CONSTRAINT DF_LK_Roles_IsDeleted DEFAULT (0);
GO
IF COL_LENGTH('dbo.LK_Roles', 'CreatedBy') IS NULL
    ALTER TABLE dbo.LK_Roles ADD CreatedBy bigint NULL;
GO
IF COL_LENGTH('dbo.LK_Roles', 'CreatedDateUtc') IS NULL
    ALTER TABLE dbo.LK_Roles ADD CreatedDateUtc datetime2(3) NULL CONSTRAINT DF_LK_Roles_CreatedDateUtc DEFAULT (sysutcdatetime());
GO
IF COL_LENGTH('dbo.LK_Roles', 'UpdatedBy') IS NULL
    ALTER TABLE dbo.LK_Roles ADD UpdatedBy bigint NULL;
GO
IF COL_LENGTH('dbo.LK_Roles', 'UpdatedDateUtc') IS NULL
    ALTER TABLE dbo.LK_Roles ADD UpdatedDateUtc datetime2(3) NULL;
GO

/* -----------------------------------------------------------------------------
   2. The seven built-in roles

   RoleIds 1-7 mirror Vitality.Models.Enums.UserRole and are referenced directly
   by [AuthorizeRoles(UserRole.X)] attributes across the API, so they are marked
   IsSystemRole = 1: renaming or deleting them is refused by RolesRepo.
   RoleName is only set when the row is missing - an existing name is never
   overwritten, in case an environment has customised it.
   -------------------------------------------------------------------------- */
/* Two steps, because RoleId is an IDENTITY column.

   Step A updates the seven built-in rows that already exist - which is the normal
   case, since RoleIds 1-7 have been in use since before RBAC.
   Step B inserts any that are genuinely missing, and only then does it need
   IDENTITY_INSERT to write the fixed id. On a healthy database it inserts nothing. */

DECLARE @SystemRoles TABLE (RoleId int PRIMARY KEY, RoleName nvarchar(250), RoleDescription nvarchar(500));

INSERT INTO @SystemRoles (RoleId, RoleName, RoleDescription)
VALUES
    (1, N'Super Admin',      N'Platform owner. Implicitly holds every permission.'),
    (2, N'Global Admin',     N'Manages clinics, catalog, subscriptions and users across the platform.'),
    (3, N'Clinic Admin',     N'Manages a single clinic: its users, patients, schedule and billing.'),
    (4, N'Provider',         N'Clinical user: appointments, treatments, prescriptions and patients.'),
    (5, N'Customer Support', N'Read-only support access.'),
    (6, N'Patient',          N'Patient portal: own appointments, treatments, orders and payments.'),
    (7, N'Tech Support',     N'Handles support tickets.');

/* Step A - flag the existing built-in rows. RoleName is never overwritten, in case an
   environment has customised it; only the description is filled in when absent. */
UPDATE  r
SET     r.IsSystemRole    = 1,
        r.IsActive        = 1,
        r.IsDeleted       = 0,
        r.RoleDescription = COALESCE(r.RoleDescription, s.RoleDescription)
FROM    dbo.LK_Roles r
JOIN    @SystemRoles s ON s.RoleId = r.RoleId;

/* Step B - insert only what is missing. */
IF EXISTS (SELECT 1 FROM @SystemRoles s
           WHERE NOT EXISTS (SELECT 1 FROM dbo.LK_Roles r WHERE r.RoleId = s.RoleId))
BEGIN
    SET IDENTITY_INSERT dbo.LK_Roles ON;

    INSERT INTO dbo.LK_Roles (RoleId, RoleName, RoleDescription, IsSystemRole, IsDefault, IsActive, IsDeleted)
    SELECT s.RoleId, s.RoleName, s.RoleDescription, 1, 0, 1, 0
    FROM   @SystemRoles s
    WHERE  NOT EXISTS (SELECT 1 FROM dbo.LK_Roles r WHERE r.RoleId = s.RoleId);

    SET IDENTITY_INSERT dbo.LK_Roles OFF;
END
GO

/* Any other pre-existing role rows are left alone, but must not look like
   system roles and must have usable flag values. */
UPDATE dbo.LK_Roles
SET    IsSystemRole = 0
WHERE  RoleId NOT IN (1, 2, 3, 4, 5, 6, 7)
  AND  IsSystemRole = 1;
GO

/* -----------------------------------------------------------------------------
   3. dbo.SYS_Permission  -  the permission catalog

   One row per permission code. ModuleKey/ModuleName/ActionName exist so the
   role editor can render the catalog as a grouped checkbox tree, the way the
   reference project's Module/SubModule/Screen/ScreenAction tree does - the
   grouping here is derived from the code prefix instead of four extra tables.
   -------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.SYS_Permission', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SYS_Permission
    (
        PermissionId    int            IDENTITY(1,1) NOT NULL,
        PermissionCode  nvarchar(100)  NOT NULL,
        ModuleKey       nvarchar(50)   NOT NULL,
        ModuleName      nvarchar(100)  NOT NULL,
        ActionName      nvarchar(100)  NOT NULL,
        Description     nvarchar(500)  NULL,
        DisplayOrder    int            NOT NULL CONSTRAINT DF_SYS_Permission_DisplayOrder DEFAULT (0),
        IsActive        bit            NOT NULL CONSTRAINT DF_SYS_Permission_IsActive     DEFAULT (1),
        CreatedDateUtc  datetime2(3)   NOT NULL CONSTRAINT DF_SYS_Permission_CreatedDateUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_SYS_Permission        PRIMARY KEY CLUSTERED (PermissionId),
        CONSTRAINT UQ_SYS_Permission_Code   UNIQUE (PermissionCode)
    );

    CREATE INDEX IX_SYS_Permission_ModuleKey ON dbo.SYS_Permission (ModuleKey, DisplayOrder);
END
GO

/* -----------------------------------------------------------------------------
   4. dbo.SYS_RolePermission  -  role -> permission grants

   Leaf-level grants only, exactly like the reference project's
   RoleScreenPermission table: a role either holds a permission code or it does
   not. Module-level "checked" state in the editor is a rollup computed from
   these rows, never stored.
   -------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.SYS_RolePermission', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SYS_RolePermission
    (
        RolePermissionId bigint       IDENTITY(1,1) NOT NULL,
        RoleId           int          NOT NULL,
        PermissionId     int          NOT NULL,
        CreatedBy        bigint       NULL,
        CreatedDateUtc   datetime2(3) NOT NULL CONSTRAINT DF_SYS_RolePermission_CreatedDateUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_SYS_RolePermission PRIMARY KEY CLUSTERED (RolePermissionId),
        CONSTRAINT UQ_SYS_RolePermission_Role_Permission UNIQUE (RoleId, PermissionId),
        CONSTRAINT FK_SYS_RolePermission_LK_Roles
            FOREIGN KEY (RoleId)       REFERENCES dbo.LK_Roles (RoleId),
        CONSTRAINT FK_SYS_RolePermission_SYS_Permission
            FOREIGN KEY (PermissionId) REFERENCES dbo.SYS_Permission (PermissionId) ON DELETE CASCADE
    );

    /* Permission resolution always filters by RoleId, so this covers the hot path. */
    CREATE INDEX IX_SYS_RolePermission_RoleId ON dbo.SYS_RolePermission (RoleId) INCLUDE (PermissionId);
END
GO

/* -----------------------------------------------------------------------------
   5. Seed the permission catalog
   -------------------------------------------------------------------------- */
-- ---- 71 permission codes, generated from the Angular PermissionsService map. ----
-- Codes are reproduced EXACTLY, misspellings included ('avilability', 'questionnaier'),
-- because the frontend compares them as literal strings.
;WITH src (PermissionCode, ModuleKey, ModuleName, ActionName, DisplayOrder) AS (
    SELECT * FROM (VALUES
        (N'dashboard_admin', N'dashboard', N'Dashboard', N'Admin View', 1000),
        (N'dashboard_clinic', N'dashboard', N'Dashboard', N'Clinic View', 1001),
        (N'dashboard_customer', N'dashboard', N'Dashboard', N'Customer View', 1002),
        (N'dashboard_patient', N'dashboard', N'Dashboard', N'Patient View', 1003),
        (N'dashboard_provider', N'dashboard', N'Dashboard', N'Provider View', 1004),
        (N'facility_add', N'facility', N'Clinics', N'Add', 2005),
        (N'facility_delete', N'facility', N'Clinics', N'Delete', 2006),
        (N'facility_edit', N'facility', N'Clinics', N'Edit', 2007),
        (N'facility_view', N'facility', N'Clinics', N'View', 2008),
        (N'f_edit', N'f', N'Clinic Info', N'Edit', 3009),
        (N'f_view', N'f', N'Clinic Info', N'View', 3010),
        (N'calendar_view', N'calendar', N'Calendar', N'View', 4011),
        (N'appointment_delete', N'appointment', N'Appointments', N'Delete', 5012),
        (N'appointment_edit', N'appointment', N'Appointments', N'Edit', 5013),
        (N'appointment_view', N'appointment', N'Appointments', N'View', 5014),
        (N'avilability_add', N'avilability', N'Availability', N'Add', 6015),
        (N'avilability_delete', N'avilability', N'Availability', N'Delete', 6016),
        (N'avilability_edit', N'avilability', N'Availability', N'Edit', 6017),
        (N'avilability_view', N'avilability', N'Availability', N'View', 6018),
        (N'avilability_slot_delete', N'avilability_slot', N'Availability Slots', N'Delete', 7019),
        (N'avilability_slot_edit', N'avilability_slot', N'Availability Slots', N'Edit', 7020),
        (N'avilability_slot_view', N'avilability_slot', N'Availability Slots', N'View', 7021),
        (N'patient_add', N'patient', N'Patients', N'Add', 8022),
        (N'patient_delete', N'patient', N'Patients', N'Delete', 8023),
        (N'patient_edit', N'patient', N'Patients', N'Edit', 8024),
        (N'patient_view', N'patient', N'Patients', N'View', 8025),
        (N'pt_view', N'pt', N'Patient Portal', N'View', 9026),
        (N'user_add', N'user', N'Users', N'Add', 10027),
        (N'user_delete', N'user', N'Users', N'Delete', 10028),
        (N'user_edit', N'user', N'Users', N'Edit', 10029),
        (N'user_view', N'user', N'Users', N'View', 10030),
        (N'user_management', N'user_management', N'User Management', N'Access', 11031),
        (N'product_category_add', N'product_category', N'Product Categories', N'Add', 12032),
        (N'product_category_delete', N'product_category', N'Product Categories', N'Delete', 12033),
        (N'product_category_edit', N'product_category', N'Product Categories', N'Edit', 12034),
        (N'product_category_view', N'product_category', N'Product Categories', N'View', 12035),
        (N'product_add', N'product', N'Products', N'Add', 13036),
        (N'product_delete', N'product', N'Products', N'Delete', 13037),
        (N'product_edit', N'product', N'Products', N'Edit', 13038),
        (N'product_view', N'product', N'Products', N'View', 13039),
        (N'pharmacy_add', N'pharmacy', N'Pharmacies', N'Add', 14040),
        (N'pharmacy_delete', N'pharmacy', N'Pharmacies', N'Delete', 14041),
        (N'pharmacy_edit', N'pharmacy', N'Pharmacies', N'Edit', 14042),
        (N'pharmacy_view', N'pharmacy', N'Pharmacies', N'View', 14043),
        (N'questionnaier_add', N'questionnaier', N'Questionnaires', N'Add', 15044),
        (N'questionnaier_delete', N'questionnaier', N'Questionnaires', N'Delete', 15045),
        (N'questionnaier_edit', N'questionnaier', N'Questionnaires', N'Edit', 15046),
        (N'questionnaier_list_edit', N'questionnaier', N'Questionnaires', N'List Edit', 15047),
        (N'questionnaier_view', N'questionnaier', N'Questionnaires', N'View', 15048),
        (N'supportTicket_add', N'supportTicket', N'Support Tickets', N'Add', 16049),
        (N'supportTicket_delete', N'supportTicket', N'Support Tickets', N'Delete', 16050),
        (N'supportTicket_edit', N'supportTicket', N'Support Tickets', N'Edit', 16051),
        (N'supportTicket_view', N'supportTicket', N'Support Tickets', N'View', 16052),
        (N'subscription_plan_add', N'subscription_plan', N'Subscription Plans', N'Add', 17053),
        (N'subscription_plan_delete', N'subscription_plan', N'Subscription Plans', N'Delete', 17054),
        (N'subscription_plan_edit', N'subscription_plan', N'Subscription Plans', N'Edit', 17055),
        (N'subscription_plan_view', N'subscription_plan', N'Subscription Plans', N'View', 17056),
        (N'treatment_more_button', N'treatment', N'Treatments', N'More Actions', 18057),
        (N'treatment_update', N'treatment', N'Treatments', N'Update', 18058),
        (N'treatment_view', N'treatment', N'Treatments', N'View', 18059),
        (N'treatment_patient_edit', N'treatment_patient', N'Patient Treatments', N'Edit', 19060),
        (N'treatment_patient_view', N'treatment_patient', N'Patient Treatments', N'View', 19061),
        (N'prescription_edit', N'prescription', N'Prescriptions', N'Edit', 20062),
        (N'prescription_refill_button', N'prescription', N'Prescriptions', N'Refill', 20063),
        (N'prescription_view', N'prescription', N'Prescriptions', N'View', 20064),
        (N'prescription_timeLine_comment', N'prescription_timeLine', N'Prescription Timeline', N'Comment', 21065),
        (N'order_edit', N'order', N'Orders', N'Edit', 22066),
        (N'order_view', N'order', N'Orders', N'View', 22067),
        (N'payment_update', N'payment', N'Payments', N'Update', 23068),
        (N'payment_view', N'payment', N'Payments', N'View', 23069),
        (N'branding-view', N'branding', N'Branding', N'View', 24070)
    ) v (PermissionCode, ModuleKey, ModuleName, ActionName, DisplayOrder)
)
MERGE dbo.SYS_Permission AS tgt
USING src AS s ON tgt.PermissionCode = s.PermissionCode
WHEN MATCHED THEN UPDATE SET
        tgt.ModuleKey    = s.ModuleKey,
        tgt.ModuleName   = s.ModuleName,
        tgt.ActionName   = s.ActionName,
        tgt.DisplayOrder = s.DisplayOrder
WHEN NOT MATCHED BY TARGET THEN
    INSERT (PermissionCode, ModuleKey, ModuleName, ActionName, DisplayOrder)
    VALUES (s.PermissionCode, s.ModuleKey, s.ModuleName, s.ActionName, s.DisplayOrder);

GO

/* -----------------------------------------------------------------------------
   6. Seed role grants
   -------------------------------------------------------------------------- */
-- ---- Role -> permission grants, reproducing the hardcoded Angular map exactly, ----
-- so moving to server-driven permissions changes no existing user's access.
-- Super Admin (RoleId 1) is deliberately NOT seeded: PermissionResolver grants it
-- every permission implicitly, matching how AuthorizeRolesAttribute already treats
-- RoleId 1 as the platform owner (it is exempt from the facility-disabled check).

-- RoleId 2 = Global Admin: 59 permissions
INSERT INTO dbo.SYS_RolePermission (RoleId, PermissionId)
SELECT 2, p.PermissionId FROM dbo.SYS_Permission p
WHERE  p.PermissionCode IN (
           N'dashboard_admin',
           N'facility_view',
           N'facility_add',
           N'facility_edit',
           N'facility_delete',
           N'calendar_view',
           N'appointment_view',
           N'appointment_delete',
           N'appointment_edit',
           N'avilability_view',
           N'avilability_add',
           N'avilability_edit',
           N'avilability_delete',
           N'avilability_slot_view',
           N'avilability_slot_edit',
           N'avilability_slot_delete',
           N'patient_view',
           N'patient_add',
           N'patient_edit',
           N'patient_delete',
           N'user_view',
           N'user_add',
           N'user_edit',
           N'user_delete',
           N'product_category_view',
           N'product_category_add',
           N'product_category_edit',
           N'product_category_delete',
           N'product_view',
           N'product_add',
           N'product_edit',
           N'product_delete',
           N'pharmacy_view',
           N'pharmacy_add',
           N'pharmacy_edit',
           N'pharmacy_delete',
           N'questionnaier_view',
           N'questionnaier_add',
           N'questionnaier_edit',
           N'questionnaier_delete',
           N'questionnaier_list_edit',
           N'supportTicket_view',
           N'supportTicket_edit',
           N'supportTicket_delete',
           N'subscription_plan_view',
           N'subscription_plan_add',
           N'subscription_plan_edit',
           N'subscription_plan_delete',
           N'treatment_view',
           N'treatment_update',
           N'treatment_patient_view',
           N'treatment_patient_edit',
           N'treatment_more_button',
           N'prescription_view',
           N'prescription_edit',
           N'prescription_refill_button',
           N'order_view',
           N'order_edit',
           N'user_management'
       )
  AND NOT EXISTS (SELECT 1 FROM dbo.SYS_RolePermission rp
                  WHERE rp.RoleId = 2 AND rp.PermissionId = p.PermissionId);

-- RoleId 3 = Clinic Admin: 44 permissions
INSERT INTO dbo.SYS_RolePermission (RoleId, PermissionId)
SELECT 3, p.PermissionId FROM dbo.SYS_Permission p
WHERE  p.PermissionCode IN (
           N'f_view',
           N'f_edit',
           N'dashboard_clinic',
           N'calendar_view',
           N'appointment_view',
           N'appointment_delete',
           N'appointment_edit',
           N'avilability_view',
           N'avilability_add',
           N'avilability_edit',
           N'avilability_delete',
           N'avilability_slot_view',
           N'avilability_slot_edit',
           N'avilability_slot_delete',
           N'patient_view',
           N'patient_add',
           N'patient_edit',
           N'patient_delete',
           N'user_view',
           N'user_add',
           N'user_edit',
           N'user_delete',
           N'treatment_view',
           N'treatment_update',
           N'treatment_patient_view',
           N'treatment_patient_edit',
           N'treatment_more_button',
           N'order_view',
           N'order_edit',
           N'product_category_view',
           N'payment_view',
           N'payment_update',
           N'prescription_view',
           N'prescription_edit',
           N'prescription_refill_button',
           N'product_view',
           N'questionnaier_view',
           N'questionnaier_add',
           N'questionnaier_edit',
           N'supportTicket_view',
           N'supportTicket_add',
           N'supportTicket_edit',
           N'supportTicket_delete',
           N'branding-view'
       )
  AND NOT EXISTS (SELECT 1 FROM dbo.SYS_RolePermission rp
                  WHERE rp.RoleId = 3 AND rp.PermissionId = p.PermissionId);

-- RoleId 4 = Provider: 33 permissions
INSERT INTO dbo.SYS_RolePermission (RoleId, PermissionId)
SELECT 4, p.PermissionId FROM dbo.SYS_Permission p
WHERE  p.PermissionCode IN (
           N'dashboard_provider',
           N'calendar_view',
           N'appointment_view',
           N'appointment_delete',
           N'appointment_edit',
           N'avilability_view',
           N'avilability_add',
           N'avilability_edit',
           N'avilability_delete',
           N'avilability_slot_view',
           N'avilability_slot_edit',
           N'avilability_slot_delete',
           N'patient_view',
           N'patient_add',
           N'patient_edit',
           N'patient_delete',
           N'treatment_view',
           N'treatment_update',
           N'treatment_patient_view',
           N'treatment_patient_edit',
           N'treatment_more_button',
           N'order_view',
           N'order_edit',
           N'payment_view',
           N'prescription_view',
           N'prescription_edit',
           N'prescription_refill_button',
           N'prescription_timeLine_comment',
           N'product_view',
           N'supportTicket_view',
           N'supportTicket_add',
           N'supportTicket_edit',
           N'supportTicket_delete'
       )
  AND NOT EXISTS (SELECT 1 FROM dbo.SYS_RolePermission rp
                  WHERE rp.RoleId = 4 AND rp.PermissionId = p.PermissionId);

-- RoleId 5 = Customer Support: 1 permissions
INSERT INTO dbo.SYS_RolePermission (RoleId, PermissionId)
SELECT 5, p.PermissionId FROM dbo.SYS_Permission p
WHERE  p.PermissionCode IN (
           N'dashboard_customer'
       )
  AND NOT EXISTS (SELECT 1 FROM dbo.SYS_RolePermission rp
                  WHERE rp.RoleId = 5 AND rp.PermissionId = p.PermissionId);

-- RoleId 6 = Patient: 8 permissions
INSERT INTO dbo.SYS_RolePermission (RoleId, PermissionId)
SELECT 6, p.PermissionId FROM dbo.SYS_Permission p
WHERE  p.PermissionCode IN (
           N'dashboard_patient',
           N'pt_view',
           N'calendar_view',
           N'appointment_view',
           N'treatment_view',
           N'order_view',
           N'payment_view',
           N'prescription_view'
       )
  AND NOT EXISTS (SELECT 1 FROM dbo.SYS_RolePermission rp
                  WHERE rp.RoleId = 6 AND rp.PermissionId = p.PermissionId);

-- RoleId 7 = Tech Support: 3 permissions
INSERT INTO dbo.SYS_RolePermission (RoleId, PermissionId)
SELECT 7, p.PermissionId FROM dbo.SYS_Permission p
WHERE  p.PermissionCode IN (
           N'supportTicket_view',
           N'supportTicket_edit',
           N'supportTicket_delete'
       )
  AND NOT EXISTS (SELECT 1 FROM dbo.SYS_RolePermission rp
                  WHERE rp.RoleId = 7 AND rp.PermissionId = p.PermissionId);
GO

COMMIT TRANSACTION;
GO

/* -----------------------------------------------------------------------------
   7. Verification  -  read the output of these three result sets
   -------------------------------------------------------------------------- */
SELECT 'Permissions in catalog' AS Check_, COUNT(*) AS Value, 71 AS Expected FROM dbo.SYS_Permission;

SELECT 'Grants per role' AS Check_,
       r.RoleId,
       r.RoleName,
       r.IsSystemRole,
       COUNT(rp.PermissionId) AS GrantedPermissions,
       CASE r.RoleId WHEN 1 THEN N'all (implicit, resolved in code)'
                     WHEN 2 THEN N'59 expected' WHEN 3 THEN N'44 expected'
                     WHEN 4 THEN N'33 expected' WHEN 5 THEN N'1 expected'
                     WHEN 6 THEN N'8 expected'  WHEN 7 THEN N'3 expected'
                     ELSE N'custom role' END AS Expected
FROM   dbo.LK_Roles r
LEFT   JOIN dbo.SYS_RolePermission rp ON rp.RoleId = r.RoleId
WHERE  r.IsDeleted = 0
GROUP  BY r.RoleId, r.RoleName, r.IsSystemRole
ORDER  BY r.RoleId;

SELECT 'Catalog by module' AS Check_, ModuleKey, ModuleName, COUNT(*) AS Permissions
FROM   dbo.SYS_Permission
GROUP  BY ModuleKey, ModuleName
ORDER  BY MIN(DisplayOrder);
GO

/* The one remaining NOEXEC user is the missing-LK_Roles precondition at the top. */
SET NOEXEC OFF;
GO
