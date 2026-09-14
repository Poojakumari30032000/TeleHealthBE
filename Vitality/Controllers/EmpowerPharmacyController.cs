using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;
using Vitality.Models.Repos.Services;
using Vitality.Models.EntityClasses;
using Vitality.Filters;
using Vitality.Models.Security;

[ApiController]
[Route("api/empower")]
public class EmpowerPharmacyController : ControllerBase
{
    private readonly IEmpowerPharmacyService _service;
    private readonly INotificationService _notificationService;

    public EmpowerPharmacyController(IEmpowerPharmacyService service, INotificationService notificationService)
    {
        _service = service;
        _notificationService = notificationService;
    }

    [HttpPost("create-orders-by-prescription/{patientPrescriptionId:long}")]
    [RequiresPermission(Permissions.Order.Edit, Permissions.Prescription.Edit)]
    public async Task<IActionResult> CreateOrdersByPrescription(long patientPrescriptionId)
    {
        var results = await _service.CreateOrderForPrescriptionAsync(patientPrescriptionId);

        if (results.Success && results.PatientOrderIdUpdated.HasValue)
        {

            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MainContext>();
            var prescription = await db.PT_PatientPrescriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientPrescriptionId == patientPrescriptionId);

            if (prescription != null && prescription.PatientId.HasValue && prescription.PatientId.Value > 0)
            {
                await _notificationService.SendOrderToPharmacyAsync(
                    patientId: prescription.PatientId.Value,
                    orderId: results.PatientOrderIdUpdated.Value,
                    ct: HttpContext.RequestAborted
                );
            }
        }

        return Ok(new
        {
            prescriptionId = patientPrescriptionId,
            count = 1,
            results
        });
    }

    [HttpGet("getShippingTypes")]
    public async Task<IActionResult> GetShippingTypes()
    {
        try
        {
            var names = await _service.GetShippingTypeNamesAsync();

            var payload = new
            {
                shippingTypeItems = names.Select(n => new { name = n }).ToList()
            };

            return Ok(payload);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new
            {
                message = "Failed to fetch shipping types from Empower.",
                detail = ex.Message
            });
        }
    }
}
