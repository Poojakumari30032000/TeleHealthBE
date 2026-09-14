using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.PatientOrders
{
    public class GetPatientOrderInfoResponseDTO
    {
        public string? OrderStatus { get; set; }
        public DateTime? OrderDeliveredDate { get; set; }
        public bool IsOrderEditable { get; set; } = true;

        public string? MRM { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? LastEditedDate { get; set; }

        public long? PatientId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? DOB { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        public long? TreatmentId { get; set; }
        public string? PatientTreatmentGuid { get; set; }

        public DateTime? OrderDate { get; set; }
        public string? CouponCode { get; set; }
        public decimal? OrderTotal { get; set; }
        public decimal? OrderDiscount { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? DateShipped { get; set; }
        public DateTime? DateArchieved { get; set; }
        public string? SubscriptionStatus { get; set; }
        public string? VisitStatus { get; set; }
        public string? LabelStatus { get; set; }

        public OrderProviderDTO? Provider { get; set; }

        public IList<OrderDrugItemDTO> Drugs { get; set; } = new List<OrderDrugItemDTO>();

        public IList<OrderMedicineDTO> Medicines { get; set; } = new List<OrderMedicineDTO>();

        public long? PrescriptionId { get; set; }

        public EmpowerTrackingInfoDTO? EmpowerTracking { get; set; }

        public bool IsManuallyFulfilled { get; set; }

        public ManualOrderTrackingInfoDTO? ManualTracking { get; set; }
    }

    public class ManualOrderTrackingInfoDTO
    {
        public string? OrderNumber { get; set; }
        public string? TrackingNumber { get; set; }
        public string? TrackingUrl { get; set; }
        public string? ShippingProvider { get; set; }
        public DateTime? DateShipped { get; set; }
        public string? Notes { get; set; }
        public string? FulfilledBy { get; set; }
        public DateTime? FulfilledAt { get; set; }
    }

    public class EmpowerTrackingInfoDTO
    {
        public string? ShipmentStatus { get; set; }
        public string? ShipmentTrackingNumber { get; set; }
        public string? ShipmentTrackingUrl { get; set; }
        public string? ShipmentProvider { get; set; }
        public DateTime? ShipmentStatusLastUpdatedTime { get; set; }
        public string? ClientOrderId { get; set; }
        public int? EipOrderId { get; set; }
        public string? LfOrderId { get; set; }
    }

    public class OrderProviderDTO
    {
        public long? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? ProviderType { get; set; }
        public string? NPI { get; set; }
        public string? DEA { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
    }

    public class OrderDrugItemDTO
    {

        public long PrescriptionMedicineId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public long? DrugId { get; set; }
        public string? DaysSupplies { get; set; }
        public string? Injection { get; set; }
        public string? InjectionQuantity { get; set; }
        public string? Needle { get; set; }
        public string? NeedleQuantity { get; set; }
        public string? Direction { get; set; }
        public string? Instruction { get; set; }
        public string? Quantity { get; set; }

        public string? DrugName { get; set; }
        public string? GenericName { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public decimal? Price { get; set; }
        public string? PackageSize { get; set; }

        public string? PharmacyName { get; set; }

        public bool? IsCustom { get; set; }
    }

    public class OrderMedicineSupplyDTO
    {
        public long MedicineSupplyId { get; set; }
        public long PrescriptionMedicineId { get; set; }
        public string? SupplyDesc { get; set; }
        public string? Direction { get; set; }
        public string? SupplyQuantity { get; set; }
        public string? SupplyItemDesignatorID { get; set; }
        public string? Name { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? TotalAmount { get; set; }
    }

    public class OrderMedicineDTO
    {

        public long PrescriptionMedicineId { get; set; }
        public long PatientPrescriptionId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public long? DrugId { get; set; }
        public long? CatalogId { get; set; }
        public string? CatalogName { get; set; }
        public string? DaysSupplies { get; set; }
        public string? Injection { get; set; }
        public string? InjectionQuantity { get; set; }
        public string? Needle { get; set; }
        public string? NeedleQuantity { get; set; }
        public string? Direction { get; set; }
        public string? Instruction { get; set; }
        public string? Quantity { get; set; }

        public string? ItemDesignatorID { get; set; }
        public string? strenght { get; set; }
        public string? DosageForm { get; set; }
        public string? PackageSize { get; set; }
        public bool? ControlSubstance { get; set; }
        public string? CourierMethod { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? TotalAmount { get; set; }

        public IList<OrderMedicineSupplyDTO> Supplies { get; set; } = new List<OrderMedicineSupplyDTO>();

        public bool? IsCustom { get; set; }
    }

}
