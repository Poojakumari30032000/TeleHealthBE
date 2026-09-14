using DudeMeds.Models.DTOs.PatientPayments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IPatientPaymentsRepo
    {
        public List<GetAllPatientPaymentsResponseDTO> GetAllPatientPayments(GetAllPatientPaymentsRequestDTO request, out int totalPatientPaymentCount);
        public SavePatientPaymentResponseDTO SavePatientPayment(SavePatientPaymentRequestDTO request, long UserId);
    }
}
