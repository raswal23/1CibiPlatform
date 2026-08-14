namespace ATS.Services.FilePDFService;

public interface IFilePdfService
{
	Task<MemoryStream> GenerateConsentFormPdfAsync(string applicantName, DateOnly signedDate, byte[] signatureImage, CancellationToken cancellationToken = default);

	Task<MemoryStream> ConvertImageToPdfAsync(IFormFile image, CancellationToken cancellationToken);

}
