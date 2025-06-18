using Car_Insurance.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Car_Insurance.Services;

/// <summary>
/// Service for generating car insurance policy documents in PDF format.
/// </summary>
public sealed class PolicyService : IPolicyService
{
    /// <summary>
    /// Generates a dummy car insurance policy PDF as a byte array.
    /// </summary>
    /// <returns>Generated PDF content as a byte array.</returns>
    public byte[] Generate()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // Content
        var lines = new[]
        {
            "This dummy policy covers the insured vehicle against accidents and theft.",
            $"Issued: {DateTime.UtcNow:yyyy-MM-dd}"
        };
        // PDF file generation
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Header().AlignCenter().Text("Car Insurance Policy").FontSize(20).Bold();
                page.Content().Text(txt =>
                {
                    foreach (var line in lines)
                        txt.Line(line);
                });
                page.Footer().AlignCenter().Text($"Generated on {DateTime.UtcNow:yyyy-MM-dd}.");
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return ms.ToArray();
    }
}