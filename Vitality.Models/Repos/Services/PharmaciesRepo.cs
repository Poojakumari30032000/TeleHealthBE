using AutoMapper;
using DudeMeds.Models.DTOs.Pharmacies;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    public class PharmaciesRepo : BaseRepo , IPharmaciesRepo
    {
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;

        public PharmaciesRepo(IMapper mapper, IAuditService auditService)
        {
            _mapper = mapper;
            _auditService = auditService;
        }
        public List<GetAllPharmaciesResponseDTO> GetAllPharmacies(GetAllPharmaciesRequestDTO request, out int totalPharmacyCount)
        {
            List<GetAllPharmaciesResponseDTO> response = new List<GetAllPharmaciesResponseDTO>();
            List<SYS_Pharmacy> list = _db.SYS_Pharmacies.Where(x => x.IsActive == true).ToList();
            if (request.PharmacyName != null)
            {
                list = list.Where(x => x.PharmacyName.Contains(request.PharmacyName)).ToList();
            }
            if (request.Status != null)
            {
                list = list.Where(x => x.Status == request.Status).ToList();
            }
            totalPharmacyCount = list.Count;
            list = list.OrderBy(x => x.PharmacyName).Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
            response = _mapper.Map<List<GetAllPharmaciesResponseDTO>>(list);
            return response;
        }

        public GetPharmacyByIdResponseDTO GetPharmacyById(long PharmacyId)
        {
            GetPharmacyByIdResponseDTO response = new GetPharmacyByIdResponseDTO();
            SYS_Pharmacy Pharmacy = _db.SYS_Pharmacies.Where(x => x.PharmacyId == PharmacyId).FirstOrDefault();
            response = _mapper.Map<GetPharmacyByIdResponseDTO>(Pharmacy);
            response.PharmacyStorage = _db.PH_PharmaciesStorageTypes.Where(x => x.PharmacyId == PharmacyId).Select(x => x.StorageType)
            .ToList();
            return response;

        }
        public string SavePharmacy(SavePharmacyRequestDTO request, long UserId, long OrganizationId)
        {

            try
            {
                SYS_Pharmacy Pharmacy = new SYS_Pharmacy();
                Guid guid = Guid.NewGuid();
                if (request.PharmacyId == 0)
                {
                    Pharmacy = _mapper.Map<SYS_Pharmacy>(request);
                    Pharmacy.Guid = guid.ToString();
                    Pharmacy.CreatedBy = UserId;
                    Pharmacy.CreatedDate = DateTime.UtcNow;
                    Pharmacy.OrganizationId = OrganizationId;
                    Pharmacy.IsActive = true;
                    Pharmacy.Status = "Active";
                    _db.SYS_Pharmacies.Add(Pharmacy);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "SYS_Pharmacy",
                        entityId: Pharmacy.PharmacyId,
                        newValues: Pharmacy,
                        userId: UserId,
                        description: $"Pharmacy '{Pharmacy.PharmacyName}' created - Address: {Pharmacy.Address}, Status: {Pharmacy.Status}",
                        module: "Pharmacy"
                    );

                    if (request.PharmacyStorage.Count != 0)
                    {
                        foreach (var pharmacy in request.PharmacyStorage)
                        {
                            PH_PharmaciesStorageType newItem = new PH_PharmaciesStorageType
                            {
                                PharmacyStorageId = 0,
                                PharmacyId = Pharmacy.PharmacyId,
                                StorageType = pharmacy,
                            };
                            _db.PH_PharmaciesStorageTypes.Add(newItem);
                        }
                        _db.SaveChanges();
                    }

                    return "Pharmacy Created Successfully";

                }
                else
                {
                    Pharmacy = _db.SYS_Pharmacies.Where(x => x.PharmacyId == request.PharmacyId).FirstOrDefault();
                    _mapper.Map(request, Pharmacy);
                    _db.SaveChanges();
                    return "Pharmacy Updated Successfully";

                }
            }
            catch (Exception ex)
            {
                return "Something went wrong. Please try again later.";
            }
        }

        public bool DeletePharmacy(long PharmacyId)
        {
            SYS_Pharmacy Pharmacy = _db.SYS_Pharmacies.Where(x => x.PharmacyId == PharmacyId).FirstOrDefault();
            if (Pharmacy != null)
            {
                var oldPharmacy = _db.SYS_Pharmacies.AsNoTracking()
                    .FirstOrDefault(x => x.PharmacyId == PharmacyId);

                Pharmacy.IsActive = false;
                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Delete",
                    entityType: "SYS_Pharmacy",
                    entityId: PharmacyId,
                    oldValues: oldPharmacy,
                    description: $"Pharmacy '{Pharmacy.PharmacyName}' deleted",
                    module: "Pharmacy"
                );

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool UpdatePharmacyStatus(UpdatePharmacyStatusRequestDTO request)
        {
            SYS_Pharmacy Pharmacy = _db.SYS_Pharmacies.Where(x => x.PharmacyId == request.PharmacyId).FirstOrDefault();
            if (Pharmacy != null)
            {
                var oldStatus = Pharmacy.Status;
                Pharmacy.Status = request.Status;
                _db.SaveChanges();

                _auditService.LogEntityChange(
                    action: "Update",
                    entityType: "SYS_Pharmacy",
                    entityId: request.PharmacyId,
                    oldValues: new { Status = oldStatus },
                    newValues: new { Status = request.Status },
                    description: $"Pharmacy '{Pharmacy.PharmacyName}' status changed from '{oldStatus}' to '{request.Status}'",
                    module: "Pharmacy"
                );

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
