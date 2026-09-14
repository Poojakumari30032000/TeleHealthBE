using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.EmpowerPharmacy
{
    public class EmpowerOrderResultDTO
    {
        public long PrescriptionId { get; set; }
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? ResponseBody { get; set; }

        public string? ClientOrderId { get; set; }
        public string? DeliveryServiceUsed { get; set; }

        public List<EmpowerOrderLineDTO> Lines { get; set; } = new();

        public long? PatientOrderIdUpdated { get; set; }
        public string? OrderStatusAfterUpdate { get; set; }
    }

    public class EmpowerOrderLineDTO
    {
        public long PrescriptionMedicineId { get; set; }
        public string? MedicineName { get; set; }
        public string ItemDesignatorIdUsed { get; set; } = "";
        public int Quantity { get; set; }
        public int DaysSupply { get; set; }
        public int Refills { get; set; }
    }
    public class ShippingTypesEnvelope
    {
        public List<ShippingTypeItem>? ShippingTypeItems { get; set; }
    }
    public class ShippingTypeItem
    {
        public string? Name { get; set; }
    }

}
