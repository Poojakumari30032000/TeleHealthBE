using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Vitality.Helper;
using Vitality.Models.DTOs.PrescriptionMedicines;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Filters;
using Vitality.Models.Security;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PrescriptionMedicinesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IPrescriptionMedicinesRepo _prescriptionMedicinesRepo;
        private readonly INotificationService _notificationService;
        private readonly MainContext _db;

        public PrescriptionMedicinesController(
            IConfiguration config,
            IMapper mapper,
            IPrescriptionMedicinesRepo prescriptionMedicinesRepo,
            INotificationService notificationService,
            MainContext db)
        {
            _configuration = config;
            _mapper = mapper;
            _prescriptionMedicinesRepo = prescriptionMedicinesRepo;
            _notificationService = notificationService;
            _db = db;
        }

        [HttpPost]
        [Route("create")]
        [RequiresPermission(Permissions.Prescription.Edit)]
        public ApiResponse<bool> Create([FromBody] CreatePrescriptionMedicineRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var ok = _prescriptionMedicinesRepo.Create(request.PatientPrescriptionId, request.MedicineName);
                response.Data = ok;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPost]
        [Route("createMany")]
        [RequiresPermission(Permissions.Prescription.Edit)]
        public async Task<ApiResponse<bool>> CreateMany([FromBody] BulkCreatePrescriptionMedicineRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var hadExistingMedicines = request.PatientPrescriptionId > 0 &&
                    await _db.PT_PrescriptionMedicines
                        .AsNoTracking()
                        .AnyAsync(m => m.PatientPrescriptionId == request.PatientPrescriptionId, HttpContext.RequestAborted);

                var ok = _prescriptionMedicinesRepo.CreateMany(
                    request.PatientPrescriptionId,
                    request.Medicines ?? new List<PrescriptionMedicinesRequestDTO>()
                );

                response.Data = ok;
                if (!ok) response.Message = "No valid medicines to insert.";

                if (ok && request.PatientPrescriptionId > 0)
                {
                    try
                    {
                        if (hadExistingMedicines)
                        {
                            await _notificationService.SendPrescriptionEditedAsync(
                                prescriptionId: request.PatientPrescriptionId,
                                ct: HttpContext.RequestAborted
                            );
                        }
                        else
                        {
                            await _notificationService.SendPrescriptionCreatedAsync(
                                prescriptionId: request.PatientPrescriptionId,
                                ct: HttpContext.RequestAborted
                            );
                        }
                    }
                    catch (Exception emailEx)
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }

        [HttpGet]
        [Route("getMedicinesByPrescriptionId")]
        [RequiresPermission(Permissions.Prescription.View)]
        public ApiResponse<List<PrescriptionMedicineItemDTO>> GetMedicinesByPrescriptionId([FromQuery] long patientPrescriptionId)
        {
            var response = new ApiResponse<List<PrescriptionMedicineItemDTO>>();
            try
            {
                var data = _prescriptionMedicinesRepo.GetMedicinesByPrescriptionId(patientPrescriptionId);
                response.Data = data.OrderBy(d => d.PrescriptionMedicineId).ToList();
                response.TotalEntityCount = response.Data.Count;
                response.TotalPages = 1;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpGet]
        [Route("getById")]
        [RequiresPermission(Permissions.Prescription.View)]
        public ApiResponse<PrescriptionMedicineItemDTO?> GetById([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<PrescriptionMedicineItemDTO?>();
            try
            {
                response.Data = _prescriptionMedicinesRepo.GetById(request.Id);
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

        [HttpPut]
        [Route("updateName")]
        [RequiresPermission(Permissions.Prescription.Edit)]
        public ApiResponse<bool> UpdateName([FromBody] UpdatePrescriptionMedicineNameRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var ok = _prescriptionMedicinesRepo.UpdateName(request.PrescriptionMedicineId, request.MedicineName);
                response.Data = ok;
                if (!ok) response.Message = "Update failed.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }

        [HttpPut]
        [Route("update")]
        [RequiresPermission(Permissions.Prescription.Edit)]
        public ApiResponse<bool> Update([FromBody] PrescriptionMedicinesRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var ok = _prescriptionMedicinesRepo.UpdateMedicine(request);
                response.Data = ok;
                if (!ok) response.Message = "Update failed.";
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
                response.Data = false;
            }
            return response;
        }

        [HttpDelete]
        [Route("delete")]
        [RequiresPermission(Permissions.Prescription.Edit)]
        public ApiResponse<bool> Delete([FromQuery] GetByIdRequestDTO request)
        {
            var response = new ApiResponse<bool>();
            try
            {
                var ok = _prescriptionMedicinesRepo.Delete(request.Id);
                response.Data = ok;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }
    }

}
