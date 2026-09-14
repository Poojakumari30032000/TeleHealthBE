using DudeMeds.Models.DTOs.PatientAppointments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.PatientAppointments;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IPatientAppointmentsRepo
    {
        public List<GetAllPatientAppointmentsByMonthResponseDTO> GetAllPatientAppointmentsByMonth(GetAllPatientAppointmentsByMonthRequestDTO request);
        public List<GetAllPatientAppointmentsByDaysResponseDTO> GetAllPatientAppointmentsByDays(GetAllPatientAppointmentsByDaysRequestDTO request);
        public List<GetAllPatientAppointmentsResponseDTO> GetAllPatientAppointments(GetAllPatientAppointmentsRequestDTO request, out int totalPatientAppointmentCount);
        public GetPatientAppointmentByIdResponseDTO GetPatientAppointmentById(long PatientAppointmentSlotId);
        Task<long> SavePatientAppointmentAsync(
            SavePatientAppointmentRequestDTO request,
            long userId,
            long? providerScheduledSlotId,
            long? patientId,
            long? productId,
            long? patientTreatmentId,
            CancellationToken ct = default);
        Task<bool> DeletePatientAppointmentAsync(
            long patientAppointmentSlotId,
            long userId,
            CancellationToken ct = default);
        public GetPatientAppointmentInfoResponseDTO GetPatientAppointmentInfo(long PatientAppointmentSlotId, int? clientTimezoneOffsetMinutes = null);
        public List<GetAllPatientAppointmentPrescriptionsResponseDTO> GetAllPatientAppointmentPrescriptions(long PatientAppointmentSlotId);
        public List<GetAllPatientAppointmentInTakeFormResponseDTO> GetAllPatientAppointmentInTakeForm(long PatientAppointmentSlotId);
        public List<GetAllPatientAppointmentInTakeFormAttahmentsResponseDTO> GetAllPatientAppointmentInTakeFormAttahments(long PatientAppointmentSlotId);
        public GetPatientAppointmentVideoCallIdRespnseDTO GetPatientAppointmentVideoCallId(GetPatientAppointmentVideoCallIdRequestDTO request);
        public GetPatientAppointmentAlertResponseDTO GetPatientAppointmentAlert(long ProviderId);
        long SavePatientAppointment(SavePatientAppointmentRequestDTO request);
        Task<bool> SaveFollowUpAppointmentAsync(SaveFollowUpAppointmentRequestDTO request);
        Task<bool> UpdateAppointmentForFollowUpAsync(UpdateAppointmentForFollowUpRequestDTO request);
        Task<GetPatientAppointmentZoomUrlResponseDTO> GetPatientAppointmentZoomUrlAsync(long PatientAppointmentSlotId, CancellationToken ct = default);

    }
}
