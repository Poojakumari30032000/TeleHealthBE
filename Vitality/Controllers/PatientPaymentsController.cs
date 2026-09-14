using AutoMapper;
using DudeMeds.Models.DTOs.Common;
using DudeMeds.Models.DTOs.PatientAppointments;
using DudeMeds.Models.DTOs.PatientOrders;
using DudeMeds.Models.DTOs.PatientPayments;
using DudeMeds.Models.DTOs.Questionnaires;
using DudeMeds.Models.Repos.Interfaces;
using DudeMeds.Models.Repos.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Vitality.Helper;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;

namespace DudeMeds.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientPaymentsController : ControllerBase
    {
        public IConfiguration _configuration;
        private readonly IMapper _mapper;

        private readonly IPatientPaymentsRepo _IPatientPaymentsRepo;
        private readonly IPatientAppointmentsRepo _IPatientAppointmentsRepo;
        private readonly IInvoiceRepo _IInvoiceRepo;
        private readonly INotificationService _notificationService;
        private readonly FacilityStatusService _facilityStatusService;
        private readonly MainContext _db;

        public PatientPaymentsController(
            IConfiguration config,
            IMapper IMapper,
            IPatientPaymentsRepo IPatientPaymentsRepo,
            IPatientAppointmentsRepo IPatientAppointmentsRepo,
            IInvoiceRepo IInvoiceRepo,
            INotificationService notificationService,
            MainContext db)
        {
            _configuration = config;
            _mapper = IMapper;
            _IPatientPaymentsRepo = IPatientPaymentsRepo;
            _IPatientAppointmentsRepo = IPatientAppointmentsRepo;
            _IInvoiceRepo = IInvoiceRepo;
            _notificationService = notificationService;
            _db = db;
            _facilityStatusService = new FacilityStatusService(_db);

        }

        [HttpGet]
        [Route("getAllPatientPayments")]
        public Vitality.Helper.ApiResponse<List<GetAllPatientPaymentsResponseDTO>> GetAllPatientPayments([FromQuery] GetAllPatientPaymentsRequestDTO request)
        {
            ApiResponse<List<GetAllPatientPaymentsResponseDTO>> response = new ApiResponse<List<GetAllPatientPaymentsResponseDTO>>();
            try
            {
                List<GetAllPatientPaymentsResponseDTO> result = new List<GetAllPatientPaymentsResponseDTO>();
                result = _IPatientPaymentsRepo.GetAllPatientPayments(request, out int totalPatientPaymentCount);
                int totalPages = (int)Math.Ceiling((double)totalPatientPaymentCount / request.PageSize);
                response.Data = result;
                response.TotalEntityCount = totalPatientPaymentCount;
                response.TotalPages = totalPages;
                response.Data = result;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return response;
        }

    }
}
