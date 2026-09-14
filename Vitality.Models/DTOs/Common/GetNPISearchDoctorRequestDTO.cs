using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Common
{
    public class GetNPISearchDoctorRequestDTO
    {
        public string? NpiNumber { get; set; }
        public string? EnumerationType { get; set; }
        public string? NpiType { get; set; }
        public string? TaxonomyDescription { get; set; }
        public string? name_purpose { get; set; }
        public string? ProviderFirstName { get; set; }
        public string? use_first_name_alias { get; set; }
        public string? ProviderLastName { get; set; }
        public string? OrganizationName { get; set; }
        public string? AuthorizedOfficialFirstName { get; set; }
        public string? AuthorizedOfficialLastName { get; set; }
        public string? address_purpose { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? AddressType { get; set; }
        public string? limit { get; set; }
        public string? skip { get; set; }
    }
}
