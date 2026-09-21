# TEL-10 — Database provisioning readiness

Status: **blocked, awaiting a human decision.** No code change is proposed here.

This file records what the repository already supports for TEL-10 and what cannot be
done without access this run does not have.

## Why the ticket cannot be completed unattended

Three of the acceptance criteria need something that exists outside this repository:

| Acceptance criterion | Blocker |
|---|---|
| Restore a `.bak`, or obtain the shared development connection string | No backup file is in the repository, and the connection string is a credential. An unattended run must not read back or commit credentials. |
| Run `SELECT name, compatibility_level FROM sys.databases WHERE name = '<db>';` and record the result | Requires a reachable SQL Server instance holding the restored database. |
| `GET /api/Health` responds and a login succeeds against the restored database | Same — requires the provisioned instance. |
| Record a decision on how the schema is version-controlled going forward | A human architectural decision, not an implementation. |

The ticket is database-first by design: there are no EF Core migrations under
`Vitality.Models/`, nothing calls `EnsureCreated()`, and the only SQL checked in is two
hand-written scripts (`Vitality.Models/Sql/Create_PT_PatientTreatmentSoapNote.sql`,
`Vitality.Models/Sql/Create_RBAC_Tables_And_Seed.sql`). The 89-table schema genuinely
cannot be generated from source, which is what makes the restore a hard prerequisite
rather than a convenience.

## What the code already handles — no further change needed

The two code-side concerns named in the acceptance criteria are already implemented,
so once the database exists the remaining work is configuration only.

### Compatibility level escape hatch — present

`MainContext.ReadSqlServerCompatibilityLevel` (`Vitality.Models/EntityClasses/MainContext.cs`)
reads the optional `Database:SqlServerCompatibilityLevel` key and returns 0 when it is
unset or unparseable. Both places that build the provider options honour it:

- `MainContext.OnConfiguring` — the self-configuration path, via `sql.UseCompatibilityLevel(level)`
- `Vitality/Program.cs` (around line 324) — the DI registration path

So if the restored database reports a level below 130, setting
`Database__SqlServerCompatibilityLevel=120` restores the EF Core 6 `IN (...)` form across
all ~108 affected queries with no code edit, exactly as the ticket anticipates. A modern
server needs nothing: 0 leaves the EF Core 8 defaults alone.

### `Encrypt` in the connection string — already validated

`Vitality/Configuration/SecretsValidation.cs` (around line 198) logs a warning when the
connection string does not mention `Encrypt`, explaining that
`Microsoft.Data.SqlClient` 5.x under EF Core 8 defaults it to `True` where EF Core 6
defaulted to `False`. `Vitality/appsettings.SECURE.json` and
`appsettings.Development.SECURE.json` both carry the guidance inline
(`Encrypt=True;TrustServerCertificate=True` hosted, `Encrypt=False` for a local instance).

The criterion is therefore about the operator's value, not about this codebase.

## Open question — schema version control

The last acceptance criterion asks for a decision, and every new table in this backlog
currently ships as hand-written SQL. The realistic options:

1. **Keep database-first; version the schema as ordered, idempotent SQL scripts.**
   Add a `Vitality.Models/Sql/` migration runner and a schema-version table, folding in the
   two existing scripts. Smallest change, matches how the team already works, and keeps the
   restored `.bak` as the source of truth. Ordering and idempotency are enforced by review
   rather than by a tool.

2. **Scaffold EF Core migrations from the restored database and go code-first.**
   `dotnet ef dbcontext scaffold` against the restored database, then an initial baseline
   migration. Gives real up/down tooling and drift detection, but the baseline has to be
   reconciled against 89 hand-maintained tables, and `MainContext.OnModelCreating` is
   already hand-written — so this is its own sizeable piece of work, not a side effect of
   provisioning.

3. **Adopt a dedicated migration tool (DbUp, Flyway, Grate).**
   Between the two: keeps the hand-written SQL the team writes anyway, adds ordering,
   hashing and a journal table without adopting the EF Core model-diff workflow.

Option 1 or 3 fits the codebase as it stands; option 2 should be its own ticket if wanted.

## To unblock

1. Provide the `.bak`, or the shared development connection string through the
   environment (`ConnectionStrings__dbConnection`) or user-secrets — never in
   `appsettings.json`, which no longer holds it.
2. Run the `sys.databases` query and set `Database__SqlServerCompatibilityLevel=120`
   if the result is below 130.
3. Choose a schema version-control option above so a follow-up ticket can implement it.
