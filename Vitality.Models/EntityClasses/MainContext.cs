using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Vitality.Models.Configuration;

namespace Vitality.Models.EntityClasses
{
    public partial class MainContext : DbContext
    {
        public MainContext()
        {
        }

        public MainContext(DbContextOptions<MainContext> options)
            : base(options)
        {
        }

        public virtual DbSet<BD_ChartColor> BD_ChartColors { get; set; } = null!;
        public virtual DbSet<BD_PrimaryColorVarient> BD_PrimaryColorVarients { get; set; } = null!;
        public virtual DbSet<DG_DrugIngredient> DG_DrugIngredients { get; set; } = null!;
        public virtual DbSet<FC_UsersInFacility> FC_UsersInFacilities { get; set; } = null!;
        public virtual DbSet<LK_Role> LK_Roles { get; set; } = null!;
        public virtual DbSet<LK_RoleTitle> LK_RoleTitles { get; set; } = null!;
        public virtual DbSet<PC_CLINICTOPATIENT> PC_CLINICTOPATIENTs { get; set; } = null!;
        public virtual DbSet<PC_CATALOGFACILITYASSIGNMENT> PC_CATALOGFACILITYASSIGNMENTs { get; set; } = null!;
        public virtual DbSet<PC_GATOCLINIC> PC_GATOCLINICs { get; set; } = null!;
        public virtual DbSet<PC_PHARMTOGLOBAL> PC_PHARMTOGLOBALs { get; set; } = null!;
        public virtual DbSet<PC_DRUGFACILITYEXCLUSION> PC_DRUGFACILITYEXCLUSIONS { get; set; } = null!;
        public virtual DbSet<PD_Bundle> PD_Bundles { get; set; } = null!;
        public virtual DbSet<PD_Catalog> PD_Catalogs { get; set; } = null!;
        public virtual DbSet<PD_Category> PD_Categories { get; set; } = null!;
        public virtual DbSet<PD_Condition> PD_Conditions { get; set; } = null!;
        public virtual DbSet<PD_CouponCodeBundle> PD_CouponCodeBundles { get; set; } = null!;
        public virtual DbSet<PD_DigitalProduct> PD_DigitalProducts { get; set; } = null!;
        public virtual DbSet<PD_Drug> PD_Drugs { get; set; } = null!;
        public virtual DbSet<PD_DrugVarientsInBundle> PD_DrugVarientsInBundles { get; set; } = null!;
        public virtual DbSet<PD_FacilityBundlePrice> PD_FacilityBundlePrices { get; set; } = null!;
        public virtual DbSet<PD_FacilityCategory> PD_FacilityCategories { get; set; } = null!;
        public virtual DbSet<PD_LabTest> PD_LabTests { get; set; } = null!;
        public virtual DbSet<PH_PharmaciesStorageType> PH_PharmaciesStorageTypes { get; set; } = null!;
        public virtual DbSet<PH_PharmacyNote> PH_PharmacyNotes { get; set; } = null!;
        public virtual DbSet<PT_CouponUsage> PT_CouponUsages { get; set; } = null!;
        public virtual DbSet<PT_MedicineSupply> PT_MedicineSupplies { get; set; } = null!;
        public virtual DbSet<PT_Patient> PT_Patients { get; set; } = null!;
        public virtual DbSet<PT_PatientAppointmentSlot> PT_PatientAppointmentSlots { get; set; } = null!;
        public virtual DbSet<PT_PatientCardDetail> PT_PatientCardDetails { get; set; } = null!;
        public virtual DbSet<PT_PatientOrder> PT_PatientOrders { get; set; } = null!;
        public virtual DbSet<PT_PatientOrderManualFulfillment> PT_PatientOrderManualFulfillments { get; set; } = null!;
        public virtual DbSet<PT_PatientPaymentDetail> PT_PatientPaymentDetails { get; set; } = null!;
        public virtual DbSet<PT_PatientPrescription> PT_PatientPrescriptions { get; set; } = null!;
        public virtual DbSet<PT_PatientPrescriptionSoapNote> PT_PatientPrescriptionSoapNotes { get; set; } = null!;
        public virtual DbSet<PT_PatientTreatment> PT_PatientTreatments { get; set; } = null!;
        public virtual DbSet<PT_PatientTreatmentDocument> PT_PatientTreatmentDocuments { get; set; } = null!;
        public virtual DbSet<PT_PatientDocument> PT_PatientDocuments { get; set; } = null!;
        public virtual DbSet<PT_PatientTreatmentSoapNote> PT_PatientTreatmentSoapNotes { get; set; } = null!;
        public virtual DbSet<PT_PatientTreatmentInTakeForm> PT_PatientTreatmentInTakeForms { get; set; } = null!;
        public virtual DbSet<PT_PatientQuestionnaire> PT_PatientQuestionnaires { get; set; } = null!;
        public virtual DbSet<PT_PatientQuestionnaireAnswer> PT_PatientQuestionnaireAnswers { get; set; } = null!;
        public virtual DbSet<PT_PrescriptionMedicine> PT_PrescriptionMedicines { get; set; } = null!;
        public virtual DbSet<Pt_PatientProduct> Pt_PatientProducts { get; set; } = null!;
        public virtual DbSet<SYS_AuditLog> SYS_AuditLogs { get; set; } = null!;
        public virtual DbSet<SYS_FailedEmailLog> SYS_FailedEmailLogs { get; set; } = null!;
        public virtual DbSet<PT_PatientProfileNote> PT_PatientProfileNotes { get; set; } = null!;
        public virtual DbSet<SYS_Brand> SYS_Brands { get; set; } = null!;
        public virtual DbSet<SYS_BroadcastMessage> SYS_BroadcastMessages { get; set; } = null!;
        public virtual DbSet<SYS_Chat> SYS_Chats { get; set; } = null!;
        public virtual DbSet<SYS_ChatChannel> SYS_ChatChannels { get; set; } = null!;
        public virtual DbSet<SYS_ChatChannelParticipant> SYS_ChatChannelParticipants { get; set; } = null!;
        public virtual DbSet<SYS_ChatReadReceipt> SYS_ChatReadReceipts { get; set; } = null!;
        public virtual DbSet<SYS_City> SYS_Cities { get; set; } = null!;
        public virtual DbSet<SYS_Country> SYS_Countries { get; set; } = null!;
        public virtual DbSet<SYS_CouponCode> SYS_CouponCodes { get; set; } = null!;
        public virtual DbSet<SYS_CouponCode1> SYS_CouponCodes1 { get; set; } = null!;
        public virtual DbSet<SYS_Facility> SYS_Facilities { get; set; } = null!;
        public virtual DbSet<SYS_ForgetPassword> SYS_ForgetPasswords { get; set; } = null!;
        public virtual DbSet<SYS_Login> SYS_Logins { get; set; } = null!;
        public virtual DbSet<SYS_Notification> SYS_Notifications { get; set; } = null!;
        public virtual DbSet<SYS_Organization> SYS_Organizations { get; set; } = null!;
        public virtual DbSet<SYS_Pharmacy> SYS_Pharmacies { get; set; } = null!;
        public virtual DbSet<SYS_Product> SYS_Products { get; set; } = null!;
        public virtual DbSet<SYS_ProviderGroup> SYS_ProviderGroups { get; set; } = null!;
        public virtual DbSet<SYS_Questionnaire> SYS_Questionnaires { get; set; } = null!;
        public virtual DbSet<SYS_QuestionnaireFacilityJson> SYS_QuestionnaireFacilityJsons { get; set; } = null!;
        public virtual DbSet<SYS_CodeSetVersion> SYS_CodeSetVersions { get; set; } = null!;
        public virtual DbSet<SYS_Icd10Code> SYS_Icd10Codes { get; set; } = null!;
        public virtual DbSet<SYS_CptCode> SYS_CptCodes { get; set; } = null!;
        public virtual DbSet<SYS_QuestionnairesInProduct> SYS_QuestionnairesInProducts { get; set; } = null!;
        public virtual DbSet<SYS_State> SYS_States { get; set; } = null!;
        public virtual DbSet<SYS_Subscription> SYS_Subscriptions { get; set; } = null!;
        public virtual DbSet<SYS_SupervisorProvider> SYS_SupervisorProviders { get; set; } = null!;
        public virtual DbSet<SYS_Ticket> SYS_Tickets { get; set; } = null!;
        public virtual DbSet<SYS_UserCard> SYS_UserCards { get; set; } = null!;
        public virtual DbSet<SYS_UserDetail> SYS_UserDetails { get; set; } = null!;
        public virtual DbSet<StgFacility> StgFacilities { get; set; } = null!;
        public virtual DbSet<Sys_EmpowerOrder> Sys_EmpowerOrders { get; set; } = null!;
        public virtual DbSet<Sys_FacilitySquareCred> Sys_FacilitySquareCreds { get; set; } = null!;
        public virtual DbSet<Sys_FacilityStripeConnect> Sys_FacilityStripeConnects { get; set; } = null!;
        public virtual DbSet<Sys_FullscriptOAuth> Sys_FullscriptOAuths { get; set; } = null!;
        public virtual DbSet<Sys_Invoice> Sys_Invoices { get; set; } = null!;
        public virtual DbSet<Sys_InvoiceLineItem> Sys_InvoiceLineItems { get; set; } = null!;
        public virtual DbSet<Sys_InvoicePayment> Sys_InvoicePayments { get; set; } = null!;
        public virtual DbSet<TK_TicketComment> TK_TicketComments { get; set; } = null!;
        public virtual DbSet<TK_TicketFile> TK_TicketFiles { get; set; } = null!;
        public virtual DbSet<UR_ProviderCategory> UR_ProviderCategories { get; set; } = null!;
        public virtual DbSet<UR_ProviderScheduledSlot> UR_ProviderScheduledSlots { get; set; } = null!;
        public virtual DbSet<UR_ProviderStateLicense> UR_ProviderStateLicenses { get; set; } = null!;
        public virtual DbSet<UR_ProviderWeeklyTemplate> UR_ProviderWeeklyTemplates { get; set; } = null!;
        public virtual DbSet<UR_ProviderDayHours> UR_ProviderDayHours { get; set; } = null!;
        public virtual DbSet<UR_ProviderTimeRange> UR_ProviderTimeRanges { get; set; } = null!;
        public virtual DbSet<UR_ProviderDateOverride> UR_ProviderDateOverrides { get; set; } = null!;
        public virtual DbSet<UR_ProviderDateOverrideTimeRange> UR_ProviderDateOverrideTimeRanges { get; set; } = null!;

