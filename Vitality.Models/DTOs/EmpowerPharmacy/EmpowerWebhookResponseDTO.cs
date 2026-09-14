namespace Vitality.Models.DTOs.EmpowerPharmacy
{
    public class EmpowerWebhookResponseDTO
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public long? EmpowerOrderId { get; set; }
        public string? OrderStatus { get; set; }
    }
}
