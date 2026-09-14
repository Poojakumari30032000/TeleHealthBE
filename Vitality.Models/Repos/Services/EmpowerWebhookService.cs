using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.DTOs.EmpowerPharmacy;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{
    public class EmpowerWebhookService : BaseRepo, IEmpowerWebhookService
    {
        private readonly ILogger<EmpowerWebhookService>? _logger;

        public EmpowerWebhookService(ILogger<EmpowerWebhookService>? logger = null)
        {
            _logger = logger;
        }

        public async Task<EmpowerWebhookResponseDTO> ProcessWebhookAsync(EmpowerWebhookRequestDTO webhook)
        {
            try
            {
                if (webhook == null)
                {
                    return new EmpowerWebhookResponseDTO
                    {
                        Success = false,
                        Message = "Webhook payload is null"
                    };
                }

                Sys_EmpowerOrder? order = null;

                if (!string.IsNullOrWhiteSpace(webhook.ClientOrderId))
                {
                    order = await _db.Sys_EmpowerOrders
                        .FirstOrDefaultAsync(o => o.ClientOrderId == webhook.ClientOrderId);
                }

                if (order == null && webhook.EipOrderId.HasValue)
                {
                    order = await _db.Sys_EmpowerOrders
                        .FirstOrDefaultAsync(o => o.EipOrderId == webhook.EipOrderId.Value);
                }

                if (order == null && !string.IsNullOrWhiteSpace(webhook.LfOrderId))
                {
                    order = await _db.Sys_EmpowerOrders
                        .FirstOrDefaultAsync(o => o.LfOrderId == webhook.LfOrderId);
                }

                if (order == null)
                {
                    order = new Sys_EmpowerOrder
                    {
                        CreatedDate = DateTime.UtcNow,
                        IsActive = true
                    };

                    if (!string.IsNullOrWhiteSpace(webhook.ClientOrderId))
                    {
                        order.ClientOrderId = webhook.ClientOrderId;
                    }

                    _db.Sys_EmpowerOrders.Add(order);
                }

                order.EipOrderId = webhook.EipOrderId ?? order.EipOrderId;
                order.LfOrderId = webhook.LfOrderId ?? order.LfOrderId;
                order.LfPatientId = webhook.LfPatientId ?? order.LfPatientId;
                order.LfReferenceId = webhook.LfReferenceId ?? order.LfReferenceId;
                order.MessageId = webhook.MessageId ?? order.MessageId;
                order.CanonicalOrderId = webhook.CanonicalOrderId ?? order.CanonicalOrderId;
                order.SalesForceOrderId = webhook.SalesForceOrderId ?? order.SalesForceOrderId;
                order.SalesForceClinicAccountId = webhook.SalesForceClinicAccountId ?? order.SalesForceClinicAccountId;
                order.LifeFilePracticeId = webhook.LifeFile_Practice_ID__c ?? order.LifeFilePracticeId;

                if (!string.IsNullOrWhiteSpace(webhook.Reference1))
                    order.Reference1 = webhook.Reference1;
                if (!string.IsNullOrWhiteSpace(webhook.Reference2))
                    order.Reference2 = webhook.Reference2;
                if (!string.IsNullOrWhiteSpace(webhook.Reference3))
                    order.Reference3 = webhook.Reference3;
                if (!string.IsNullOrWhiteSpace(webhook.Reference4))
                    order.Reference4 = webhook.Reference4;
                if (!string.IsNullOrWhiteSpace(webhook.Reference5))
                    order.Reference5 = webhook.Reference5;

                if (!string.IsNullOrWhiteSpace(webhook.OrderStatus))
                {
                    order.OrderStatus = webhook.OrderStatus;
                    order.OrderStatusLastUpdatedTime = webhook.OrderStatusLastUpdatedTime ?? DateTime.UtcNow;
                }

                if (!string.IsNullOrWhiteSpace(webhook.Error))
                {
                    order.Error = webhook.Error;
                }

                if (!string.IsNullOrWhiteSpace(webhook.PrescriptionPdfBase64))
                {
                    order.PrescriptionPdfBase64 = webhook.PrescriptionPdfBase64;
                }

                if (webhook.ShipmentLines != null && webhook.ShipmentLines.Count > 0)
                {
                    var latestShipment = webhook.ShipmentLines
                        .OrderByDescending(s => s.ShipmentStatusLastUpdatedTime)
                        .FirstOrDefault();
                    if (latestShipment != null)
                    {
                        order.ShipmentStatus = latestShipment.ShipmentStatus ?? order.ShipmentStatus;
                        order.ShipmentTrackingNumber = latestShipment.ShipmentTrackingNumber ?? order.ShipmentTrackingNumber;
                        order.ShipmentTrackingUrl = latestShipment.ShipmentTrackingUrl ?? order.ShipmentTrackingUrl;
                        order.ShipmentProvider = latestShipment.ShipmentProvider ?? order.ShipmentProvider;
                        order.ShipmentStatusLastUpdatedTime = latestShipment.ShipmentStatusLastUpdatedTime ?? DateTime.UtcNow;
                    }
                }

                if (order.PatientPrescriptionId == null && !string.IsNullOrWhiteSpace(order.ClientOrderId))
                {

                    var parts = order.ClientOrderId.Split('_');
                    if (parts.Length >= 4 && parts[2] == "pres" && long.TryParse(parts[3], out var prescriptionId))
                    {
                        var prescription = await _db.PT_PatientPrescriptions
                            .FirstOrDefaultAsync(p => p.PatientPrescriptionId == prescriptionId);

                        if (prescription != null)
                        {
                            order.PatientPrescriptionId = prescriptionId;
                            order.PatientId = prescription.PatientId;
                            order.FacilityId = prescription.FacilityId;
                        }
                    }
                }

                if (order.PatientPrescriptionId.HasValue)
                {
                    var prescription = await _db.PT_PatientPrescriptions
                        .FirstOrDefaultAsync(p => p.PatientPrescriptionId == order.PatientPrescriptionId.Value);

                    if (prescription != null)
                    {

                        if (!string.IsNullOrWhiteSpace(order.OrderStatus))
                        {
                            switch (order.OrderStatus)
                            {
                                case "Received":
                                    prescription.PrescriptionStatus = "Received";
                                    break;
                                case "Processing":
                                    prescription.PrescriptionStatus = "Processing";
                                    break;
                                case "Complete":
                                    prescription.PrescriptionStatus = "Complete";
                                    break;
                            }

                            prescription.ModifiedDate = DateTime.UtcNow;
                        }

                        if (prescription.PatientOrderId.HasValue)
                        {
                            var patientOrder = await _db.PT_PatientOrders
                                .FirstOrDefaultAsync(o => o.PatientOrderId == prescription.PatientOrderId.Value);

                            if (patientOrder != null)
                            {
                                if (!string.IsNullOrWhiteSpace(order.OrderStatus))
                                {

                                    patientOrder.OrderStatus = order.OrderStatus == "Complete"
                                        ? "Completed"
                                        : order.OrderStatus;
                                    patientOrder.ModifiedDate = DateTime.UtcNow;
                                }

                                if (!string.IsNullOrWhiteSpace(order.ShipmentTrackingNumber))
                                {
                                    patientOrder.TrackingNumber = order.ShipmentTrackingNumber;
                                    patientOrder.ShippedDate = order.ShipmentStatusLastUpdatedTime ?? DateTime.UtcNow;
                                }
                            }
                        }
                    }
                }

                order.ModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger?.LogInformation(
                    "Empower webhook processed: ClientOrderId={ClientOrderId}, Status={Status}, EipOrderId={EipOrderId}",
                    webhook.ClientOrderId, webhook.OrderStatus, webhook.EipOrderId);

                return new EmpowerWebhookResponseDTO
                {
                    Success = true,
                    Message = $"Order {webhook.OrderStatus} successfully",
                    EmpowerOrderId = order.EmpowerOrdersId,
                    OrderStatus = order.OrderStatus
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Error processing Empower webhook: ClientOrderId={ClientOrderId}",
                    webhook?.ClientOrderId);

                return new EmpowerWebhookResponseDTO
                {
                    Success = false,
                    Message = $"Error processing webhook: {ex.Message}"
                };
            }
        }

        public async Task<Sys_EmpowerOrder?> GetOrderByClientOrderIdAsync(string clientOrderId)
        {
            if (string.IsNullOrWhiteSpace(clientOrderId))
                return null;

            return await _db.Sys_EmpowerOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.ClientOrderId == clientOrderId && o.IsActive == true);
        }

        public async Task<Sys_EmpowerOrder?> GetOrderByEipOrderIdAsync(int eipOrderId)
        {
            return await _db.Sys_EmpowerOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.EipOrderId == eipOrderId && o.IsActive == true);
        }
    }
}
