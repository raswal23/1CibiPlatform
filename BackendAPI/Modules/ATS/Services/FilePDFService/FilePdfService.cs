namespace ATS.Services.FilePDFService;

public class FilePdfService : IFilePdfService
{
	public async Task<MemoryStream> ConvertImageToPdfAsync(
	IFormFile imageStream,
	CancellationToken cancellationToken)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		byte[] imageBytes;

		await using (var memory = new MemoryStream())
		{
			await imageStream.CopyToAsync(memory, cancellationToken);
			imageBytes = memory.ToArray();
		}

		var pdf = new MemoryStream();

		Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Margin(20);

				page.Content()
					.Image(imageBytes)
					.FitWidth();
			});
		})
		.GeneratePdf(pdf);

		pdf.Position = 0;

		return pdf;
	}

	public Task<MemoryStream> GenerateConsentFormPdfAsync(string applicantName, DateOnly signedDate, byte[] signatureImage, CancellationToken cancellationToken = default)
	{
		QuestPDF.Settings.License = LicenseType.Community;

		var stream = new MemoryStream();
		var document = new ConsentFormPdfDocument(applicantName, signedDate, signatureImage);
		document.GeneratePdf(stream);
		stream.Position = 0;
		return Task.FromResult(stream);
	}
}
