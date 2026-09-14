using Square.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.FacilitySquareCred;
using Vitality.Models.DTOs.PatientProduct;
using Vitality.Models.DTOs.Square;

namespace Vitality.Models.Repos.Interfaces
{
    public interface ISquarePaymentRepo
    {
        Task<CreatePaymentResponse> CreatePaymentAsync(PaymentRequest request);

        Task<CreatePaymentResponse> ChargeCustomerWithSavedCard(string squareCustomerId, string cardId, int amount, string currency = "USD");

        Task<CreatePaymentResponse> ChargeCustomerWithSavedCard(string squareCustomerId, string cardId, int amount, string currency, long? facilityId);

        Task<CreatePaymentResponse> ChargeCustomerWithSavedCard(string squareCustomerId, string cardId, int amount, string currency, long? facilityId, string? idempotencyKey);

        Task<bool> SavePaymentCards(SaveCardRequest request);

        List<SquareCardDTO> GetCardsByUserId(long userId);
        bool SetDefaultCard(long userId, long cardId);
        bool DeleteCard(long cardId);
        bool SavePatientProduct(SavePatientProductDTO request, long UserId);

        bool SaveFacilitySquareCredentials(SaveSquareCredRequestDto request);
        SaveSquareCredRequestDto GetFacilitySquareCredentialsByFacilityId(long FacilityId);
        SaveSquareCredRequestDto GetAllFacilitySquareCredentialsByIdWithoutToken(long FacilityId);

        public List<SquareCardDTO> GetCardsByPatientId(long patientId);
        Task<SquareAppIdResponseDto?> GetSquareAppIdByFacilityIdAsync(long facilityId);
    }
}
