using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Vitality.Helper;
using Vitality.Models.DTOs.EmpowerPharmacy;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class EmpowerWebhookController : ControllerBase
    {
        private readonly IEmpowerWebhookService _webhookService;

        public EmpowerWebhookController(IEmpowerWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        [HttpPost("order-status")]
        public async Task<IActionResult> ProcessOrderStatusWebhook([FromBody] EmpowerWebhookRequestDTO webhook)
        {
            try
            {

                if (webhook == null)
                {

                    return BadRequest(new ApiResponse<EmpowerWebhookResponseDTO>
                    {
                        Message = "Webhook payload is required"
                    });
                }

                var result = await _webhookService.ProcessWebhookAsync(webhook);

                if (result.Success)
                {

                    return Ok(new ApiResponse<EmpowerWebhookResponseDTO>
                    {
                        Data = result,
                        Message = result.Message
                    });
                }
                else
                {

                    return BadRequest(new ApiResponse<EmpowerWebhookResponseDTO>
                    {
                        Data = result,
                        Message = result.Message
                    });
                }
            }
            catch (System.Exception ex)
            {

                return StatusCode(500, new ApiResponse<EmpowerWebhookResponseDTO>
                {
                    Message = $"Error processing webhook: {ex.Message}"
                });
            }
        }

        [HttpGet("order/{clientOrderId}")]
        public async Task<IActionResult> GetOrderByClientOrderId(string clientOrderId)
        {
            try
            {
                var order = await _webhookService.GetOrderByClientOrderIdAsync(clientOrderId);

                if (order == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Message = $"Order with ClientOrderId '{clientOrderId}' not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Data = new
                    {
                        order.EmpowerOrdersId,
                        order.ClientOrderId,
                        order.EipOrderId,
                        order.LfOrderId,
                        order.OrderStatus,
                        order.ShipmentStatus,
                        order.ShipmentTrackingNumber,
                        order.ShipmentTrackingUrl,
                        order.ShipmentProvider,
                        order.OrderStatusLastUpdatedTime,
                        order.ShipmentStatusLastUpdatedTime,
                        order.Error
                    }
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Message = $"Error retrieving order: {ex.Message}"
                });
            }
        }
    }
}
