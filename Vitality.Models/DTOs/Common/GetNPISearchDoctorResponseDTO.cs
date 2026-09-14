using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Common
{
    public class GetNPISearchDoctorResponseDTO
    {
        public int result_count { get; set; }
        public List<FinalResult> Results { get; set; }
    }
    public class Address
    {
        public string? country_code { get; set; }
        public string? country_name { get; set; }
        public string? address_purpose { get; set; }
        public string? address_type { get; set; }
        public string? address_1 { get; set; }
        public string? city { get; set; }
        public string? state { get; set; }
        public string? postal_code { get; set; }
        public string? telephone_number { get; set; }
        public string? fax_number { get; set; }
    }

    public class Basic
    {
        public string? first_name { get; set; }
        public string? last_name { get; set; }
        public string? authorized_official_first_name { get; set; }
        public string? authorized_official_last_name { get; set; }
        public string? middle_name { get; set; }
        public string? credential { get; set; }
        public string? sole_proprietor { get; set; }
        public string? gender { get; set; }
        public string? enumeration_date { get; set; }
        public string? last_updated { get; set; }
        public string? status { get; set; }
        public string? name_prefix { get; set; }
        public string? name_suffix { get; set; }
    }

    public class Taxonomy
    {
        public string? code { get; set; }
        public string? taxonomy_group { get; set; }
        public string? desc { get; set; }
        public string? state { get; set; }
        public string? license { get; set; }
        public bool primary { get; set; }
    }

    public class identifiers
    {
        public string? code { get; set; }
        public string? desc { get; set; }
        public string? issuer { get; set; }
        public string? identifier { get; set; }
        public string? state { get; set; }
    }

    public class FinalResult
    {
        public string? created_epoch { get; set; }
        public string? enumeration_type { get; set; }
        public string? last_updated_epoch { get; set; }
        public string? number { get; set; }
        public List<Address> Addresses { get; set; }
        public List<object> PracticeLocations { get; set; }
        public Basic Basic { get; set; }
        public List<Taxonomy> Taxonomies { get; set; }
        public List<identifiers> Identifiers { get; set; }
        public List<object> Endpoints { get; set; }
        public List<object> OtherNames { get; set; }
    }
}
