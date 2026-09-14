namespace Vitality.Models.Security
{
    /// <summary>
    /// Every permission code recognised by the application, as compile-time constants.
    /// <para>
    /// These are the SINGLE SOURCE OF TRUTH shared by three places that must agree
    /// byte-for-byte, or a permission silently never matches:
    /// </para>
    /// <list type="number">
    ///   <item>the <c>PermissionCode</c> column of <c>dbo.SYS_Permission</c>
    ///         (seeded by <c>Sql/Create_RBAC_Tables_And_Seed.sql</c>),</item>
    ///   <item><c>[RequiresPermission(...)]</c> on API endpoints,</item>
    ///   <item>the strings the Angular app passes to <c>PermissionsService.hasAnyPermission()</c>,
    ///         its route <c>data.permissions</c> arrays and its
    ///         <c>[appHasAnyPermission]</c> directive.</item>
    /// </list>
    /// <para>
    /// Two codes are misspelled - <c>avilability_*</c> and <c>questionnaier_*</c>. The
    /// misspellings are DELIBERATELY preserved: the Angular route guards and templates
    /// already compare these exact strings, so "fixing" them here would silently revoke
    /// access to the availability and questionnaire screens. Rename them in a dedicated
    /// change that updates the frontend, the seed script and the database together.
    /// </para>
    /// <para>
    /// Using a constant rather than a literal in an attribute means a typo is a build
    /// error instead of an endpoint that quietly denies everyone.
    /// </para>
    /// </summary>
    public static class Permissions
    {
        /// <summary>Module <c>dashboard</c> - 5 permission(s).</summary>
        public static class Dashboard
        {
            public const string Admin = "dashboard_admin";
            public const string Clinic = "dashboard_clinic";
            public const string Customer = "dashboard_customer";
            public const string Patient = "dashboard_patient";
            public const string Provider = "dashboard_provider";
        }

        /// <summary>Module <c>facility</c> - 4 permission(s).</summary>
        public static class Facility
        {
            public const string Add = "facility_add";
            public const string Delete = "facility_delete";
            public const string Edit = "facility_edit";
            public const string View = "facility_view";
        }

        /// <summary>Module <c>f</c> - 2 permission(s).</summary>
        public static class ClinicInfo
        {
            public const string Edit = "f_edit";
            public const string View = "f_view";
        }

        /// <summary>Module <c>calendar</c> - 1 permission(s).</summary>
        public static class Calendar
        {
            public const string View = "calendar_view";
        }

        /// <summary>Module <c>appointment</c> - 3 permission(s).</summary>
        public static class Appointment
        {
            public const string Delete = "appointment_delete";
            public const string Edit = "appointment_edit";
            public const string View = "appointment_view";
        }

        /// <summary>Module <c>avilability</c> - 4 permission(s).</summary>
        public static class Availability
        {
            public const string Add = "avilability_add";
            public const string Delete = "avilability_delete";
            public const string Edit = "avilability_edit";
            public const string View = "avilability_view";
        }

        /// <summary>Module <c>avilability_slot</c> - 3 permission(s).</summary>
        public static class AvailabilitySlot
        {
            public const string Delete = "avilability_slot_delete";
            public const string Edit = "avilability_slot_edit";
            public const string View = "avilability_slot_view";
        }

        /// <summary>Module <c>patient</c> - 4 permission(s).</summary>
        public static class Patient
        {
            public const string Add = "patient_add";
            public const string Delete = "patient_delete";
            public const string Edit = "patient_edit";
            public const string View = "patient_view";
        }

        /// <summary>Module <c>pt</c> - 1 permission(s).</summary>
        public static class PatientPortal
        {
            public const string View = "pt_view";
        }

        /// <summary>Module <c>user</c> - 4 permission(s).</summary>
        public static class User
        {
            public const string Add = "user_add";
            public const string Delete = "user_delete";
            public const string Edit = "user_edit";
            public const string View = "user_view";
        }

        /// <summary>Module <c>user_management</c> - 1 permission(s).</summary>
        public static class UserManagement
        {
            public const string Access = "user_management";
        }

        /// <summary>Module <c>product_category</c> - 4 permission(s).</summary>
        public static class ProductCategory
        {
            public const string Add = "product_category_add";
            public const string Delete = "product_category_delete";
            public const string Edit = "product_category_edit";
            public const string View = "product_category_view";
        }

        /// <summary>Module <c>product</c> - 4 permission(s).</summary>
        public static class Product
        {
            public const string Add = "product_add";
            public const string Delete = "product_delete";
            public const string Edit = "product_edit";
            public const string View = "product_view";
        }

        /// <summary>Module <c>pharmacy</c> - 4 permission(s).</summary>
        public static class Pharmacy
        {
            public const string Add = "pharmacy_add";
            public const string Delete = "pharmacy_delete";
            public const string Edit = "pharmacy_edit";
            public const string View = "pharmacy_view";
        }

        /// <summary>Module <c>questionnaier</c> - 5 permission(s).</summary>
        public static class Questionnaire
        {
            public const string Add = "questionnaier_add";
            public const string Delete = "questionnaier_delete";
            public const string Edit = "questionnaier_edit";
            public const string ListEdit = "questionnaier_list_edit";
            public const string View = "questionnaier_view";
        }

        /// <summary>Module <c>supportTicket</c> - 4 permission(s).</summary>
        public static class SupportTicket
        {
            public const string Add = "supportTicket_add";
            public const string Delete = "supportTicket_delete";
            public const string Edit = "supportTicket_edit";
            public const string View = "supportTicket_view";
        }

        /// <summary>Module <c>subscription_plan</c> - 4 permission(s).</summary>
        public static class SubscriptionPlan
        {
            public const string Add = "subscription_plan_add";
            public const string Delete = "subscription_plan_delete";
            public const string Edit = "subscription_plan_edit";
            public const string View = "subscription_plan_view";
        }

        /// <summary>Module <c>treatment</c> - 3 permission(s).</summary>
        public static class Treatment
        {
            public const string MoreButton = "treatment_more_button";
            public const string Update = "treatment_update";
            public const string View = "treatment_view";
        }

        /// <summary>Module <c>treatment_patient</c> - 2 permission(s).</summary>
        public static class PatientTreatment
        {
            public const string Edit = "treatment_patient_edit";
            public const string View = "treatment_patient_view";
        }

        /// <summary>Module <c>prescription</c> - 3 permission(s).</summary>
        public static class Prescription
        {
            public const string Edit = "prescription_edit";
            public const string RefillButton = "prescription_refill_button";
            public const string View = "prescription_view";
        }

        /// <summary>Module <c>prescription_timeLine</c> - 1 permission(s).</summary>
        public static class PrescriptionTimeline
        {
            public const string Comment = "prescription_timeLine_comment";
        }

        /// <summary>Module <c>order</c> - 2 permission(s).</summary>
        public static class Order
        {
            public const string Edit = "order_edit";
            public const string View = "order_view";
        }

        /// <summary>Module <c>payment</c> - 2 permission(s).</summary>
        public static class Payment
        {
            public const string Update = "payment_update";
            public const string View = "payment_view";
        }

        /// <summary>Module <c>branding</c> - 1 permission(s).</summary>
        public static class Branding
        {
            public const string View = "branding-view";
        }

        /// <summary>
        /// All permission codes declared above, resolved once by reflection.
        /// Used by the role editor to validate that a submitted permission id set
        /// contains nothing unknown, and by tests to assert that the constants and
        /// the seeded catalog have not drifted apart.
        /// </summary>
        public static IReadOnlyCollection<string> All { get; } = typeof(Permissions)
            .GetNestedTypes()
            .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Public
                                       | System.Reflection.BindingFlags.Static
                                       | System.Reflection.BindingFlags.FlattenHierarchy))
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
    }
}
