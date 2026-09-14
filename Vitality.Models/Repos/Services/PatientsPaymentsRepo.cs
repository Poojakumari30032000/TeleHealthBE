using AutoMapper;
using DudeMeds.Models.DTOs.PatientPayments;
using DudeMeds.Models.DTOs.Questionnaires;
using DudeMeds.Models.Repos.Interfaces;
using System.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DudeMeds.Models.DTOs.PatientOrders;
using System.Data;
using Vitality.Models.Repos;
using Vitality.Models.EntityClasses;
using Vitality.Models.CommonMethods;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    public class PatientsPaymentsRepo : BaseRepo , IPatientPaymentsRepo
    {
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;

        public PatientsPaymentsRepo(IMapper mapper, IAuditService auditService)
        {
            _mapper = mapper;
            _auditService = auditService;
        }

        public List<GetAllPatientPaymentsResponseDTO> GetAllPatientPayments(GetAllPatientPaymentsRequestDTO request, out int totalPatientPaymentCount)
        {
            DynamicParameters param = new DynamicParameters();

            param.Add("@PageSize", request.PageSize);
            param.Add("@PageNumber", request.PageNumber);
            param.Add("@FacilityGuid", request.FacilityGuid);
            param.Add("@Title", request.Title);
            param.Add("@PatientId", request.PatientId);
            param.Add("@TotalPatientPaymentCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var data = ReturnJson<GetAllPatientPaymentsResponseDTO>("[dbo].[sprocGetAllPatientPayments]", param).ToList();
            totalPatientPaymentCount = param.Get<int>("@TotalPatientPaymentCount");

            return data;
        }

        public SavePatientPaymentResponseDTO SavePatientPayment(SavePatientPaymentRequestDTO request, long UserId)
        {

            try
            {
                PT_PatientPaymentDetail patientPayment = new PT_PatientPaymentDetail();
                Guid guid = Guid.NewGuid();
                if (request.PatientPaymentId == 0)
                {
                    patientPayment = _mapper.Map<PT_PatientPaymentDetail>(request);
                    patientPayment.Guid = guid.ToString();
                    patientPayment.CreatedBy = UserId;
                    patientPayment.CreatedDate = DateTime.UtcNow;
                    patientPayment.PaymentStatus = "Pending";
                    patientPayment.IsActive = true;
                    _db.PT_PatientPaymentDetails.Add(patientPayment);
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "PT_PatientPaymentDetail",
                        entityId: patientPayment.PatientPaymentId,
                        newValues: patientPayment,
                        userId: UserId,
                        patientId: request.PatientId,
                        description: $"Payment processed for Patient ID {request.PatientId}, Amount: ${patientPayment.TotalPrice}",
                        module: "Payment"
                    );

                   PT_PatientCardDetail patientCard = new PT_PatientCardDetail();
                    patientCard.PatientCardId = 0;
                    patientCard.PatientId = request.PatientId;
                    patientCard.PatientPaymentId = patientPayment.PatientPaymentId;
                    using (AesManaged aes = new AesManaged())
                    {

                        patientCard.CardNumber = Convert.ToBase64String(CommonMethods.Encrypt(request.CardNumber, aes.Key, aes.IV));
                        patientCard.CVC = Convert.ToBase64String(CommonMethods.Encrypt(request.CVC, aes.Key, aes.IV));
                    }
                    patientCard.ExpirationDate = request.ExpirationDate;
                    patientCard.IsActive = true;
                    patientCard.CreatedBy = UserId;
                    patientCard.CreatedDate = DateTime.UtcNow;
                    _db.PT_PatientCardDetails.Add(patientCard);
                    _db.SaveChanges();

                    SYS_Notification notification = new SYS_Notification();
                    notification.FacilityId = _db.PT_Patients.Where(x => x.PatientId == patientPayment.PatientId).Select(x => x.FacilityId).FirstOrDefault();
                    notification.NotificationType = "Order";
                    notification.IsRead = false;
                    notification.Description = "A New Payment Has Been Done.";
                    notification.CreatedDate = DateTime.UtcNow;
                    notification.PatientId = patientPayment.PatientId;
                    _db.SYS_Notifications.Add(notification);
                    _db.SaveChanges();
                }

                SavePatientPaymentResponseDTO response = new SavePatientPaymentResponseDTO();
                response = _mapper.Map<SavePatientPaymentResponseDTO>(patientPayment);
                return response;

            }
            catch (Exception ex)
            {
                return null;
            }
        }

    }
}
