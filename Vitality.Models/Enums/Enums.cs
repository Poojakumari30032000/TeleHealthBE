using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.Enums
{
    public enum UserRole
    {
        [Description("Super Admin")]
        SuperAdmin = 1,

        [Description("Global Admin")]
        GlobalAdmin = 2,

        [Description("Clinic Admin")]
        ClinicAdmin = 3,

        [Description("Provider")]
        Provider = 4,

        [Description("Customer Support")]
        CustomerSupport = 5,

        [Description("Patient")]
        Patient = 6,
        [Description("Tech Support")]
        TechSupport = 7
    }

    public enum TicketPriority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum TicketStatus
    {
        Pending = 1,
        InProgress = 2,
        Review = 3,
        Closed = 4
    }

    public enum InvoiceType
    {
        ClinicToGlobal,
        PatientToClinic,
        ClinicToPatient,
        GAToClinic
    }
    public enum InvoiceStatus
    {
        Paid,
        Pending,
    }
    public enum CouponDiscountType : byte
    {
        Amount = 0,
        Percentage = 1
    }

    public enum FacilityPaymentMode
    {
        Square = 1,
        Stripe = 2
    }
}
