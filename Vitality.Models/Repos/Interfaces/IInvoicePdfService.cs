using Vitality.Models.DTOs.Invoices;

namespace Vitality.Models.Repos.Interfaces
{

    public interface IInvoicePdfService
    {

        Task<bool> GenerateFacilityInvoicePdfAsync(GetDetailedFacilityInvoiceResponseDTO invoiceData, string outputPath);

        string GetInvoicePdfRelativePath(string invoiceNumber, long invoiceId);
    }
}
