using Microsoft.EntityFrameworkCore;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.Repos.Services
{

    public class FacilityStatusService
    {
        private readonly MainContext _db;

        public FacilityStatusService(MainContext db)
        {
            _db = db;
        }

        public bool IsFacilityActive(long? facilityId)
        {
            if (!facilityId.HasValue || facilityId.Value <= 0)
            {
                return false;
            }

            var facility = _db.SYS_Facilities
                .AsNoTracking()
                .FirstOrDefault(f => f.FacilityId == facilityId.Value);

            if (facility == null)
            {
                return false;
            }

            return facility.IsActive == true &&
                   !string.IsNullOrWhiteSpace(facility.Status) &&
                   facility.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
        }

        public long? GetUserFacilityId(long userId)
        {
            var userFacility = _db.FC_UsersInFacilities
                .AsNoTracking()
                .FirstOrDefault(uf => uf.UserId == userId);

            return userFacility?.FacilityId;
        }

        public long? GetPatientFacilityId(long? patientId)
        {
            if (!patientId.HasValue || patientId.Value <= 0)
            {
                return null;
            }

            var patient = _db.PT_Patients
                .AsNoTracking()
                .FirstOrDefault(p => p.PatientId == patientId.Value);

            return patient?.FacilityId;
        }

        public bool IsUserFacilityActive(long userId)
        {
            var facilityId = GetUserFacilityId(userId);
            if (!facilityId.HasValue)
            {

                return true;
            }

            return IsFacilityActive(facilityId.Value);
        }

        public bool IsPatientFacilityActive(long? patientId)
        {
            var facilityId = GetPatientFacilityId(patientId);
            if (!facilityId.HasValue)
            {

                return false;
            }

            return IsFacilityActive(facilityId.Value);
        }
    }
}
