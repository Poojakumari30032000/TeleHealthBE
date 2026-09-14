using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.EmpowerPharmacy
{
    public class EmpowerPrescriberResolved
    {
        public string? Npi { get; set; }
        public string? StateLicenseNumber { get; set; }
        public string? DeaNumber { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? PhoneNumber { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? StateProvince { get; set; }
        public string? PostalCode { get; set; }
        public string? CountryCode { get; set; }
    }
}
