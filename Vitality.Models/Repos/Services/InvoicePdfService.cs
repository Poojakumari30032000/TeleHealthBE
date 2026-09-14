using System;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Vitality.Models.DTOs.Invoices;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{

    public class InvoicePdfService : IInvoicePdfService
    {
        public InvoicePdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<bool> GenerateFacilityInvoicePdfAsync(GetDetailedFacilityInvoiceResponseDTO invoiceData, string outputPath)
        {
            try
            {

                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Element(ComposeHeader);

                        page.Content()
                            .Element(container => ComposeContent(container, invoiceData));

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.CurrentPageNumber();
                                x.Span(" / ");
                                x.TotalPages();
                            });
                    });
                });

                await Task.Run(() => document.GeneratePdf(outputPath));
                return File.Exists(outputPath);
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Error generating PDF: {ex.Message}");
                return false;
            }
        }

        private void ComposeHeader(IContainer container)
        {
            container
                .Row(row =>
                {
                    row.RelativeColumn().Column(column =>
                    {
                        column.Item().Text("TelehealthUS HEALTHCARE").FontSize(20).FontFamily(Fonts.Calibri).Bold();
                        column.Item().Text("Monthly Facility Invoice").FontSize(14).FontFamily(Fonts.Calibri);
                    });

                    row.ConstantItem(100).AlignRight().Column(column =>
                    {
                        column.Item().Text("Invoice").FontSize(12).FontFamily(Fonts.Calibri).Bold();
                    });
                });
        }

        private void ComposeContent(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            container
                .PaddingVertical(1, Unit.Centimetre)
                .Column(column =>
                {
                    column.Spacing(0.5f, Unit.Centimetre);

                    column.Item().Element(compose => ComposeInvoiceInfo(compose, invoiceData));

                    column.Item().Element(compose => ComposeFacilityInfo(compose, invoiceData));

                    column.Item().Element(compose => ComposeSummary(compose, invoiceData));

                    if (invoiceData.ProviderInvoiceDetails != null && invoiceData.ProviderInvoiceDetails.Any())
                    {
                        column.Item().Element(compose => ComposeProviderDetails(compose, invoiceData));
                    }

                    if (invoiceData.OrderDetails != null && invoiceData.OrderDetails.Any())
                    {
                        column.Item().Element(compose => ComposeOrderDetails(compose, invoiceData));
                    }

                    if (invoiceData.MedicationInvoiceDetails != null && invoiceData.MedicationInvoiceDetails.Any())
                    {
                        column.Item().Element(compose => ComposeMedicationDetails(compose, invoiceData));
                    }

                    column.Item().Element(compose => ComposeTotals(compose, invoiceData));
                });
        }

        private void ComposeInvoiceInfo(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            container
                .Background(Colors.Grey.Lighten3)
                .Padding(10)
                .Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text($"Invoice Number: {invoiceData.InvoiceNumber ?? "N/A"}").FontSize(11).Bold();
                        column.Item().Text($"Invoice Date: {invoiceData.InvoiceDate?.ToString("MM/dd/yyyy") ?? "N/A"}");
                        column.Item().Text($"Due Date: {invoiceData.DueDate?.ToString("MM/dd/yyyy") ?? "N/A"}");

                    });
                });
        }

        private void ComposeFacilityInfo(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            container
                .Padding(10)
                .Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("Bill To:").FontSize(12).Bold();
                        column.Item().Text(invoiceData.FacilityName ?? "N/A").FontSize(11);
                        if (!string.IsNullOrWhiteSpace(invoiceData.FacilityEmail))
                            column.Item().Text($"Email: {invoiceData.FacilityEmail}").FontSize(10);
                        if (!string.IsNullOrWhiteSpace(invoiceData.FacilityAddress))
                            column.Item().Text($"Address: {invoiceData.FacilityAddress}").FontSize(10);
                    });
                });
        }

        private void ComposeSummary(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            var summary = invoiceData.Summary ?? new InvoiceSummaryDTO();

            container
                .Background(Colors.Blue.Lighten5)
                .Padding(10)
                .Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("Summary").FontSize(12).Bold();
                        column.Item().Text($"Total Appointments: {summary.TotalAppointments}");
                        column.Item().Text($"Total Orders: {summary.TotalOrders}");

                    });
                });
        }

        private void ComposeProviderDetails(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            container
                .Padding(5)
                .Column(column =>
                {
                    column.Item().PaddingBottom(5).Text("Provider Invoice Details").FontSize(14).Bold();

                    foreach (var providerDetail in invoiceData.ProviderInvoiceDetails)
                    {
                        column.Item().Element(elem => ComposeProviderDetail(elem, providerDetail));
                    }
                });
        }

        private void ComposeProviderDetail(IContainer container, ProviderInvoiceDetailDTO providerDetail)
        {
            container
                .Border(1)
                .BorderColor(Colors.Grey.Medium)
                .Padding(10)
                .Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Provider: {providerDetail.ProviderName ?? "N/A"}").Bold();
                        row.ConstantItem(150).Text($"Appointments: {providerDetail.AppointmentCount}").AlignRight();
                        row.ConstantItem(120).Text($"Total: ${providerDetail.TotalAppointmentAmount?.ToString("F2") ?? "0.00"}").AlignRight();
                    });

                    if (providerDetail.Appointments != null && providerDetail.Appointments.Any())
                    {
                        column.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Date").FontSize(9).Bold();
                                header.Cell().Element(CellStyle).Text("Patient").FontSize(9).Bold();
                                header.Cell().Element(CellStyle).Text("Treatment").FontSize(9).Bold();
                                header.Cell().Element(CellStyle).Text("Status").FontSize(9).Bold();
                                header.Cell().Element(CellStyle).Text("Amount").FontSize(9).Bold();
                            });

                            foreach (var appointment in providerDetail.Appointments
                                .Where(a => a.AppointmentStatus != null &&
                                           a.AppointmentStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                                .Take(10))
                            {
                                table.Cell().Element(CellStyle).Text(appointment.AppointmentDate?.ToString("MM/dd/yyyy") ?? "N/A").FontSize(8);
                                table.Cell().Element(CellStyle).Text(appointment.PatientName ?? "N/A").FontSize(8);
                                table.Cell().Element(CellStyle).Text(appointment.TreatmentName ?? "N/A").FontSize(8);
                                table.Cell().Element(CellStyle).Text(appointment.AppointmentStatus ?? "N/A").FontSize(8);
                                table.Cell().Element(CellStyle).Text($"${appointment.AppointmentAmount?.ToString("F2") ?? "0.00"}").FontSize(8);
                            }
                        });
                    }
                });
        }

        private void ComposeOrderDetails(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            container
                .Padding(5)
                .Column(column =>
                {
                    column.Item().PaddingBottom(5).Text("Order Details").FontSize(14).Bold();

                    foreach (var orderDetail in invoiceData.OrderDetails.Take(20))
                    {
                        column.Item().Element(elem => ComposeOrderDetail(elem, orderDetail));
                    }

                    if (invoiceData.OrderDetails.Count > 20)
                    {
                        column.Item().Text($"... and {invoiceData.OrderDetails.Count - 20} more orders").FontSize(9).Italic();
                    }
                });
        }

        private void ComposeOrderDetail(IContainer container, FacilityInvoiceOrderDTO orderDetail)
        {
            container
                .Border(1)
                .BorderColor(Colors.Grey.Medium)
                .Padding(8)
                .Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Order #{orderDetail.OrderId}").Bold();
                        row.ConstantItem(150).Text($"Date: {orderDetail.OrderDate?.ToString("MM/dd/yyyy") ?? "N/A"}").AlignRight();

                    });

                    column.Item().Text($"Patient: {orderDetail.PatientName ?? "N/A"}").FontSize(9);

                    if (!string.IsNullOrWhiteSpace(orderDetail.TreatmentName))
                    {
                        column.Item().Text($"Treatment: {orderDetail.TreatmentName}").FontSize(9);
                    }

                    var orderTotal = orderDetail.RetailAmount ?? orderDetail.WholesaleAmount ?? 0m;

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total: ${orderTotal.ToString("F2")}").FontSize(10).Bold();
                    });

                });
        }

        private void ComposeMedicationDetails(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            container
                .Padding(5)
                .Column(column =>
                {
                    column.Item().PaddingBottom(5).Text("Medication Details").FontSize(14).Bold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Product").FontSize(9).Bold();
                            header.Cell().Element(CellStyle).Text("Quantity").FontSize(9).Bold();
                            header.Cell().Element(CellStyle).Text("Unit Price").FontSize(9).Bold();
                            header.Cell().Element(CellStyle).Text("Total Price").FontSize(9).Bold();
                            header.Cell().Element(CellStyle).Text("Bundle").FontSize(9).Bold();
                        });

                        foreach (var medication in invoiceData.MedicationInvoiceDetails.Take(15))
                        {
                            table.Cell().Element(CellStyle).Text(medication.ProductName ?? "N/A").FontSize(8);
                            table.Cell().Element(CellStyle).Text(medication.Quantity.ToString()).FontSize(8);
                            table.Cell().Element(CellStyle).Text($"${medication.UnitPrice?.ToString("F2") ?? "0.00"}").FontSize(8);
                            table.Cell().Element(CellStyle).Text($"${medication.TotalPrice?.ToString("F2") ?? "0.00"}").FontSize(8);
                            table.Cell().Element(CellStyle).Text(medication.BundleName ?? "N/A").FontSize(8);
                        }
                    });
                });
        }

        private void ComposeTotals(IContainer container, GetDetailedFacilityInvoiceResponseDTO invoiceData)
        {
            container
                .Background(Colors.Grey.Lighten4)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().PaddingBottom(5).Text("Invoice Totals").FontSize(14).Bold();

                    var ordersTotal = invoiceData.PharmacyBillTotal ?? invoiceData.WholesaleTotal ?? 0m;
                    var appointmentsTotal = invoiceData.ProviderBillTotal ?? 0m;

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Orders Total:");
                        row.ConstantItem(150).Text($"${ordersTotal.ToString("F2")}").AlignRight();
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Appointments Total:");
                        row.ConstantItem(150).Text($"${appointmentsTotal.ToString("F2")}").AlignRight();
                    });

                    var platformFee = invoiceData.PlatformFee ?? 0m;
                    if (platformFee > 0m)
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Platform Fee (Monthly Subscription):");
                            row.ConstantItem(150).Text($"${platformFee.ToString("F2")}").AlignRight();
                        });
                    }

                    column.Item().PaddingTop(5).BorderTop(1).BorderColor(Colors.Black).Row(row =>
                    {
                        row.RelativeItem().Text("Grand Total:").FontSize(12).Bold();
                        row.ConstantItem(150).Text($"${invoiceData.TotalAmount?.ToString("F2") ?? "0.00"}").FontSize(12).Bold().AlignRight();
                    });
                });
        }

        private IContainer CellStyle(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(5)
                .PaddingHorizontal(5);
        }

        public string GetInvoicePdfRelativePath(string invoiceNumber, long invoiceId)
        {

            var sanitizedInvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber)
                ? $"INV-{invoiceId}"
                : invoiceNumber.Replace("/", "-").Replace("\\", "-").Replace(":", "-");

            return Path.Combine("Invoices", $"{sanitizedInvoiceNumber}.pdf").Replace("\\", "/");
        }
    }
}
