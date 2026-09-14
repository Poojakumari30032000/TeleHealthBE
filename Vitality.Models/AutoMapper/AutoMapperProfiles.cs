using AutoMapper;
using DudeMeds.Models.DTOs.Categories;
using DudeMeds.Models.DTOs.Conditions;
using DudeMeds.Models.DTOs.DropDown;
using DudeMeds.Models.DTOs.Facilities;
using DudeMeds.Models.DTOs.PatientAppointments;
using DudeMeds.Models.DTOs.PatientOrders;
using DudeMeds.Models.DTOs.PatientPayments;
using DudeMeds.Models.DTOs.PatientPrescriptions;
using DudeMeds.Models.DTOs.Patients;
using DudeMeds.Models.DTOs.PatientTreatments;
using DudeMeds.Models.DTOs.Pharmacies;
using DudeMeds.Models.DTOs.Products;
using DudeMeds.Models.DTOs.ProviderSchedules;
using DudeMeds.Models.DTOs.Questionnaires;
using DudeMeds.Models.DTOs.Tickets;
using DudeMeds.Models.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Brands;
using Vitality.Models.DTOs.FacilitySquareCred;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.DTOs.PatientAppointments;
using Vitality.Models.DTOs.PatientProduct;
using Vitality.Models.DTOs.Subscriptions;
using Vitality.Models.EntityClasses;

