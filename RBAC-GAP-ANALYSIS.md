# RBAC gap analysis — TEL-36

Ticket: [TEL-36](https://poojakumarismartdata.atlassian.net/browse/TEL-36) ·
Epic: [TEL-6](https://poojakumarismartdata.atlassian.net/browse/TEL-6) (M5) ·
Source requirement: Module Summary row 2, "Role-based screen and module access"

Measured against `TeleHealthBE` and `TeleHealthFE` at `origin/development`, and
against the live `db_abe680_isohealth` schema, on 2026-09-21.

The ticket's instruction is *do not rebuild*. This document establishes what
already ships, so that only the gap is sized and implemented.

---

## 1. Summary

**The requirement is substantially met. RBAC is built, deployed and enforced.**
The gap is not missing machinery — it is uneven application of the machinery that
exists.

| Question | Answer |
|---|---|
| Is there a permission model? | Yes — deployed, 71 permissions, 148 grants, 7 roles |
| Is it enforced server-side? | Yes — every endpoint requires authentication; 118 also require a named permission |
| Is the frontend server-driven? | Yes — `GET api/Roles/myPermissions`, with a first-paint fallback |
| Is anything unprotected? | **Three controllers were anonymous. Fixed in this change.** |
| What remains? | 139 endpoints authenticate the caller but do not check their role |

---

## 2. What already exists — verified

### Database

Deployed and populated. Counted directly against the live database:

| Table | Rows | Purpose |
|---|---|---|
| `dbo.LK_Roles` | 7 | Roles, extended with `IsSystemRole`, `IsDefault`, `IsActive`, `IsDeleted`, audit columns |
| `dbo.SYS_Permission` | 71 | Permission catalog, unique on `PermissionCode`, grouped by `ModuleKey` |
| `dbo.SYS_RolePermission` | 148 | Role → permission grants, unique on `(RoleId, PermissionId)` |

Schema is applied by `Vitality.Models/Sql/Create_RBAC_Tables_And_Seed.sql`
(idempotent, additive). There are no EF Core migrations in this repository; the
model is mapped in `MainContext.Rbac.cs` via the `OnModelCreatingPartial` hook so
a re-scaffold of `MainContext.cs` cannot wipe it.

User → role is the pre-existing chain `SYS_UserDetails.LoginId` →
`SYS_Logins.RoleId` → `LK_Roles.RoleId`. One role per user; there is no
user-role junction table and none is needed.

### Backend

* `Vitality/Filters/RequiresPermissionAttribute.cs` — permission check
* `Vitality/Filters/AuthorizeRolesAttribute.cs` — role check
* `Vitality.Models/Repos/Services/Security/PermissionResolver.cs` — resolves the
  caller's grants **from the database, not from the JWT claim**, caches per role
  for 10 minutes, and invalidates on role save. Super Admin (RoleId 1) holds
  every permission implicitly and is deliberately not seeded.
* `Vitality.Models/Security/Permissions.cs` — the 71 codes as constants
* `Vitality/Controllers/RolesController.cs` + `RolesRepo.cs` — role admin API

### Frontend

`TeleHealthFE/src/app/shared/permission/` — `PermissionsService`,
`permission.guard.ts`, `has-permission.directive.ts`.

`PermissionsService` is already server-authoritative: it calls
`GET api/Roles/myPermissions` and those codes replace the hardcoded
`rolePermissions` map as soon as they arrive. The hardcoded map survives **only**
as a first-paint fallback, and the file says so explicitly, including that none
of it is a security boundary.

**No frontend work is required by this ticket.** This was the part most likely to
be re-estimated as new work; it is done.

---

## 3. Coverage measurement

Parsed from `Vitality/Controllers/*.cs`.

| Category | Count | Share |
|---|---|---|
| Total HTTP endpoints | **339** | 100% |
| `[RequiresPermission]` | 118 | 35% |
| `[AuthorizeRoles]` only | 35 | 10% |
| `[AllowAnonymous]` (by design) | 54 | 16% |
| **Authenticated, no role or permission check** | **139** | **41%** |

### What the 139 actually means

It does **not** mean unprotected. `Program.cs` adds a global `AuthorizeFilter`
to MVC options:

```csharp
builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(policy));
})
```

Every controller inherits it, including those with no `[Authorize]` of their own.
Verified: `GET /api/Chats/getChatChannels` returns **401** without a token.

Note this is an `AuthorizeFilter` on MVC options, **not** an
`AuthorizationOptions.FallbackPolicy`. `AddAuthorization` at `Program.cs:279`
sets `DefaultPolicy`, which would not have covered attribute-less controllers.
The global filter is what does. Anyone refactoring that registration must keep it.

So the real exposure is **horizontal privilege**: any authenticated user, of any
role, can call all 139. A Patient's token satisfies `[Authorize]` exactly as well
as a Global Admin's.

### Where the 139 sit

| Controller | Endpoints | Notes |
|---|---|---|
| `DropDowns` | 29 | Reference data; also `saveRoleTitle` / `deleteRoleTitle`, which are writes |
| `Invoice` | 22 | Includes `RefundPayment`, `PayInvoice`, `CancelInvoice`, `GetAdminPaymentDashboard` |
| `Dashboards` | 18 | Cross-tenant revenue, earnings, patient counts |
| `Coupons` | 8 | create / update / delete / activate / deactivate |
| `Payment` | 7 | `CreatePayment`, `SaveCardPayment`, `DeleteCard`, `SetDefaultCard` |
| `Products` | 7 | `saveCatalog`, `editClinicBundlePrice`, `saveCustomDrug` |
| `Chats` | 6 | Message history and channel reads |
| `Users` | 6 | `UpdateUserPassword`, `UpdateUserProfile` |
| `Facilities` | 4 | `saveFacility`, `assign`, `unassign` |
| `ProductConditions` | 4 | |
| `ProviderSchedules` | 4 | |
| `NotificationTest` | 3 | Class-level `[AuthorizeRoles(GlobalAdmin, SuperAdmin)]` — covered |
| `Commons`, `Notifications`, `PatientAppointments`, `Questionnaires`, `ReminderEmails`, `Subscriptions` | 2 each | |
| `Brands`, `EmpowerPharmacy`, `Fullscript`, `PatientPayments`, `ProductCategories` | 1 each | |

Full per-endpoint listing is reproducible from the parser described in §7.

---

## 4. Critical finding — fixed in this change

Three controllers carried a **class-level `[AllowAnonymous]`**, which overrides
the global filter. They were reachable by anyone on the internet who knew the
route, with no token.

| Endpoint | Method | What it does |
|---|---|---|
| `/api/TestRecurring/trigger-recurring-payments` | POST | **Charges real saved cards** via Stripe and Square for every treatment whose next payment date has arrived |
| `/api/TestMonthlyInvoice/trigger-monthly-invoice` | POST | Generates billable invoices for every active facility |
| `/api/IntakeReminderDiagnostic/GetAllAppointments` | GET | Returns appointment rows — patient ids, treatment ids, times |
| `/api/IntakeReminderDiagnostic/CheckAppointment/{id}` | GET | Returns patient name and email for an appointment |

`GetAllAppointments` was confirmed by request against the running API with no
`Authorization` header: **HTTP 200, 50 appointment records**. That is an
unauthenticated PHI disclosure.

The two POST endpoints were **not** exercised — triggering them would have
charged live patient cards. Their exposure is established from the same
class-level attribute and confirmed present in the published Swagger document.

### Fix applied

`[AllowAnonymous]` replaced with `[AuthorizeRoles(UserRole.SuperAdmin)]` on all
three, matching how `NotificationTestController` already treats the equivalent
manual-trigger surface.

Verified after the change, with no `Authorization` header:

| Endpoint | Before | After |
|---|---|---|
| `GET /api/IntakeReminderDiagnostic/GetAllAppointments` | 200 | **401** |
| `POST /api/TestRecurring/trigger-recurring-payments` | reachable | **401** |
| `POST /api/TestMonthlyInvoice/trigger-monthly-invoice` | reachable | **401** |
| `POST /api/Accounts/login` (regression control) | 200 | 200 |

`dotnet build -c Debug`: 0 errors. `dotnet test`: 31 passed, 0 failed.

### Follow-up worth raising separately

These are debug scaffolding shipped to production. Restricting them is the
minimum fix; deleting them, or excluding them outside Development, is the better
one. Out of scope here — this ticket is RBAC, not controller lifecycle.

---

## 5. The blocker for the remaining 139

Annotating the rest is **not** mechanical, and this is the single most important
finding for estimation.

The live grant matrix does not line up with the obvious permission choices. The
clearest example:

```
payment_view    granted to roles 3, 4, 6   (Clinic Admin, Provider, Patient)
payment_update  granted to role  3          (Clinic Admin)
```

**Global Admin (role 2) holds neither.** Yet `InvoiceController` contains
`GetFacilityInvoicesForGlobalAdmin` and `GetAdminPaymentDashboard` — endpoints
that exist specifically for Global Admin.

Adding `[RequiresPermission(Permissions.Payment.View)]` to those would return 403
to the only role that is supposed to use them. The same trap exists across the
billing surface.

So each of the 139 needs three decisions, not one:

1. Which permission code applies — and whether a new code is needed (a new code
   means a `SYS_Permission` row, grants, and a seed-script change).
2. Which roles should hold it — reconciled against the 148 existing grants.
3. Whether granting it to a role that lacks it today changes that role's
   effective access anywhere else, since permissions are shared across endpoints.

None of these can be answered from the code alone. They are product decisions
about who may do what, and getting one wrong locks a real role out of a real
screen in production.

**This is why the ticket is labelled `gap-analysis` and why CLAUDE.md puts that
label on the scheduled agent's skip list.**

---

## 6. Recommended plan and revised estimate

Sequenced so that the risky work is decided before it is written.

| # | Work | Size | Blocked on |
|---|---|---|---|
| 1 | Lock down the three anonymous controllers | **Done** (this change) | — |
| 2 | Agree the permission-per-endpoint matrix for the 139, in a review with the platform owner | 1–2 days | Product decision |
| 3 | Reconcile grants: decide whether Global Admin gains `payment_view` / `payment_update`, and update `Create_RBAC_Tables_And_Seed.sql` | 0.5 day | Output of 2 |
| 4 | Apply `[RequiresPermission]` across the 139, in per-controller tranches | 2–3 days | Output of 2 and 3 |
| 5 | Verify each role against each tranche — no automated coverage exists for authorization | 2–3 days | Output of 4 |
| 6 | Permissions for modules this backlog adds (ICD/CPT admin, AI notes, transcripts) | Size with those tickets | Those modules |

**Revised estimate: 6–9 days**, excluding item 6, and gated on a decision session
for item 2.

The figure this replaces assumed building RBAC. Roughly 80% of that scope already
exists and is deployed. What remains is applying it, and the cost is dominated by
verification, not by writing attributes.

### Risks

* **Lockout.** A wrong permission on a live endpoint returns 403 to a real user.
  Deploy in tranches, per controller, not as one change.
* **No test coverage.** The suite is 31 tests over three pure functions. Nothing
  covers controllers, repositories or authorization. Every tranche in item 4
  needs manual verification, or characterisation tests written first.
* **Misspelled codes are load-bearing.** `avilability`, `questionnaier` — the
  Angular app compares these as literal strings. Do not correct them on one side
  only. New codes should not copy the pattern.

---

## 7. Reproducing the measurement

Endpoint counts come from parsing attribute blocks above each action in
`Vitality/Controllers/*.cs`, classifying each by whether its block or its
class carries `[RequiresPermission]`, `[AuthorizeRoles]` or `[AllowAnonymous]`.

Grant matrix:

```sql
SELECT p.PermissionCode,
       STUFF((SELECT ',' + CAST(rp.RoleId AS varchar(3))
              FROM dbo.SYS_RolePermission rp
              WHERE rp.PermissionId = p.PermissionId
              ORDER BY rp.RoleId FOR XML PATH('')), 1, 1, '') AS roles
FROM dbo.SYS_Permission p
ORDER BY p.PermissionCode;
```

Live verification of an endpoint's protection:

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5199/<route>
```

---

## 8. Acceptance criteria

| Criterion | State |
|---|---|
| Written gap analysis, screen by screen and module by module | **Met** — this document |
| Only the identified gap implemented; filters, guards and tables extended not replaced | **Met** — no existing filter, guard or table was changed |
| New modules (ICD/CPT admin, AI notes, transcripts) covered by permissions | **Not applicable** — those modules do not exist yet; carried as item 6 |
| Permission checks enforced server-side, not only hidden in the UI | **Partially met** — three anonymous controllers closed; the 139 remain, gated on §5 |
| Revised estimate recorded, replacing any earlier figure | **Met** — §6, 6–9 days |