        /// <summary>
        /// Self-configuration fallback for the many repositories that construct
        /// <c>new MainContext()</c> directly (see <c>BaseRepo</c>) instead of taking
        /// the DI-registered context. When the context is created through DI,
        /// <c>optionsBuilder.IsConfigured</c> is already true and none of this runs.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Environment variable wins, so a server never depends on a file.
                var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__dbConnection");
                var compatibilityLevel = 0;

                try
                {
                    var configuration = BuildFallbackConfiguration();

                    if (string.IsNullOrEmpty(connectionString))
                    {
                        connectionString = configuration.GetConnectionString("dbConnection");
                    }

                    compatibilityLevel = ReadSqlServerCompatibilityLevel(configuration);
                }
                catch (Exception) when (!string.IsNullOrEmpty(connectionString))
                {
                    // The environment variable already gave us what we need, so a
                    // missing or malformed appsettings.json is not fatal here.
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Failed to load the database connection string. Set the 'ConnectionStrings__dbConnection' " +
                        "environment variable, or (in Development) run: " +
                        "dotnet user-secrets set \"ConnectionStrings:dbConnection\" \"<value>\" " +
                        "--project .\\Vitality\\Vitality.csproj. See SECRETS.md.", ex);
                }

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        "Database connection string not found. It is intentionally no longer stored in appsettings.json. " +
                        "Set the 'ConnectionStrings__dbConnection' environment variable (staging/production), " +
                        "or run: dotnet user-secrets set \"ConnectionStrings:dbConnection\" \"<value>\" " +
                        "--project .\\Vitality\\Vitality.csproj (development). See SECRETS.md.");
                }

                optionsBuilder.UseSqlServer(connectionString, sql =>
                {
                    if (compatibilityLevel > 0)
                    {
                        sql.UseCompatibilityLevel(compatibilityLevel);
                    }
                });
            }
        }

        /// <summary>
        /// Builds the configuration used by the self-configuration path above.
        /// Provider order matters: later providers override earlier ones, so
        /// environment variables always win.
        /// </summary>
        internal static IConfigurationRoot BuildFallbackConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                // Development-only. Secrets are no longer in appsettings.json, so this
                // path has to read the local user-secrets store too. No-op on a server.
                .AddDevelopmentUserSecrets()
                .AddEnvironmentVariables()
                .Build();
        }

        /// <summary>
        /// Reads the optional <c>Database:SqlServerCompatibilityLevel</c> escape hatch.
        /// <para>
        /// EF Core 8 changed how <c>collection.Contains(entity.Column)</c> is
        /// translated: it now emits <c>OPENJSON(@p)</c> instead of <c>IN (@p0, @p1, ...)</c>.
        /// There are around 108 such queries in this codebase, and OPENJSON requires
        /// the target DATABASE to be at compatibility level 130 or higher
        /// (SQL Server 2016+). Check with:
        /// </para>
        /// <code>SELECT name, compatibility_level FROM sys.databases WHERE name = '&lt;db&gt;';</code>
        /// <para>
        /// If the level is below 130, set this to 120 to restore the EF Core 6
        /// behaviour across every query without editing any of them. 0 means
        /// "leave EF Core 8 defaults alone" and is the right value on a modern server.
        /// </para>
        /// </summary>
        /// <returns>The configured level, or 0 when unset or invalid.</returns>
        public static int ReadSqlServerCompatibilityLevel(IConfiguration configuration)
        {
            var raw = configuration["Database:SqlServerCompatibilityLevel"];

            return int.TryParse(raw, out var level) && level > 0 ? level : 0;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BD_ChartColor>(entity =>
            {
                entity.HasKey(e => e.ChartColorId);

                entity.Property(e => e.ChartColor).HasMaxLength(500);
            });

            modelBuilder.Entity<BD_PrimaryColorVarient>(entity =>
            {
                entity.HasKey(e => e.PrimaryColorVarientId);

                entity.Property(e => e.PrimaryColorVarient).HasMaxLength(50);
            });

            modelBuilder.Entity<DG_DrugIngredient>(entity =>
            {
                entity.HasKey(e => e.DrugIngredientId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.IngredientName).HasMaxLength(1000);

                entity.Property(e => e.IngredientStrength).HasMaxLength(1000);

                entity.Property(e => e.Status).HasMaxLength(50);
            });

            modelBuilder.Entity<FC_UsersInFacility>(entity =>
            {
                entity.HasKey(e => e.FacilityUserId)
                    .HasName("PK_FC_UserInFacilities");

                entity.HasIndex(e => new { e.UserId, e.FacilityId }, "IX_FC_UsersInFacilities_UserFacility");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<LK_Role>(entity =>
            {
                entity.HasKey(e => e.RoleId);

                entity.Property(e => e.RoleName).HasMaxLength(250);
            });

            modelBuilder.Entity<LK_RoleTitle>(entity =>
            {
                entity.HasKey(e => e.RoleTitleId);
                entity.Property(e => e.RoleTitleName).HasMaxLength(150);
                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");
            });

            modelBuilder.Entity<PC_CLINICTOPATIENT>(entity =>
            {
                entity.HasKey(e => e.ClinicToPatientId);

                entity.ToTable("PC_CLINICTOPATIENT");

                entity.HasIndex(e => e.DrugId, "IX_PC_CTP_Drug");

                entity.HasIndex(e => e.FacilityId, "IX_PC_CTP_Facility");

                entity.Property(e => e.ClinicSuggestedRetailPrice).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.HasOne(d => d.Drug)
                    .WithMany(p => p.PC_CLINICTOPATIENTs)
                    .HasForeignKey(d => d.DrugId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PC_CTP_Drug");

                entity.HasOne(d => d.GAtoClinic)
                    .WithMany(p => p.PC_CLINICTOPATIENTs)
                    .HasForeignKey(d => d.GAtoClinicId)
                    .HasConstraintName("FK_PC_CTP_GAtoClinic");
            });

            modelBuilder.Entity<PC_CATALOGFACILITYASSIGNMENT>(entity =>
            {
                entity.HasKey(e => e.CatalogFacilityAssignmentId);

                entity.ToTable("PC_CATALOGFACILITYASSIGNMENT");

                entity.HasIndex(e => e.CatalogId, "IX_PC_CFA_CatalogId");
                entity.HasIndex(e => e.FacilityId, "IX_PC_CFA_FacilityId");
                entity.HasIndex(e => new { e.CatalogId, e.FacilityId }, "UX_PC_CFA_CatalogFacility")
                    .IsUnique()
                    .HasFilter("([IsActive]=(1))");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.CreatedDate)
                    .HasDefaultValueSql("(sysutcdatetime())");

                entity.HasOne(d => d.Catalog)
                    .WithMany(p => p.PC_CATALOGFACILITYASSIGNMENTs)
                    .HasForeignKey(d => d.CatalogId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PC_CFA_PD_Catalog");
            });

            modelBuilder.Entity<PC_GATOCLINIC>(entity =>
            {
                entity.HasKey(e => e.GAtoClinicId);

                entity.ToTable("PC_GATOCLINIC");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.SuggestedRetailPrice).HasColumnType("decimal(18, 4)");

                entity.HasOne(d => d.PharmToGlobal)
                    .WithMany(p => p.PC_GATOCLINICs)
                    .HasForeignKey(d => d.PharmToGlobalId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PC_GAC_PharmToGlobal");
            });

            modelBuilder.Entity<PC_PHARMTOGLOBAL>(entity =>
            {
                entity.HasKey(e => e.PharmToGlobalId);

                entity.ToTable("PC_PHARMTOGLOBAL");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.Property(e => e.MarkupPercent)
                    .HasColumnType("decimal(9, 4)")
                    .HasDefaultValueSql("((0))");

                entity.Property(e => e.PharmacyPrice).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.WholesalePrice)
                    .HasColumnType("decimal(18, 4)")
                    .HasComputedColumnSql("(case when [PharmacyPrice] IS NULL OR [MarkupPercent] IS NULL then NULL else CONVERT([decimal](18,4),[PharmacyPrice]*((1)+[MarkupPercent]/(100.0))) end)", true);

                entity.HasOne(d => d.Drug)
                    .WithMany(p => p.PC_PHARMTOGLOBALs)
                    .HasForeignKey(d => d.DrugId)
                    .HasConstraintName("FK_PC_PTG_Drug");
            });

            modelBuilder.Entity<PC_DRUGFACILITYEXCLUSION>(entity =>
            {
                entity.HasKey(e => e.DrugFacilityExclusionId);

                entity.ToTable("PC_DRUGFACILITYEXCLUSION");

                entity.HasIndex(e => e.DrugId, "IX_PC_DFE_Drug");
                entity.HasIndex(e => e.FacilityId, "IX_PC_DFE_Facility");
                entity.HasIndex(e => new { e.DrugId, e.FacilityId }, "IX_PC_DFE_DrugFacility");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("(sysutcdatetime())");
            });

            modelBuilder.Entity<PD_Bundle>(entity =>
            {
                entity.HasKey(e => e.BundleId);

                entity.Property(e => e.ComparePrice).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Name).HasMaxLength(250);

                entity.Property(e => e.Price).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.Status).HasMaxLength(50);
            });

            modelBuilder.Entity<PD_Catalog>(entity =>
            {
                entity.HasKey(e => e.CatalogId);

                entity.ToTable("PD_Catalog");

                entity.HasIndex(e => e.CatalogName, "UX_PD_Catalog_CatalogName")
                    .IsUnique()
                    .HasFilter("([IsActive]=(1))");

                entity.Property(e => e.CatalogName).HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");
                entity.Property(e => e.IsSystemDefined).HasDefaultValueSql("((0))");
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");
            });

            modelBuilder.Entity<PD_Category>(entity =>
            {
                entity.HasKey(e => e.CategoryId);

                entity.Property(e => e.CategoryDescription).HasMaxLength(1000);

                entity.Property(e => e.CategoryName).HasMaxLength(1000);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<PD_Condition>(entity =>
            {
                entity.HasKey(e => e.ConditionId);

                entity.Property(e => e.ConditionName).HasMaxLength(1000);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<PD_CouponCodeBundle>(entity =>
            {
                entity.HasKey(e => e.CouponCodeBundleId)
                    .HasName("PK__PD_Coupo__3C785D70FE6DBCF4");

                entity.ToTable("PD_CouponCodeBundle");

                entity.HasIndex(e => e.BundleId, "IX_PD_CouponCodeBundle_BundleId");

                entity.HasIndex(e => e.CoupanCodeId, "IX_PD_CouponCodeBundle_CoupanCodeId");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.HasOne(d => d.Bundle)
                    .WithMany(p => p.PD_CouponCodeBundles)
                    .HasForeignKey(d => d.BundleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PD_CouponCodeBundle_Bundle");

                entity.HasOne(d => d.CoupanCode)
                    .WithMany(p => p.PD_CouponCodeBundles)
                    .HasForeignKey(d => d.CoupanCodeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PD_CouponCodeBundle_CoupanCode");
            });

            modelBuilder.Entity<PD_DigitalProduct>(entity =>
            {
                entity.HasKey(e => e.DigitalProductId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Name).HasMaxLength(250);

                entity.Property(e => e.ProductType).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);
            });

            modelBuilder.Entity<PD_Drug>(entity =>
            {
                entity.HasKey(e => e.DrugId)
                    .HasName("PK_DG_DurgVarients");

                entity.HasIndex(e => e.CatalogId, "IX_PD_Drugs_CatalogId");

                entity.Property(e => e.BillingFrequency).HasMaxLength(50);

                entity.Property(e => e.BrandName).HasMaxLength(250);

                entity.Property(e => e.ComparePrice).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.Control_Substance).HasColumnName("Control Substance");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Dosage).HasMaxLength(50);

                entity.Property(e => e.DosageForm).HasMaxLength(50);

                entity.Property(e => e.Dose).HasMaxLength(50);

                entity.Property(e => e.Form).HasMaxLength(500);

                entity.Property(e => e.GenericName).HasMaxLength(250);

                entity.Property(e => e.Instruction).HasMaxLength(500);

                entity.Property(e => e.LablerName).HasMaxLength(250);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Name).HasMaxLength(1000);

                entity.Property(e => e.PackageNDC).HasMaxLength(500);

                entity.Property(e => e.PackageSize).HasMaxLength(100);

                entity.Property(e => e.Packing).HasMaxLength(500);

                entity.Property(e => e.Price).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.Markup)
                    .HasColumnType("decimal(18, 4)");

                entity.Property(e => e.QuantityUnit).HasMaxLength(50);

                entity.Property(e => e.ShippingFrequency).HasMaxLength(50);

                entity.Property(e => e.ShortName).HasMaxLength(250);

                entity.Property(e => e.Sig).HasMaxLength(500);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.Strenght).HasMaxLength(500);

                entity.Property(e => e.SuggestedRetail).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.Type).HasMaxLength(50);

                entity.Property(e => e.UPC).HasMaxLength(500);

                entity.HasOne(d => d.Catalog)
                    .WithMany(p => p.PD_Drugs)
                    .HasForeignKey(d => d.CatalogId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PD_Drugs_PD_Catalog");
            });

            modelBuilder.Entity<PD_DrugVarientsInBundle>(entity =>
            {
                entity.HasKey(e => e.DrugVarientBundleId)
                    .HasName("PK_PD_DrugsInBundles");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Name).HasMaxLength(250);

                entity.Property(e => e.Price).HasColumnType("decimal(18, 0)");
            });

            modelBuilder.Entity<PD_FacilityBundlePrice>(entity =>
            {
                entity.HasKey(e => e.FacilityBundlePriceId)
                    .HasName("PK__PD_Facil__834CD8B6C2543961");

                entity.ToTable("PD_FacilityBundlePrice");

                entity.HasIndex(e => new { e.FacilityId, e.BundleId }, "UQ_PFBP")
                    .IsUnique();

                entity.Property(e => e.ClinicPrice).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.CreatedDateUtc)
                    .HasPrecision(0)
                    .HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.ModifiedDateUtc).HasPrecision(0);

                entity.HasOne(d => d.Bundle)
                    .WithMany(p => p.PD_FacilityBundlePrices)
                    .HasForeignKey(d => d.BundleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__PD_Facili__Bundl__3791033A");

                entity.HasOne(d => d.Facility)
                    .WithMany(p => p.PD_FacilityBundlePrices)
                    .HasForeignKey(d => d.FacilityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__PD_Facili__Facil__369CDF01");
            });

            modelBuilder.Entity<PD_FacilityCategory>(entity =>
            {
                entity.HasKey(e => new { e.FacilityId, e.CategoryId });

                entity.HasIndex(e => new { e.CategoryId, e.IsActive }, "IX_PD_FacilityCategories_CategoryId_IsActive");

                entity.HasIndex(e => new { e.FacilityId, e.IsActive }, "IX_PD_FacilityCategories_FacilityId_IsActive");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.PD_FacilityCategories)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PD_FacilityCategories_PD_Categories");

                entity.HasOne(d => d.Facility)
                    .WithMany(p => p.PD_FacilityCategories)
                    .HasForeignKey(d => d.FacilityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PD_FacilityCategories_SYS_Facilities");
            });

            modelBuilder.Entity<PD_LabTest>(entity =>
            {
                entity.HasKey(e => e.LabTestId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Name).HasMaxLength(250);

                entity.Property(e => e.ProductType).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);
            });

            modelBuilder.Entity<PH_PharmaciesStorageType>(entity =>
            {
                entity.HasKey(e => e.PharmacyStorageId);

                entity.Property(e => e.StorageType).HasMaxLength(50);
            });

            modelBuilder.Entity<PH_PharmacyNote>(entity =>
            {
                entity.HasKey(e => e.PharmacyNoteId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<PT_CouponUsage>(entity =>
            {
                entity.HasKey(e => e.CouponUsageId);

                entity.ToTable("PT_CouponUsage");

                entity.HasIndex(e => e.CouponCodeId, "IX_PT_CouponUsage_CouponCodeId");

                entity.HasIndex(e => new { e.PatientId, e.BundleId, e.IsActive }, "IX_PT_CouponUsage_PatientId_BundleId");

                entity.HasIndex(e => e.PatientOrderId, "IX_PT_CouponUsage_PatientOrderId");

                entity.HasIndex(e => e.PatientPaymentId, "IX_PT_CouponUsage_PatientPaymentId");

                entity.Property(e => e.CouponCode).HasMaxLength(250);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.UsedDate).HasColumnType("datetime");

                entity.HasOne(d => d.Bundle)
                    .WithMany(p => p.PT_CouponUsages)
                    .HasForeignKey(d => d.BundleId)
                    .HasConstraintName("FK_PT_CouponUsage_Bundle");

                entity.HasOne(d => d.CouponCodeNavigation)
                    .WithMany(p => p.PT_CouponUsages)
                    .HasForeignKey(d => d.CouponCodeId)
                    .HasConstraintName("FK_PT_CouponUsage_CouponCode");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.PT_CouponUsages)
                    .HasForeignKey(d => d.PatientId)
                    .HasConstraintName("FK_PT_CouponUsage_Patient");

                entity.HasOne(d => d.PatientOrder)
                    .WithMany(p => p.PT_CouponUsages)
                    .HasForeignKey(d => d.PatientOrderId)
                    .HasConstraintName("FK_PT_CouponUsage_PatientOrder");

                entity.HasOne(d => d.PatientPayment)
                    .WithMany(p => p.PT_CouponUsages)
                    .HasForeignKey(d => d.PatientPaymentId)
                    .HasConstraintName("FK_PT_CouponUsage_PatientPayment");
            });

            modelBuilder.Entity<PT_MedicineSupply>(entity =>
            {
                entity.HasKey(e => e.MedicineSupplyId)
                    .HasName("PK__PT_Medic__0028B5C981E784EF");

                entity.ToTable("PT_MedicineSupply");

                entity.Property(e => e.SupplyItemDesignatorID).HasMaxLength(64);

                entity.Property(e => e.SupplyQuantity).HasMaxLength(50);
                entity.Property(e => e.Direction).HasMaxLength(500);

                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.WholesalePrice).HasColumnType("decimal(18, 4)");
            });

            modelBuilder.Entity<PT_PatientProfileNote>(entity =>
            {
                entity.HasKey(e => e.PatientProfileNoteId);

                entity.HasIndex(e => e.PatientId, "IX_PT_PatientProfileNotes_PatientId");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.Property(e => e.NoteText).HasMaxLength(4000);

                entity.HasOne(d => d.Patient)
                    .WithMany()
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PT_PatientProfileNote_PT_Patient");
            });

            modelBuilder.Entity<PT_Patient>(entity =>
            {
                entity.HasKey(e => e.PatientId);

                entity.HasIndex(e => new { e.IsActive, e.Status, e.FacilityId }, "IX_PT_Patients_Active");

                entity.Property(e => e.Address).HasMaxLength(250);

                entity.Property(e => e.ArchievedDate).HasColumnType("datetime");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.DOB).HasColumnType("datetime");

                entity.Property(e => e.Email).HasMaxLength(250);

                entity.Property(e => e.FirstName).HasMaxLength(250);

                entity.Property(e => e.Gender).HasMaxLength(250);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.LastName).HasMaxLength(250);

                entity.Property(e => e.MRN).HasMaxLength(250);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Phone).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.Street).HasMaxLength(250);

                entity.Property(e => e.WaitListReason).HasMaxLength(1000);

                entity.Property(e => e.Zipcode).HasMaxLength(50);
            });

            modelBuilder.Entity<PT_PatientAppointmentSlot>(entity =>
            {
                entity.HasKey(e => e.PatientAppointmentSlotId);

                entity.HasIndex(e => e.ZoomMeetingId, "IX_PAS_ZoomMeetingId");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.StartDate).HasColumnType("datetime");

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.Title).HasMaxLength(500);

                entity.Property(e => e.VonageSessionId).HasMaxLength(500);

                entity.Property(e => e.VonageTokenId).HasMaxLength(500);

                entity.Property(e => e.ZoomCreatedAt).HasColumnType("datetime");

                entity.Property(e => e.ZoomHostEmail).HasMaxLength(256);

                entity.Property(e => e.ZoomJoinUrl).HasMaxLength(512);

                entity.Property(e => e.ZoomMeetingId).HasMaxLength(32);

                entity.Property(e => e.ZoomPassword).HasMaxLength(64);

                entity.Property(e => e.ZoomStartUrl).HasMaxLength(512);

                entity.Property(e => e.ZoomStatus).HasMaxLength(32);

                entity.Property(e => e.ZoomUUID).HasMaxLength(128);
            });

            modelBuilder.Entity<PT_PatientCardDetail>(entity =>
            {
                entity.HasKey(e => e.PatientCardId);

                entity.Property(e => e.CVC).HasMaxLength(50);

                entity.Property(e => e.CardNumber).HasMaxLength(250);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ExpirationDate).HasColumnType("date");
            });

            modelBuilder.Entity<PT_PatientOrder>(entity =>
            {
                entity.HasKey(e => e.PatientOrderId);

                entity.Property(e => e.Address).HasMaxLength(500);

                entity.Property(e => e.CouponCode).HasMaxLength(500);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.FacilityGuid).HasMaxLength(100);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.LabelStatus).HasMaxLength(50);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.OrderDiscount).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.OrderPayableAmount).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.OrderStatus).HasMaxLength(50);

                entity.Property(e => e.OrderTotal).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.ShippedDate).HasColumnType("datetime");

                entity.Property(e => e.SubscriptionStatus).HasMaxLength(50);

                entity.Property(e => e.TrackingNumber).HasMaxLength(100);

                entity.Property(e => e.VisitStatus).HasMaxLength(50);
            });

            modelBuilder.Entity<PT_PatientOrderManualFulfillment>(entity =>
            {
                entity.HasKey(e => e.PatientOrderManualFulfillmentId)
                    .HasName("PK_PT_PatientOrderManualFulfillment");

                entity.ToTable("PT_PatientOrderManualFulfillment");

                entity.HasIndex(e => e.PatientOrderId, "UX_PT_PatientOrderManualFulfillment_PatientOrderId")
                    .IsUnique();

                entity.Property(e => e.DateShipped).HasColumnType("datetime2(7)");
                entity.Property(e => e.FulfilledAt).HasColumnType("datetime2(7)");
                entity.Property(e => e.FulfilledBy).HasMaxLength(200);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.OrderNumber).HasMaxLength(100);
                entity.Property(e => e.ShippingProvider).HasMaxLength(50);
                entity.Property(e => e.TrackingNumber).HasMaxLength(100);
                entity.Property(e => e.TrackingUrl).HasMaxLength(500);

                entity.HasOne(d => d.PatientOrder)
                    .WithMany()
                    .HasForeignKey(d => d.PatientOrderId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_PT_PatientOrderManualFulfillment_PT_PatientOrder");
            });

            modelBuilder.Entity<PT_PatientPaymentDetail>(entity =>
            {
                entity.HasKey(e => e.PatientPaymentId);

                entity.Property(e => e.CouponCode).HasMaxLength(250);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.DiscountPrice).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.FacilityGuid).HasMaxLength(100);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PaymentStatus).HasMaxLength(50);

                entity.Property(e => e.ShipmentAddress).HasMaxLength(250);

                entity.Property(e => e.ShipmentStreet).HasMaxLength(50);

                entity.Property(e => e.ShipmentZipCode).HasMaxLength(50);

                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18, 0)");
            });

            modelBuilder.Entity<PT_PatientPrescription>(entity =>
            {
                entity.HasKey(e => e.PatientPrescriptionId)
                    .HasName("PK_PT_PatientsPrescriptions");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ExpiryDate).HasColumnType("date");

                entity.Property(e => e.FacilityGuid).HasMaxLength(100);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PrescriptionStatus).HasMaxLength(50);

                entity.Property(e => e.PrescritionDate).HasColumnType("date");

                entity.Property(e => e.ProductType).HasMaxLength(50);
            });

            modelBuilder.Entity<PT_PatientPrescriptionSoapNote>(entity =>
            {
                entity.HasKey(e => e.SoapNoteId)
                    .HasName("PK__PT_Patie__F44F92EA1125250A");

                entity.ToTable("PT_PatientPrescriptionSoapNote");

                entity.HasIndex(e => e.PatientAppointmentSlotId, "IX_PPSN_Appointment");

                entity.HasIndex(e => e.PatientPrescriptionId, "IX_PPSN_Prescription");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.Guid).HasMaxLength(64);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.SignaturePath).HasMaxLength(512);

                entity.Property(e => e.SignedAt).HasColumnType("datetime");

                entity.Property(e => e.SignedBy).HasMaxLength(256);

                entity.Property(e => e.Status).HasMaxLength(32);

                entity.HasOne(d => d.PatientPrescription)
                    .WithMany(p => p.PT_PatientPrescriptionSoapNotes)
                    .HasForeignKey(d => d.PatientPrescriptionId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PPSN_Prescription");
            });

            modelBuilder.Entity<PT_PatientTreatment>(entity =>
            {
                entity.HasKey(e => e.PatientTreatmentId);

                entity.HasIndex(e => new { e.NextRecurringPaymentDate, e.IsRecurring, e.IsActive }, "IX_PT_PatientTreatments_NextRecurringPaymentDate");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ExpiryDate).HasColumnType("date");

                entity.Property(e => e.FacilityGuid).HasMaxLength(100);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.NextRecurringPaymentDate).HasColumnType("datetime");

                entity.Property(e => e.OriginalPaymentAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.RecurringCouponCodeId).HasColumnType("bigint");

                entity.Property(e => e.RecurringAmountAfterCoupon).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.RecurringStartDate).HasColumnType("datetime");

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.TreatmentStatus).HasMaxLength(50);
            });

            modelBuilder.Entity<PT_PatientTreatmentSoapNote>(entity =>
            {
                entity.HasKey(e => e.SoapNoteId);
                entity.ToTable("PT_PatientTreatmentSoapNote");
                entity.HasIndex(e => e.PatientTreatmentId, "IX_PT_PatientTreatmentSoapNotes_PatientTreatmentId");
                entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getutcdate())");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.Property(e => e.SignaturePath).HasMaxLength(512);
                entity.Property(e => e.SignedAt).HasColumnType("datetime");
                entity.Property(e => e.SignedBy).HasMaxLength(256);
                entity.Property(e => e.Status).HasMaxLength(32);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasOne(d => d.PatientTreatment)
                    .WithMany()
                    .HasForeignKey(d => d.PatientTreatmentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PT_PatientTreatmentSoapNote_PT_PatientTreatment");
            });

            modelBuilder.Entity<PT_PatientTreatmentDocument>(entity =>
            {
                entity.HasKey(e => e.PatientTreatmentDocumentId);

                entity.HasIndex(e => e.PatientTreatmentId, "IX_PT_PatientTreatmentDocuments_PatientTreatmentId");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Description).HasMaxLength(2000);

                entity.Property(e => e.DocumentName).HasMaxLength(500);

                entity.Property(e => e.DocumentUrl).HasMaxLength(2000);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.HasOne(d => d.PatientTreatment)
                    .WithMany(p => p.PT_PatientTreatmentDocuments)
                    .HasForeignKey(d => d.PatientTreatmentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PT_PatientTreatmentDocument_PT_PatientTreatment");
            });

            modelBuilder.Entity<PT_PatientDocument>(entity =>
            {
                entity.ToTable("PT_PatientDocument");

                entity.HasKey(e => e.PatientDocumentId);

                entity.HasIndex(e => e.PatientId, "IX_PT_PatientDocuments_PatientId");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Description).HasMaxLength(2000);

                entity.Property(e => e.DocumentName).HasMaxLength(500);

                entity.Property(e => e.DocumentUrl).HasMaxLength(2000);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.HasOne(d => d.Patient)
                    .WithMany()
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PT_PatientDocument_PT_Patients");
            });

            modelBuilder.Entity<PT_PatientTreatmentInTakeForm>(entity =>
            {
                entity.HasKey(e => e.PatientTreatmentInTakeFormId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ConsentHtml).HasColumnType("nvarchar(max)");

                entity.Property(e => e.Type).HasMaxLength(50);
            });

            modelBuilder.Entity<PT_PatientQuestionnaire>(entity =>
            {
                entity.HasKey(e => e.PatientQuestionnaireId);
                entity.ToTable("PT_PatientQuestionnaire");
                entity.HasIndex(e => new { e.PatientId, e.Status }, "IX_PT_PatientQuestionnaire_PatientId_Status");
                entity.HasIndex(e => e.QuestionnaireId, "IX_PT_PatientQuestionnaire_QuestionnaireId");
                entity.HasIndex(e => e.FacilityId, "IX_PT_PatientQuestionnaire_FacilityId");
                entity.Property(e => e.Status).HasMaxLength(32).HasDefaultValueSql("('Assigned')");
                entity.Property(e => e.AssignedDate).HasColumnType("datetime").HasDefaultValueSql("(getutcdate())");
                entity.Property(e => e.DueDate).HasColumnType("datetime");
                entity.Property(e => e.StartedDate).HasColumnType("datetime");
                entity.Property(e => e.SubmittedDate).HasColumnType("datetime");
                entity.Property(e => e.DraftSavedDate).HasColumnType("datetime");
                entity.Property(e => e.Guid).HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getutcdate())");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.HasOne(d => d.Patient)
                    .WithMany()
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PT_PatientQuestionnaire_PT_Patients");
                entity.HasOne(d => d.Questionnaire)
                    .WithMany()
                    .HasForeignKey(d => d.QuestionnaireId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PT_PatientQuestionnaire_SYS_Questionnaires");
            });

            modelBuilder.Entity<PT_PatientQuestionnaireAnswer>(entity =>
            {
                entity.HasKey(e => e.PatientQuestionnaireAnswerId);
                entity.ToTable("PT_PatientQuestionnaireAnswer");
                entity.HasIndex(e => new { e.PatientQuestionnaireId, e.DisplayOrder }, "IX_PT_PatientQuestionnaireAnswer_PatientQuestionnaireId");
                entity.Property(e => e.FieldKey).HasMaxLength(128);
                entity.Property(e => e.Type).HasMaxLength(50);
                entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getutcdate())");
                entity.HasOne(d => d.PatientQuestionnaire)
                    .WithMany(p => p.PT_PatientQuestionnaireAnswers)
                    .HasForeignKey(d => d.PatientQuestionnaireId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PT_PatientQuestionnaireAnswer_PT_PatientQuestionnaire");
            });

            modelBuilder.Entity<PT_PrescriptionMedicine>(entity =>
            {
                entity.HasKey(e => e.PrescriptionMedicineId)
                    .HasName("PK__PT_Presc__2C5AC2369846482A");

                entity.HasIndex(e => e.PatientPrescriptionId, "IX_PrescriptionMedicines_PatientPrescriptionId");

                entity.Property(e => e.MedicineName).HasMaxLength(200);

                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.WholesalePrice).HasColumnType("decimal(18, 4)");

                entity.HasOne(d => d.PatientPrescription)
                    .WithMany(p => p.PT_PrescriptionMedicines)
                    .HasForeignKey(d => d.PatientPrescriptionId)
                    .HasConstraintName("FK_PrescriptionMedicines_PatientPrescriptions");
            });

            modelBuilder.Entity<Pt_PatientProduct>(entity =>
            {
                entity.HasKey(e => e.PatientProductId)
                    .HasName("PK__Pt_Patie__5AAB4B91062C3E21");

                entity.ToTable("Pt_PatientProduct");

                entity.Property(e => e.PatientProductId).ValueGeneratedNever();
            });

            modelBuilder.Entity<SYS_AuditLog>(entity =>
            {
                entity.HasKey(e => e.AuditLogId);

                entity.ToTable("SYS_AuditLog");

                entity.HasIndex(e => e.Action, "IX_AuditLog_Action");

                entity.HasIndex(e => e.CreatedDate, "IX_AuditLog_CreatedDate");

                entity.HasIndex(e => e.EntityId, "IX_AuditLog_EntityId");

                entity.HasIndex(e => e.EntityType, "IX_AuditLog_EntityType");

                entity.HasIndex(e => e.PatientId, "IX_AuditLog_PatientId");

                entity.HasIndex(e => e.Status, "IX_AuditLog_Status");

                entity.HasIndex(e => e.UserId, "IX_AuditLog_UserId");

                entity.Property(e => e.Action).HasMaxLength(100);

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.EntityType).HasMaxLength(100);

                entity.Property(e => e.IpAddress).HasMaxLength(50);

                entity.Property(e => e.RequestMethod).HasMaxLength(10);

                entity.Property(e => e.RequestPath).HasMaxLength(500);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.UserAgent).HasMaxLength(500);
            });

            modelBuilder.Entity<SYS_FailedEmailLog>(entity =>
            {
                entity.HasKey(e => e.FailedEmailLogId);

                entity.ToTable("SYS_FailedEmailLog");

                entity.HasIndex(e => e.FailedAtUtc, "IX_FailedEmailLog_FailedAtUtc");

                entity.Property(e => e.ToEmail).HasMaxLength(500);
                entity.Property(e => e.ToName).HasMaxLength(500);
                entity.Property(e => e.Subject).HasMaxLength(1000);
                entity.Property(e => e.ErrorMessage).HasMaxLength(4000);
                entity.Property(e => e.ExceptionType).HasMaxLength(500);
                entity.Property(e => e.FailedAtUtc).HasColumnType("datetime");
            });

            modelBuilder.Entity<SYS_Brand>(entity =>
            {
                entity.HasKey(e => e.BrandId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.FontStyle).HasMaxLength(250);

                entity.Property(e => e.Logo).HasMaxLength(1000);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PrimaryColor).HasMaxLength(250);

                entity.Property(e => e.SecondaryColor).HasMaxLength(250);
            });

            modelBuilder.Entity<SYS_BroadcastMessage>(entity =>
            {
                entity.HasKey(e => e.BroadcastMessageId);

                entity.Property(e => e.BroadcastDate).HasColumnType("datetime");

                entity.Property(e => e.Description).HasMaxLength(500);
            });

            modelBuilder.Entity<SYS_Chat>(entity =>
            {
                entity.HasIndex(e => e.ChannelId, "IX_SYS_Chats_ChannelId");

                entity.HasIndex(e => e.IndividualReceiverId, "IX_SYS_Chats_IndividualReceiverId");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.MessageType).HasMaxLength(50);

                entity.HasOne(d => d.Channel)
                    .WithMany(p => p.SYS_Chats)
                    .HasForeignKey(d => d.ChannelId)
                    .HasConstraintName("FK_SYS_Chats_SYS_ChatChannels");
            });

            modelBuilder.Entity<SYS_ChatChannel>(entity =>
            {
                entity.HasKey(e => e.ChannelId);

                entity.HasIndex(e => e.TreatmentId, "IX_SYS_ChatChannels_TreatmentId");

                entity.Property(e => e.ChannelName).HasMaxLength(500);

                entity.Property(e => e.ChannelType).HasMaxLength(50);

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<SYS_ChatChannelParticipant>(entity =>
            {
                entity.HasKey(e => e.ParticipantId);

                entity.HasIndex(e => e.ChannelId, "IX_SYS_ChatChannelParticipants_ChannelId");

                entity.HasIndex(e => e.UserId, "IX_SYS_ChatChannelParticipants_UserId");

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.Property(e => e.JoinedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.LeftDate).HasColumnType("datetime");

                entity.HasOne(d => d.Channel)
                    .WithMany(p => p.SYS_ChatChannelParticipants)
                    .HasForeignKey(d => d.ChannelId)
                    .HasConstraintName("FK_SYS_ChatChannelParticipants_SYS_ChatChannels");
            });

            modelBuilder.Entity<SYS_ChatReadReceipt>(entity =>
            {
                entity.ToTable("SYS_ChatReadReceipt");

                entity.HasIndex(e => e.ChatId, "IX_SYS_ChatReadReceipts_ChatId");

                entity.HasIndex(e => new { e.ChatId, e.UserId }, "IX_SYS_ChatReadReceipts_ChatId_UserId")
                    .IsUnique();

                entity.HasIndex(e => e.IsActive, "IX_SYS_ChatReadReceipts_IsActive")
                    .HasFilter("([IsActive]=(1))");

                entity.HasIndex(e => e.UserId, "IX_SYS_ChatReadReceipts_UserId");

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.Property(e => e.ReadDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.HasOne(d => d.Chat)
                    .WithMany(p => p.SYS_ChatReadReceipts)
                    .HasForeignKey(d => d.ChatId)
                    .HasConstraintName("FK_SYS_ChatReadReceipts_SYS_Chats");
            });

            modelBuilder.Entity<SYS_City>(entity =>
            {
                entity.HasNoKey();

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<SYS_Country>(entity =>
            {
                entity.HasNoKey();

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ShortName)
                    .HasMaxLength(3)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<SYS_CouponCode>(entity =>
            {
                entity.HasKey(e => e.CoupanCodeId);

                entity.ToTable("SYS_CouponCode");

                entity.HasIndex(e => new { e.CoupanCode, e.FacilityId }, "UX_SYS_CouponCode_Code_Facility")
                    .IsUnique();

                entity.Property(e => e.CoupanCode).HasMaxLength(64);

                entity.Property(e => e.CreatedDate)
                    .HasPrecision(0)
                    .HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.ExpiryDate).HasColumnType("date");

                entity.Property(e => e.AppliesToRecurring)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");
            });

            modelBuilder.Entity<SYS_CouponCode1>(entity =>
            {
                entity.HasKey(e => e.CoupanCodeId);

                entity.ToTable("SYS_CouponCodes");

                entity.Property(e => e.CoupanCode).HasMaxLength(50);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<SYS_Facility>(entity =>
            {
                entity.HasKey(e => e.FacilityId);

                entity.HasIndex(e => e.FacilityId, "IX_SYS_Facilities_Id");

                entity.Property(e => e.IsExternal).HasDefaultValueSql("((0))");

                entity.Property(e => e.Address).HasMaxLength(250);

                entity.Property(e => e.BillingAddress).HasMaxLength(250);

                entity.Property(e => e.BillingAddressType).HasMaxLength(50);

                entity.Property(e => e.BillingZipCode).HasMaxLength(50);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Email).HasMaxLength(250);

                entity.Property(e => e.FacilityContactEmail).HasMaxLength(500);

                entity.Property(e => e.FacilityContactName).HasMaxLength(500);

                entity.Property(e => e.FacilityContactPhone).HasMaxLength(50);

                entity.Property(e => e.Fax).HasMaxLength(50);

                entity.Property(e => e.FedearlTaxId).HasMaxLength(50);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.NPI).HasMaxLength(50);

                entity.Property(e => e.Phone).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.TitleLong).HasMaxLength(250);

                entity.Property(e => e.TitleShort).HasMaxLength(250);

                entity.Property(e => e.ZipCode).HasMaxLength(50);

                entity.Property(e => e.PaymentModeId).HasDefaultValue(1);
            });

            modelBuilder.Entity<SYS_ForgetPassword>(entity =>
            {
                entity.HasKey(e => e.ForgetPasswordId);

                entity.Property(e => e.Code).HasMaxLength(500);
            });

            modelBuilder.Entity<SYS_Login>(entity =>
            {
                entity.HasKey(e => e.LoginId)
                    .HasName("PK_Login");

                entity.HasIndex(e => e.RoleId, "IX_Login_RoleId");

                entity.Property(e => e.Email).HasMaxLength(200);

                entity.Property(e => e.Password).HasMaxLength(100);
            });

            modelBuilder.Entity<SYS_Notification>(entity =>
            {
                entity.HasKey(e => e.NotificationId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.NotificationType).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_Organization>(entity =>
            {
                entity.HasKey(e => e.OrganizationId);

                entity.Property(e => e.Address).HasMaxLength(250);

                entity.Property(e => e.BillingAddressType).HasMaxLength(50);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Email).HasMaxLength(250);

                entity.Property(e => e.Fax).HasMaxLength(50);

                entity.Property(e => e.FedearlTaxId).HasMaxLength(50);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.NPI).HasMaxLength(50);

                entity.Property(e => e.OrganizationContactEmail).HasMaxLength(500);

                entity.Property(e => e.OrganizationContactName).HasMaxLength(500);

                entity.Property(e => e.OrganizationContactPhone).HasMaxLength(50);

                entity.Property(e => e.OtherAddress).HasMaxLength(250);

                entity.Property(e => e.OtherZipCode).HasMaxLength(50);

                entity.Property(e => e.Phone).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.TitleLong).HasMaxLength(250);

                entity.Property(e => e.TitleShort).HasMaxLength(250);

                entity.Property(e => e.ZipCode).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_Pharmacy>(entity =>
            {
                entity.HasKey(e => e.PharmacyId);

                entity.Property(e => e.Accereditation).HasMaxLength(50);

                entity.Property(e => e.Address).HasMaxLength(250);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.DBAName).HasMaxLength(250);

                entity.Property(e => e.DEANumber).HasMaxLength(50);

                entity.Property(e => e.Email).HasMaxLength(250);

                entity.Property(e => e.EmergencyContactName).HasMaxLength(250);

                entity.Property(e => e.EmergencyPhone).HasMaxLength(50);

                entity.Property(e => e.FederalTaxId).HasMaxLength(50);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.InChargePharmacist).HasMaxLength(250);

                entity.Property(e => e.LegalBusinesName).HasMaxLength(250);

                entity.Property(e => e.LicenseExpiration).HasColumnType("datetime");

                entity.Property(e => e.MedicaidNumber).HasMaxLength(50);

                entity.Property(e => e.MedicarePTAN).HasMaxLength(50);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.NABPId).HasMaxLength(50);

                entity.Property(e => e.PharmacistLicense).HasMaxLength(100);

                entity.Property(e => e.PharmacyName).HasMaxLength(250);

                entity.Property(e => e.PharmacyType).HasMaxLength(50);

                entity.Property(e => e.PhoneNumber).HasMaxLength(50);

                entity.Property(e => e.StateCSLicense).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_Product>(entity =>
            {
                entity.HasKey(e => e.ProductId)
                    .HasName("PK_PD_Durgs");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ProductName).HasMaxLength(1000);

                entity.Property(e => e.ProductType).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_ProviderGroup>(entity =>
            {
                entity.HasKey(e => e.GroupId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.GroupName).HasMaxLength(250);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<SYS_Questionnaire>(entity =>
            {
                entity.HasKey(e => e.QuestionnaireId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.Language).HasMaxLength(50);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.QuestionaireType).HasMaxLength(50);

                entity.Property(e => e.QuestionnaireName).HasMaxLength(1000);

                entity.Property(e => e.Review).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);
            });

            // TEL-19 - clinical code sets. See Sql/Create_SYS_ClinicalCodeSets.sql.
            modelBuilder.Entity<SYS_CodeSetVersion>(entity =>
            {
                entity.HasKey(e => e.CodeSetVersionId);
                entity.ToTable("SYS_CodeSetVersion");
                entity.HasIndex(e => new { e.CodeSystem, e.VersionLabel }, "UX_SYS_CodeSetVersion_System_Label").IsUnique();
                entity.HasIndex(e => new { e.CodeSystem, e.EffectiveDate }, "IX_SYS_CodeSetVersion_System_Effective");
                entity.Property(e => e.CodeSystem).HasMaxLength(16);
                entity.Property(e => e.VersionLabel).HasMaxLength(32);
                entity.Property(e => e.EffectiveDate).HasColumnType("date");
                entity.Property(e => e.TerminationDate).HasColumnType("date");
                entity.Property(e => e.SourceFileName).HasMaxLength(260);
                entity.Property(e => e.ImportedDate).HasColumnType("datetime");
                entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getutcdate())");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<SYS_Icd10Code>(entity =>
            {
                entity.HasKey(e => e.Icd10CodeId);
                entity.ToTable("SYS_Icd10Code");
                entity.HasIndex(e => new { e.CodeSetVersionId, e.Code }, "UX_SYS_Icd10Code_Version_Code").IsUnique();
                entity.HasIndex(e => e.Code, "IX_SYS_Icd10Code_Code");
                entity.Property(e => e.Code).HasMaxLength(8);
                entity.Property(e => e.DisplayCode).HasMaxLength(9);
                entity.Property(e => e.ShortDescription).HasMaxLength(60);
                entity.Property(e => e.LongDescription).HasMaxLength(400);
                entity.Property(e => e.EffectiveDate).HasColumnType("date");
                entity.Property(e => e.TerminationDate).HasColumnType("date");
                entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getutcdate())");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.HasOne(d => d.CodeSetVersion)
                    .WithMany()
                    .HasForeignKey(d => d.CodeSetVersionId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SYS_Icd10Code_SYS_CodeSetVersion");
            });

            modelBuilder.Entity<SYS_CptCode>(entity =>
            {
                entity.HasKey(e => e.CptCodeId);
                entity.ToTable("SYS_CptCode");
                entity.HasIndex(e => new { e.CodeSetVersionId, e.Code }, "UX_SYS_CptCode_Version_Code").IsUnique();
                entity.HasIndex(e => e.Code, "IX_SYS_CptCode_Code");
                entity.Property(e => e.Code).HasMaxLength(5);
                entity.Property(e => e.ShortDescription).HasMaxLength(60);
                entity.Property(e => e.LongDescription).HasMaxLength(1000);
                entity.Property(e => e.EffectiveDate).HasColumnType("date");
                entity.Property(e => e.TerminationDate).HasColumnType("date");
                entity.Property(e => e.CreatedDate).HasColumnType("datetime").HasDefaultValueSql("(getutcdate())");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.HasOne(d => d.CodeSetVersion)
                    .WithMany()
                    .HasForeignKey(d => d.CodeSetVersionId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SYS_CptCode_SYS_CodeSetVersion");
            });

            modelBuilder.Entity<SYS_QuestionnaireFacilityJson>(entity =>
            {
                entity.HasKey(e => e.QuestionnaireFacilityJsonId)
                    .HasName("PK_SQFJ");

                entity.ToTable("SYS_QuestionnaireFacilityJson");

                entity.HasIndex(e => e.FacilityId, "IX_SQFJ_Facility");

                entity.HasIndex(e => e.QuestionnaireId, "IX_SQFJ_Questionnaire");

                entity.HasIndex(e => new { e.QuestionnaireId, e.FacilityId }, "UX_SQFJ_QId_FId_Active")
                    .IsUnique()
                    .HasFilter("([IsActive]=(1))");

                entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.HasOne(d => d.Facility)
                    .WithMany(p => p.SYS_QuestionnaireFacilityJsons)
                    .HasForeignKey(d => d.FacilityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SQFJ_Facilities");

                entity.HasOne(d => d.Questionnaire)
                    .WithMany(p => p.SYS_QuestionnaireFacilityJsons)
                    .HasForeignKey(d => d.QuestionnaireId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_SQFJ_Questionnaires");
            });

            modelBuilder.Entity<SYS_QuestionnairesInProduct>(entity =>
            {
                entity.HasKey(e => e.QuestionnaireInProductId);

                entity.Property(e => e.QuestionaireType).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_State>(entity =>
            {
                entity.HasNoKey();

                entity.Property(e => e.Name)
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ShortName).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_Subscription>(entity =>
            {
                entity.HasKey(e => e.SubscriptionId);

                entity.Property(e => e.AnnualPrice).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.BillingCycle).HasMaxLength(50);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.MonthlyPrice).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.PlanName).HasMaxLength(250);

                entity.Property(e => e.SetupFee).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.SupportLevel).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_SupervisorProvider>(entity =>
            {
                entity.HasKey(e => e.SupervisorProviderId);
            });

            modelBuilder.Entity<SYS_Ticket>(entity =>
            {
                entity.HasKey(e => e.TicketId);

                entity.Property(e => e.ContactEmail).HasMaxLength(500);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Priority).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.Subject).HasMaxLength(1000);

                entity.Property(e => e.Type).HasMaxLength(50);
            });

            modelBuilder.Entity<SYS_UserCard>(entity =>
            {
                entity.HasKey(e => e.CardId)
                    .HasName("PK__SYS_User__55FECDAE6E89F99D");

                entity.ToTable("SYS_UserCard");

                entity.Property(e => e.CardBrand).HasMaxLength(50);

                entity.Property(e => e.CardHolderName).HasMaxLength(250);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.Currency).HasMaxLength(250);

                entity.Property(e => e.ExpirationMonth).HasMaxLength(2);

                entity.Property(e => e.ExpirationYear).HasMaxLength(4);

                entity.Property(e => e.Last4).HasMaxLength(4);

                entity.Property(e => e.SquareCardId).HasMaxLength(100);

                entity.Property(e => e.SquareClientId).HasMaxLength(255);

                entity.Property(e => e.StripePaymentMethodId).HasMaxLength(255);

                entity.Property(e => e.StripeCustomerId).HasMaxLength(255);

                entity.Property(e => e.StripeAccountId).HasMaxLength(255);

                entity.HasOne(d => d.User)
                    .WithMany(p => p.SYS_UserCards)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_SYS_UserCard_SYS_UserDetails");
            });

            modelBuilder.Entity<SYS_UserDetail>(entity =>
            {
                entity.HasKey(e => e.UserId)
                    .HasName("PK_TheraUser");

                entity.HasIndex(e => new { e.IsActive, e.Status, e.UserId }, "IX_SYS_UserDetails_ActiveStatus");

                entity.Property(e => e.Address).HasMaxLength(50);

                entity.Property(e => e.AddressType).HasMaxLength(50);

                entity.Property(e => e.CAQHId).HasMaxLength(50);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.DEA).HasMaxLength(50);

                entity.Property(e => e.DOB).HasColumnType("date");

                entity.Property(e => e.Email).HasMaxLength(250);

                entity.Property(e => e.FirstName).HasMaxLength(200);

                entity.Property(e => e.Gender).HasMaxLength(50);

                entity.Property(e => e.Guid).HasMaxLength(100);

                entity.Property(e => e.IsFirstUse).HasDefaultValueSql("((1))");

                entity.Property(e => e.LastName).HasMaxLength(200);

                entity.Property(e => e.License).HasMaxLength(50);

                entity.Property(e => e.Medicaid).HasMaxLength(50);

                entity.Property(e => e.MiddleName).HasMaxLength(200);

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.NPI).HasMaxLength(50);

                entity.Property(e => e.Phone).HasMaxLength(20);

                entity.Property(e => e.ProfileUrl).HasMaxLength(500);

                entity.Property(e => e.ProviderType).HasMaxLength(50);

                entity.Property(e => e.SSN).HasMaxLength(50);

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.TaxId).HasMaxLength(50);

                entity.Property(e => e.Title).HasMaxLength(200);

                entity.Property(e => e.ZipCode).HasMaxLength(20);

                entity.Property(e => e.StripePlatformCustomerId).HasMaxLength(255);

                entity.HasOne(d => d.Login)
                    .WithMany(p => p.SYS_UserDetails)
                    .HasForeignKey(d => d.LoginId)
                    .HasConstraintName("FK_SYS_Users_SYS_Logins");
            });

            modelBuilder.Entity<StgFacility>(entity =>
            {
                entity.HasNoKey();

                entity.Property(e => e.Address).HasMaxLength(250);

                entity.Property(e => e.BillingAddress).HasMaxLength(250);

                entity.Property(e => e.BillingAddressType).HasMaxLength(50);

                entity.Property(e => e.BillingZipCode).HasMaxLength(20);

                entity.Property(e => e.Email).HasMaxLength(200);

                entity.Property(e => e.FacilityName).HasMaxLength(200);

                entity.Property(e => e.Phone).HasMaxLength(50);

                entity.Property(e => e.ZipCode).HasMaxLength(20);
            });

            modelBuilder.Entity<Sys_EmpowerOrder>(entity =>
            {
                entity.HasKey(e => e.EmpowerOrdersId);

                entity.HasIndex(e => e.ClientOrderId, "IX_Sys_EmpowerOrders_ClientOrderId");

                entity.HasIndex(e => e.CreatedDate, "IX_Sys_EmpowerOrders_CreatedDate");

                entity.HasIndex(e => e.EipOrderId, "IX_Sys_EmpowerOrders_EipOrderId");

                entity.HasIndex(e => e.OrderStatus, "IX_Sys_EmpowerOrders_OrderStatus");

                entity.HasIndex(e => e.PatientId, "IX_Sys_EmpowerOrders_PatientId");

                entity.HasIndex(e => e.PatientPrescriptionId, "IX_Sys_EmpowerOrders_PatientPrescriptionId");

                entity.Property(e => e.ClientOrderId).HasMaxLength(255);

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.Property(e => e.LfOrderId).HasMaxLength(100);

                entity.Property(e => e.LfPatientId).HasMaxLength(100);

                entity.Property(e => e.LfReferenceId).HasMaxLength(100);

                entity.Property(e => e.MessageId).HasMaxLength(100);

                entity.Property(e => e.OrderStatus).HasMaxLength(50);

                entity.Property(e => e.Reference1).HasMaxLength(255);

                entity.Property(e => e.Reference2).HasMaxLength(255);

                entity.Property(e => e.Reference3).HasMaxLength(255);

                entity.Property(e => e.Reference4).HasMaxLength(255);

                entity.Property(e => e.Reference5).HasMaxLength(255);

                entity.Property(e => e.SalesForceClinicAccountId).HasMaxLength(100);

                entity.Property(e => e.SalesForceOrderId).HasMaxLength(100);

                entity.Property(e => e.ShipmentProvider).HasMaxLength(50);

                entity.Property(e => e.ShipmentStatus).HasMaxLength(50);

                entity.Property(e => e.ShipmentTrackingNumber).HasMaxLength(100);

                entity.Property(e => e.ShipmentTrackingUrl).HasMaxLength(500);

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.Sys_EmpowerOrders)
                    .HasForeignKey(d => d.PatientId)
                    .HasConstraintName("FK_Sys_EmpowerOrders_PT_Patients");

                entity.HasOne(d => d.PatientPrescription)
                    .WithMany(p => p.Sys_EmpowerOrders)
                    .HasForeignKey(d => d.PatientPrescriptionId)
                    .HasConstraintName("FK_Sys_EmpowerOrders_PT_PatientPrescription");
            });

            modelBuilder.Entity<Sys_FacilitySquareCred>(entity =>
            {
                entity.HasKey(e => e.FacilitySquareCredId)
                    .HasName("PK__Sys_Faci__6F2711FD313CAB6F");

                entity.ToTable("Sys_FacilitySquareCred");

                entity.Property(e => e.AccessToken)
                    .HasMaxLength(1000)
                    .IsUnicode(false);

                entity.Property(e => e.RefreshToken)
                    .HasMaxLength(1000)
                    .IsUnicode(false);

                entity.Property(e => e.TokenExpiresAt).HasColumnType("datetime");

                entity.Property(e => e.MerchantId)
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.ApplicationId)
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.LocationId)
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.HasOne(d => d.Facility)
                    .WithMany(p => p.Sys_FacilitySquareCreds)
                    .HasForeignKey(d => d.FacilityId)
                    .HasConstraintName("FK__Sys_Facil__Facil__278FA59B");
            });

            modelBuilder.Entity<Sys_FacilityStripeConnect>(entity =>
            {
                entity.HasKey(e => e.FacilityStripeConnectId)
                    .HasName("PK_Sys_FacilityStripeConnect");

                entity.ToTable("Sys_FacilityStripeConnect");

                entity.Property(e => e.StripeAccountId)
                    .HasMaxLength(255)
                    .IsUnicode(false);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.HasOne(d => d.Facility)
                    .WithMany(p => p.Sys_FacilityStripeConnects)
                    .HasForeignKey(d => d.FacilityId)
                    .HasConstraintName("FK_Sys_FacilityStripeConnect_FacilityId");
            });

            modelBuilder.Entity<Sys_FullscriptOAuth>(entity =>
            {
                entity.HasKey(e => e.FullscriptOAuthId)
                    .HasName("PK__Sys_Full__FullscriptOAuthId");

                entity.ToTable("Sys_FullscriptOAuth");

                entity.Property(e => e.AccessToken)
                    .HasMaxLength(1000)
                    .IsUnicode(false);

                entity.Property(e => e.RefreshToken)
                    .HasMaxLength(1000)
                    .IsUnicode(false);

                entity.Property(e => e.TokenExpiresAt).HasColumnType("datetime");
                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.HasOne(d => d.Facility)
                    .WithMany(p => p.Sys_FullscriptOAuths)
                    .HasForeignKey(d => d.FacilityId)
                    .HasConstraintName("FK__Sys_FullscriptOAuth__FacilityId");
            });

            modelBuilder.Entity<Sys_Invoice>(entity =>
            {
                entity.HasKey(e => e.InvoiceId)
                    .HasName("PK__Invoices__D796AAB5E04F9DED");

                entity.Property(e => e.Amount).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.CustomerName)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.InvoiceDate).HasColumnType("datetime");

                entity.Property(e => e.InvoiceNumber)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.InvoiceType)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Status)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.Sys_Invoices)
                    .HasForeignKey(d => d.PatientId)
                    .HasConstraintName("FK_Sys_Invoices_PT_Patients");

                entity.HasOne(d => d.Subscription)
                    .WithMany(p => p.Sys_Invoices)
                    .HasForeignKey(d => d.SubscriptionId)
                    .HasConstraintName("FK_Invoices_Subscription");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Sys_Invoices)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_SYS_Invoices_SYS_UserDetails_UserId");
            });

            modelBuilder.Entity<Sys_InvoiceLineItem>(entity =>
            {
                entity.HasKey(e => e.InvoiceLineItemId)
                    .HasName("PK_Sys_InvoiceLineItems");

                entity.ToTable("Sys_InvoiceLineItem");

                entity.HasIndex(e => e.BundleId, "IX_Sys_InvoiceLineItem_BundleId")
                    .HasFilter("([BundleId] IS NOT NULL)");

                entity.HasIndex(e => e.DrugId, "IX_Sys_InvoiceLineItem_DrugId")
                    .HasFilter("([DrugId] IS NOT NULL)");

                entity.HasIndex(e => e.InvoiceId, "IX_Sys_InvoiceLineItem_InvoiceId");

                entity.HasIndex(e => e.ProductId, "IX_Sys_InvoiceLineItem_ProductId")
                    .HasFilter("([ProductId] IS NOT NULL)");

                entity.Property(e => e.CouponCode).HasMaxLength(50);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.DiscountedPrice).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.LineTotal).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.ProductLineItemName).HasMaxLength(500);

                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 4)");

                entity.HasOne(d => d.Invoice)
                    .WithMany(p => p.Sys_InvoiceLineItems)
                    .HasForeignKey(d => d.InvoiceId)
                    .HasConstraintName("FK_Sys_InvoiceLineItem_Sys_Invoices");
            });

            modelBuilder.Entity<Sys_InvoicePayment>(entity =>
            {
                entity.HasKey(e => e.InvoicePaymentId)
                    .HasName("PK_Sys_InvoicePayments");

                entity.ToTable("Sys_InvoicePayment");

                entity.HasIndex(e => e.InvoiceId, "IX_Sys_InvoicePayment_InvoiceId");

                entity.HasIndex(e => e.IsActive, "IX_Sys_InvoicePayment_IsActive")
                    .HasFilter("([IsActive]=(1))");

                entity.HasIndex(e => e.PaidByFacilityId, "IX_Sys_InvoicePayment_PaidByFacilityId")
                    .HasFilter("([PaidByFacilityId] IS NOT NULL)");

                entity.HasIndex(e => e.PaidByPatientId, "IX_Sys_InvoicePayment_PaidByPatientId")
                    .HasFilter("([PaidByPatientId] IS NOT NULL)");

                entity.HasIndex(e => e.PaymentDate, "IX_Sys_InvoicePayment_PaymentDate");

                entity.HasIndex(e => e.PaymentStatus, "IX_Sys_InvoicePayment_PaymentStatus");

                entity.Property(e => e.CreatedDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getutcdate())");

                entity.Property(e => e.IsActive).HasDefaultValueSql("((1))");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.PaymentAmount).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.PaymentDate).HasColumnType("datetime");

                entity.Property(e => e.PaymentMethod).HasMaxLength(50);

                entity.Property(e => e.PaymentNotes).HasMaxLength(1000);

                entity.Property(e => e.PaymentStatus).HasMaxLength(50);

                entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.RefundDate).HasColumnType("datetime");

                entity.Property(e => e.RefundReason).HasMaxLength(500);

                entity.Property(e => e.RefundTransactionId).HasMaxLength(255);

                entity.Property(e => e.SquarePaymentId).HasMaxLength(255);

                entity.Property(e => e.TransactionId).HasMaxLength(255);

                entity.HasOne(d => d.Card)
                    .WithMany(p => p.Sys_InvoicePayments)
                    .HasForeignKey(d => d.CardId)
                    .HasConstraintName("FK_Sys_InvoicePayment_SYS_UserCard");

                entity.HasOne(d => d.Invoice)
                    .WithMany(p => p.Sys_InvoicePayments)
                    .HasForeignKey(d => d.InvoiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Sys_InvoicePayment_Sys_Invoices");

                entity.HasOne(d => d.PaidByFacility)
                    .WithMany(p => p.Sys_InvoicePayments)
                    .HasForeignKey(d => d.PaidByFacilityId)
                    .HasConstraintName("FK_Sys_InvoicePayment_SYS_Facilities");

                entity.HasOne(d => d.PaidByPatient)
                    .WithMany(p => p.Sys_InvoicePayments)
                    .HasForeignKey(d => d.PaidByPatientId)
                    .HasConstraintName("FK_Sys_InvoicePayment_PT_Patients");

                entity.HasOne(d => d.PaidByUser)
                    .WithMany(p => p.Sys_InvoicePayments)
                    .HasForeignKey(d => d.PaidByUserId)
                    .HasConstraintName("FK_Sys_InvoicePayment_SYS_UserDetails");
            });

            modelBuilder.Entity<TK_TicketComment>(entity =>
            {
                entity.HasKey(e => e.TicketCommentId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            });

            modelBuilder.Entity<TK_TicketFile>(entity =>
            {
                entity.HasKey(e => e.TicketFileId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.Property(e => e.TicketFileName).HasMaxLength(500);
            });

            modelBuilder.Entity<UR_ProviderCategory>(entity =>
            {
                entity.HasKey(e => e.ProviderCategoryId);
            });

            modelBuilder.Entity<UR_ProviderScheduledSlot>(entity =>
            {
                entity.HasKey(e => e.ProviderScheduledSlotId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.SlotDate).HasColumnType("date");

                entity.Property(e => e.StartTimeUtc).HasColumnType("datetime2");

                entity.Property(e => e.EndTimeUtc).HasColumnType("datetime2");

                entity.Property(e => e.Status).HasMaxLength(50);

                entity.Property(e => e.SourceType).HasMaxLength(20);

                entity.Property(e => e.Title).HasMaxLength(500);

                entity.HasOne(d => d.SourceOverride)
                    .WithMany()
                    .HasForeignKey(d => d.SourceOverrideId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_UR_ProviderScheduledSlots_UR_ProviderDateOverrides");

                entity.HasIndex(e => new { e.ProviderId, e.StartTimeUtc })
                    .HasDatabaseName("UX_UR_ProviderScheduledSlots_ProviderId_StartTimeUtc")
                    .IsUnique()
                    .HasFilter("[IsActive] = 1 AND [StartTimeUtc] IS NOT NULL");

                entity.HasIndex(e => new { e.ProviderId, e.StartTimeUtc, e.Status })
                    .HasDatabaseName("IX_UR_ProviderScheduledSlots_Calendar");
            });

            modelBuilder.Entity<UR_ProviderWeeklyTemplate>(entity =>
            {
                entity.HasKey(e => e.ProviderWeeklyTemplateId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.Property(e => e.LastMaterializedDate).HasColumnType("date");
                entity.Property(e => e.Timezone).HasMaxLength(64).IsRequired();
                entity.Property(e => e.DefaultSlotDurationMinutes).HasDefaultValue(10);
                entity.Property(e => e.MaterializationHorizonDays).HasDefaultValue(30);

                entity.HasIndex(e => e.ProviderId)
                    .HasDatabaseName("UX_UR_ProviderWeeklyTemplates_ProviderId")
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
            });

            modelBuilder.Entity<UR_ProviderDayHours>(entity =>
            {
                entity.HasKey(e => e.ProviderDayHoursId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");

                entity.HasOne(d => d.ProviderWeeklyTemplate)
                    .WithMany(p => p.UR_ProviderDayHours)
                    .HasForeignKey(d => d.ProviderWeeklyTemplateId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_UR_ProviderDayHours_UR_ProviderWeeklyTemplates");

                entity.HasIndex(e => new { e.ProviderWeeklyTemplateId, e.DayOfWeek })
                    .HasDatabaseName("UX_UR_ProviderDayHours_TemplateId_DayOfWeek")
                    .IsUnique();
            });

            modelBuilder.Entity<UR_ProviderTimeRange>(entity =>
            {
                entity.HasKey(e => e.ProviderTimeRangeId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.Property(e => e.StartTimeLocal).HasColumnType("time");
                entity.Property(e => e.EndTimeLocal).HasColumnType("time");

                entity.HasOne(d => d.ProviderDayHours)
                    .WithMany(p => p.UR_ProviderTimeRanges)
                    .HasForeignKey(d => d.ProviderDayHoursId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_UR_ProviderTimeRanges_UR_ProviderDayHours");

                entity.HasIndex(e => new { e.ProviderDayHoursId, e.StartTimeLocal })
                    .HasDatabaseName("IX_UR_ProviderTimeRanges_DayHoursId_StartTimeLocal");
            });

            modelBuilder.Entity<UR_ProviderDateOverride>(entity =>
            {
                entity.HasKey(e => e.ProviderDateOverrideId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.Property(e => e.OverrideDate).HasColumnType("date");
                entity.Property(e => e.Note).HasMaxLength(500);

                entity.HasIndex(e => new { e.ProviderId, e.OverrideDate })
                    .HasDatabaseName("UX_UR_ProviderDateOverrides_ProviderId_OverrideDate")
                    .IsUnique()
                    .HasFilter("[IsActive] = 1");
            });

            modelBuilder.Entity<UR_ProviderDateOverrideTimeRange>(entity =>
            {
                entity.HasKey(e => e.ProviderDateOverrideTimeRangeId);

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");
                entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
                entity.Property(e => e.StartTimeLocal).HasColumnType("time");
                entity.Property(e => e.EndTimeLocal).HasColumnType("time");

                entity.HasOne(d => d.ProviderDateOverride)
                    .WithMany(p => p.UR_ProviderDateOverrideTimeRanges)
                    .HasForeignKey(d => d.ProviderDateOverrideId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_UR_ProviderDateOverrideTimeRanges_UR_ProviderDateOverrides");
            });

            modelBuilder.Entity<UR_ProviderStateLicense>(entity =>
            {
                entity.HasKey(e => e.ProviderStateLicenseId);

                entity.Property(e => e.StateLicense).HasMaxLength(500);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