namespace Vitality.Models.AutoMapper
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            #region DropDowns
            CreateMap<SYS_Country, GetAllCountryResponseDTO>().ReverseMap();
            CreateMap<SYS_State, GetAllStateResponseDTO>().ReverseMap();
            CreateMap<SYS_City, GetAllCityResponseDTO>().ReverseMap();
            #endregion

            #region Users
            CreateMap<SYS_UserDetail, SaveUserRequestDTO>().ReverseMap();
            CreateMap<SYS_UserDetail, GetUserByIdResponseDTO>().ReverseMap();
            CreateMap<SYS_UserDetail, GetAllUsersResponseDTO>().ReverseMap();
            CreateMap<SYS_UserDetail, GetAllUsersRequestDTO>().ReverseMap();
            #endregion

            #region Patients
            CreateMap<PT_Patient, SavePatientRequestDTO>().ReverseMap();
            CreateMap<PT_Patient, GetPatientByIdResponseDTO>().ReverseMap();
            CreateMap<PT_Patient, GetAllPatientsResponseDTO>().ReverseMap();
            CreateMap<PT_Patient, GetAllPatientsRequestDTO>().ReverseMap();
            #endregion

            #region Facilities
            CreateMap<SYS_Facility, SaveFacilityRequestDTO>().ReverseMap();
            CreateMap<SYS_Facility, GetFacilityByIdResponseDTO>().ReverseMap();
            CreateMap<SYS_Facility, GetAllFacilitiesResponseDTO>().ReverseMap();
            CreateMap<SYS_Facility, GetAllFacilitiesRequestDTO>().ReverseMap();
            #endregion

            #region ProviderSchedules
            CreateMap<UR_ProviderScheduledSlot, SaveProviderSlotRequestDTO>().ReverseMap();
            #endregion

            #region PatientAppointments
            CreateMap<PT_PatientAppointmentSlot, SavePatientAppointmentRequestDTO>().ReverseMap();
            CreateMap<PT_PatientAppointmentSlot, GetPatientAppointmentByIdResponseDTO>().ReverseMap();
            CreateMap<PT_PatientAppointmentSlot, GetPatientAppointmentAlertResponseDTO>().ReverseMap();
            #endregion

            #region Questionnaires
            CreateMap<SYS_Questionnaire, SaveQuestionnaireRequestDTO>().ReverseMap();
            CreateMap<SYS_Questionnaire, GetQuestionnaireByIdResponseDTO>().ReverseMap();
            CreateMap<SYS_Questionnaire, GetAllQuestionnairesResponseDTO>().ReverseMap();
            #endregion

            #region PatientTreatments
            CreateMap<PT_PatientTreatment, SavePatientTreatmentRequestDTO>().ReverseMap();
            #endregion

            #region Products
            CreateMap<PD_Drug, GetAllProductsResponseDTO>().ReverseMap();
            CreateMap<PD_Bundle, GetAllProductsResponseDTO>().ReverseMap();
            CreateMap<PD_LabTest, GetAllProductsResponseDTO>().ReverseMap();
            CreateMap<PD_DigitalProduct, GetAllProductsResponseDTO>().ReverseMap();
            CreateMap<DG_DrugIngredient, SaveDrugIngredientRequestDTO>().ReverseMap();
            CreateMap<DG_DrugIngredient, GetDrugIngredientByIdResponseDTO>().ReverseMap();
            CreateMap<DG_DrugIngredient, GetAllDrugIngredientsResponseDTO>().ReverseMap();

            CreateMap<SaveDrugRequestDTO, PD_Drug>()
                .ForMember(d => d.Price, o => o.Ignore())
                .ForMember(d => d.ComparePrice, o => o.Ignore())
                .ForMember(d => d.SuggestedRetail, o => o.Ignore())
                .ForMember(d => d.Markup, o => o.Ignore())
                .ForAllMembers(o => o.Condition((src, dest, val) => val != null));

            CreateMap<PD_Drug, GetDrugByIdResponseDTO>().ReverseMap();
            CreateMap<PD_Bundle, SaveBundleRequestDTO>().ReverseMap();
            CreateMap<PD_Bundle, GetBundleByIdResponseDTO>().ReverseMap();
            CreateMap<PD_DrugVarientsInBundle, SaveDrugVarientsInBundleRequestDTO>().ReverseMap();

            #endregion

            #region Categories
            CreateMap<PD_Category, SaveCategoryRequestDTO>().ReverseMap();
            CreateMap<PD_Category, GetCategoryByIdResponseDTO>().ReverseMap();
            CreateMap<PD_Category, GetAllCategoriesResponseDTO>().ReverseMap();
            #endregion

            #region Conditions
            CreateMap<PD_Condition, SaveConditionRequestDTO>().ReverseMap();
            CreateMap<PD_Condition, GetConditionByIdResponseDTO>().ReverseMap();
            CreateMap<PD_Condition, GetAllConditionsResponseDTO>().ReverseMap();
            #endregion

            #region PatientOrders
            CreateMap<PT_PatientOrder, SavePatientOrderRequestDTO>().ReverseMap();
            CreateMap<PT_PatientOrder, GetPatientOrderByIdResponseDTO>().ReverseMap();
            CreateMap<PT_PatientOrder, GetAllPatientOrdersResponseDTO>().ReverseMap();
            CreateMap<PT_PatientOrder, SavePatientOrderResponseDTO>().ReverseMap();
            #endregion

            #region PatientPayments
            CreateMap<PT_PatientPaymentDetail, SavePatientPaymentRequestDTO>().ReverseMap();
            CreateMap<PT_PatientPaymentDetail, SavePatientPaymentResponseDTO>().ReverseMap();
            #endregion

            #region Pharmacies
            CreateMap<SYS_Pharmacy, GetAllPharmaciesResponseDTO>().ReverseMap();
            CreateMap<SYS_Pharmacy, GetAllPharmaciesRequestDTO>().ReverseMap();
            CreateMap<SYS_Pharmacy, GetPharmacyByIdResponseDTO>().ReverseMap();
            CreateMap<SYS_Pharmacy, SavePharmacyRequestDTO>().ReverseMap();
            #endregion

            #region PatientPrescriptions
            CreateMap<PT_PatientPrescription, GetPatientPrescriptionByIdResponseDTO>().ReverseMap();
            #endregion

            #region Tickets
            CreateMap<SYS_Ticket, SaveTicketRequestDTO>().ReverseMap();
            CreateMap<SYS_Ticket, GetTicketByIdResponseDTO>().ReverseMap();
            CreateMap<SYS_Ticket, GetAllTicketsResponseDTO>().ReverseMap();
            #endregion

            #region Brands
            CreateMap<SYS_Brand, GetBrandByIdResponseDTO>().ReverseMap();
            CreateMap<SYS_Brand, SaveBrandRequestDTO>().ReverseMap();
            #endregion

            #region Subscriptions
            CreateMap<SYS_Subscription, GetSubscriptionByIdResponseDTO>().ReverseMap();
            CreateMap<SYS_Subscription, SaveSubscriptionRequestDTO>().ReverseMap();
            CreateMap<SYS_Subscription, GetAllSubscriptionsResponseDTO>().ReverseMap();
            #endregion

             #region Invoices
            CreateMap<Sys_Invoice, GetInvoiceByIdResponseDTO>().ReverseMap();
            CreateMap<Sys_Invoice, SaveInvoiceRequestDTO>().ReverseMap();
            CreateMap<Sys_Invoice, GetAllInvoicesResponseDTO>().ReverseMap();
            #endregion

            #region PatientProduct
            CreateMap<Pt_PatientProduct, SavePatientProductDTO>().ReverseMap();
            CreateMap<SavePatientProductDTO, Pt_PatientProduct>().ReverseMap();

            #endregion
            #region SquareCredentials
            CreateMap<Sys_FacilitySquareCred, SaveSquareCredRequestDto>().ReverseMap();
            CreateMap<SaveSquareCredRequestDto, Sys_FacilitySquareCred>().ReverseMap();

            #endregion
        }
    }
}
