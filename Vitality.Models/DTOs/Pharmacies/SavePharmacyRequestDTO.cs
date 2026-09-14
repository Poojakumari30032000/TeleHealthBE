using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Pharmacies
{
    public class SavePharmacyRequestDTO
    {
        public long PharmacyId { get; set; }
        public long? OrganizationId { get; set; }
        public string? PharmacyName { get; set; }
        public string? PharmacyType { get; set; }
        public string? LegalBusinesName { get; set; }
        public string? DBAName { get; set; }
        public string? Address { get; set; }
        public string? NABPId { get; set; }
        public string? DEANumber { get; set; }
        public string? StateCSLicense { get; set; }
        public DateTime? LicenseExpiration { get; set; }
        public string? Accereditation { get; set; }
        public string? InChargePharmacist { get; set; }
        public string? PharmacistLicense { get; set; }
        public List<string>? PharmacyStorage { get; set; }
        public bool? Is24Operation { get; set; }
        public bool? IsCompoundingServices { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyPhone { get; set; }
        public string? FederalTaxId { get; set; }
        public string? MedicaidNumber { get; set; }
        public string? MedicarePTAN { get; set; }
    }
}
