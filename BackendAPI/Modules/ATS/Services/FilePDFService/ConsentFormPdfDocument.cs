namespace ATS.Services.FilePDFService;

public class ConsentFormPdfDocument : IDocument
{
	private readonly string _applicantName;
	private readonly DateOnly _signedDate;
	private readonly byte[] _signatureImage;

	private const string PrimaryBlue = "#174A9C";
	private const string BulletBlue = "#1E88E5";
	private const string Divider = "#DCE6F4";
	private const string TextColor = "#333333";

	public ConsentFormPdfDocument(
		string applicantName,
		DateOnly signedDate,
		byte[] signatureImage)
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
			page.Size(PageSizes.A4);
			page.MarginHorizontal(40);
			page.MarginVertical(35);

			page.DefaultTextStyle(x =>
				x.FontSize(10)
				 .FontColor(TextColor));

			page.Content().Column(column =>
			{
				column.Spacing(12);

				BuildConsentSection(column);

				column.Item()
					.PaddingVertical(10)
					.LineHorizontal(0.8f)
					.LineColor(Divider);

				BuildReleaseSection(column);
			});
			page.Footer()
				.Element(container =>
				{
					container.Column(column =>
					{
						BuildSignatureSection(column);
					});
				});
		});
	}
	private void BuildConsentSection(ColumnDescriptor column)
	{
		column.Item().Row(row =>
		{
			row.ConstantItem(40)
				.AlignTop()
				.Width(24)
				.Height(24)
				.Svg(PdfIcons.Shield);

			row.RelativeItem().Column(c =>
			{
				c.Item()
					.Text(ConsentFormTextConstants.ConsentTitle)
					.Bold()
					.FontSize(16)
					.FontColor(PrimaryBlue);

				c.Item()
					.PaddingTop(5)
					.Text(ConsentFormTextConstants.ConsentIntro)
					.Justify()
					.LineHeight(1.3f);
			});
		});

		column.Item().PaddingTop(12);

		foreach (var item in ConsentFormTextConstants.ConsentItems)
		{
			BulletItem(column, item);
		}
	}
	private void BuildReleaseSection(ColumnDescriptor column)
	{
		column.Item().Row(row =>
		{
			row.ConstantItem(40)
				.AlignTop()
				.Width(24)
				.Height(24)
				.Svg(PdfIcons.Document);

			row.RelativeItem().Column(c =>
			{
				c.Item()
					.Text(ConsentFormTextConstants.ReleaseTitle)
					.Bold()
					.FontSize(16)
					.FontColor(PrimaryBlue);

				c.Item()
					.PaddingTop(8)
					.Text(text =>
					{
						text.Span("Purpose of Consent: ").Bold().FontSize(11);
						text.Span(
							"Background Screening/Credit (Due Diligence) Check").FontSize(11);
					})
					;

				c.Item()
					.PaddingTop(8)
					.Text(ConsentFormTextConstants.ReleaseText)
					.FontSize(11)
					.Justify()
					.LineHeight(1.3f);

				c.Item()
					.PaddingTop(8)
					.Text("I certify that the information set out by me in this authorization/consent is correct.").FontSize(11);

				c.Item()
					.PaddingTop(10)
					.Row(r =>
					{
						r.ConstantItem(18)
							.PaddingTop(2)
							.PaddingRight(2)
							.Svg(PdfIcons.Info);

						r.RelativeItem()
							.Text(text =>
							{
								text.Span("You may reach out to the DPO at ");

								text.Span("dpo@cibi.com.ph")
									.Underline()
									.FontColor(PrimaryBlue);
							});
					});
			});
		});
	}
	private void BuildSignatureSection(ColumnDescriptor column)
	{
		column.Item().Row(row =>
		{
			SignatureColumn(
				row.RelativeItem(),
				"SIGNATURE",
				c =>
				{
					if (_signatureImage != null && _signatureImage.Length > 0)
					{
						c.Image(_signatureImage, ImageScaling.FitHeight);
					}
				});

			row.ConstantItem(20);

			SignatureColumn(
				row.RelativeItem(),
				"NAME",
				c =>
				{
					c.PaddingTop(18)
					 .Text(_applicantName)
					 .FontSize(11);
				});

			row.ConstantItem(20);

			SignatureColumn(
				row.RelativeItem(),
				"DATE",
				c =>
				{
					c.PaddingTop(18)
					 .Text(_signedDate.ToString("MMMM dd, yyyy"))
					 .FontSize(11);
				});
		});
	}
	private void BulletItem(ColumnDescriptor column, string text)
	{
		column.Item()
			.PaddingLeft(23)
			.Row(row =>
		{
			row.ConstantItem(40)
				.AlignTop()
				.AlignCenter()
				.Text("•")
				.FontSize(12f)
				.FontColor(BulletBlue);

			row.RelativeItem()
				.Text(text)
				.FontSize(11)
				.Justify()
				.LineHeight(1.3f);
		});
	}
	private void SignatureColumn(
	IContainer container,
	string title,
	Action<IContainer> content)
	{
		container.Column(column =>
		{
			column.Item()
				.Text(title)
				.SemiBold()
				.FontSize(11)
				.FontColor("#666666");

			column.Item()
				.Height(35)
				.Element(content);

			column.Item()
				.LineHorizontal(1)
				.LineColor("#A8A8A8");
		});
	}
}