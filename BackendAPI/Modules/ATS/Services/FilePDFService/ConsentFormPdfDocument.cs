namespace ATS.Services.FilePDFService;

public class ConsentFormPdfDocument : IDocument
{
	private readonly string _applicantName;
	private readonly DateOnly _signedDate;
	private readonly byte[] _signatureImage;

	public ConsentFormPdfDocument(string applicantName, DateOnly signedDate, byte[] signatureImage)
	{
		_applicantName = applicantName;
		_signedDate = signedDate;
		_signatureImage = signatureImage;
	}

	public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

	public void Compose(IDocumentContainer container)
	{
		container.Page(page =>
		{
			page.Margin(30);
			page.Size(PageSizes.A4);
			page.DefaultTextStyle(x => x.FontSize(10));

			page.Content().Column(column =>
			{
				column.Spacing(10);

				column.Item().AlignCenter().Text(ConsentFormTextConstants.ConsentTitle).SemiBold().FontSize(14);

				foreach (var text in ConsentFormTextConstants.ConsentItems)
				{
					column.Item().Text(text);
				}

				column.Item().PaddingTop(6).AlignCenter().Text(ConsentFormTextConstants.ReleaseTitle).SemiBold().FontSize(12);
				column.Item().Text(ConsentFormTextConstants.PurposeText);
				column.Item().Text(ConsentFormTextConstants.ReleaseText);
				column.Item().Text(ConsentFormTextConstants.DpoText);

				column.Item().PaddingTop(20).Text($"Applicant Name: {_applicantName}");

				column.Item().PaddingTop(8).Text("Signature:");
				column.Item().Height(80).Width(260).Image(_signatureImage, ImageScaling.FitArea);

				column.Item().PaddingTop(6).Text($"Signed Date: {_signedDate:MMMM dd, yyyy}");
			});
		});
	}
}
