namespace ATS.Services.FilePDFService;

public class ConsentFormPdfDocument : IDocument
{
	private readonly string _applicantName;
	private readonly DateOnly _signedDate;
	private readonly byte[] _signatureImage;

	// ==================== THEME TOKENS ====================
	// Product brand gradient: linear-gradient(100deg, #0B1B3D 0%, #1C3A70 35%, #1D5FD1 75%, #4F93EA 100%).
	// The stops below are the canonical brand colors used across the PDF.

	private static readonly string Navy950 = "#0B1B3D";  // gradient stop 0%
	private static readonly string Navy800 = "#1C3A70";  // gradient stop 35%
	private static readonly string Blue600 = "#1D5FD1";  // gradient stop 75%
	private static readonly string Blue400 = "#4F93EA";  // gradient stop 100%
	private static readonly Color[] BrandGradient = [Navy950, Navy800, Blue600, Blue400];
	private static readonly string TextPrimary = "#101828";
	private static readonly string TextSecondary = "#4B5468";
	private static readonly string TextMuted = "#8992A6";
	private static readonly string BorderSoft = "#E6EAF2";
	private static readonly string NoticeBg = "#EEF3FE";
	private static readonly string NoticeBorder = "#D7E4FC";
	private static readonly string FieldBg = "#FAFBFD";
	private static readonly string WatermarkColor = "#121D5FD1"; // Blue600 at ~7% alpha

	public ConsentFormPdfDocument(
		string applicantName,
		DateOnly signedDate,
		byte[] signatureImage)
	{
		_applicantName = applicantName;
		_signedDate = signedDate;
		_signatureImage = signatureImage;
	}


	public void Compose(IDocumentContainer container)
	{
		container.Page(page =>
		{
			page.Size(PageSizes.A4);
			page.MarginHorizontal(40);
			page.MarginVertical(30);

			page.DefaultTextStyle(x =>
				x.FontSize(9.5f)
				 .FontColor(TextPrimary));

			page.Background().Element(BuildWatermark);

			page.Header().Element(BuildBrandBar);

			page.Content().PaddingTop(14).Column(column =>
			{
				column.Spacing(9);

				BuildConsentSection(column);

				column.Item().PaddingVertical(3).Element(BuildSectionDivider);

				BuildReleaseSection(column);

				column.Item().PaddingTop(8);

				BuildSignatureSection(column);
			});

			page.Footer().Element(BuildFooter);
		});
	}

	// ==================== BACKGROUND ====================

	private void BuildWatermark(IContainer container)
	{
		// A4 is 595 x 842 pt; translate to the page center, rotate, then pull back by
		// roughly half the rendered text size so the watermark pivots on its own center.
		container
			.TranslateX(297.5f)
			.TranslateY(421)
			.Rotate(-36)
			.TranslateX(-236)
			.TranslateY(-33)
			.Text("CONFIDENTIAL")
			.Black()
			.FontSize(52)
			.LetterSpacing(0.15f)
			.FontColor(WatermarkColor);
	}

	// ==================== HEADER / FOOTER ====================

	private void BuildBrandBar(IContainer container)
	{
		container.Column(outer =>
		{
			outer.Item().Row(row =>
			{
				row.ConstantItem(38).AlignMiddle().Element(c => c
					.Width(30).Height(30)
					.CornerRadius(8)
					.BackgroundLinearGradient(100, BrandGradient)
					.Padding(7)
					.Svg(PdfIcons.LogoRing));

				row.RelativeItem().AlignMiddle().Column(c =>
				{
					c.Item().Text("CIBI").Bold().FontSize(14).FontColor(Navy950);
					c.Item().Text("Information Inc.").FontSize(7.5f).FontColor(TextMuted);
				});

				row.ConstantItem(230).AlignMiddle().Column(c =>
				{
					c.Item().AlignRight().Text("Consent Form").Bold().FontSize(11).FontColor(TextPrimary);
					c.Item().AlignRight().PaddingTop(2)
						.Text($"REF: CF-{_signedDate:yyyy-MMdd}  ·  Background Screening")
						.FontSize(6.5f).FontColor(TextMuted).LetterSpacing(0.08f);
				});
			});

			// brand gradient rule matching the product's hero gradient
			outer.Item().PaddingTop(9)
				.Height(2.5f)
				.BackgroundLinearGradient(100, BrandGradient);
		});
	}

	private void BuildFooter(IContainer container)
	{
		container.PaddingTop(6).Column(col =>
		{
			col.Item().LineHorizontal(0.8f).LineColor(BorderSoft);

			col.Item().PaddingTop(6).Row(row =>
			{
				row.ConstantItem(12).AlignMiddle().Element(e => e.Width(7).Height(8).Svg(PdfIcons.Lock));

				row.RelativeItem().AlignMiddle()
					.Text("This document contains confidential information intended solely for the named applicant.")
					.FontSize(7).FontColor(TextMuted);

				row.ConstantItem(70).AlignMiddle().AlignRight().Text(text =>
				{
					text.DefaultTextStyle(x => x.FontSize(7).FontColor(TextMuted));
					text.Span("Page ");
					text.CurrentPageNumber();
					text.Span(" of ");
					text.TotalPages();
				});
			});
		});
	}

	// ==================== SECTIONS ====================

	private void SectionHeader(ColumnDescriptor column, string svgIcon, string? sectionLabel, string title)
	{
		column.Item().Row(row =>
		{
			row.ConstantItem(38).AlignMiddle().Element(c => SectionIconBadge(c, svgIcon));

			row.RelativeItem().AlignMiddle().Column(c =>
			{
				if (sectionLabel is not null)
				{
					c.Item().Text(sectionLabel)
						.SemiBold().FontSize(7).FontColor(TextMuted).LetterSpacing(0.18f);
				}

				c.Item().Text(title).Bold().FontSize(14.5f).FontColor(Navy950);
			});
		});
	}

	private void BuildConsentSection(ColumnDescriptor column)
	{
		SectionHeader(column, PdfIcons.Shield, "SECTION 01", ConsentFormTextConstants.ConsentTitle);

		column.Item().Text(ConsentFormTextConstants.ConsentIntro)
			.FontColor(TextSecondary)
			.Justify()
			.LineHeight(1.32f);

		column.Item().Column(c =>
		{
			c.Spacing(7);
			var number = 1;
			foreach (var item in ConsentFormTextConstants.ConsentItems)
			{
				NumberedItem(c, number++, item);
			}
		});
	}

	private void BuildSectionDivider(IContainer container)
	{
		container.Row(row =>
		{
			row.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(BorderSoft);

			row.AutoItem().PaddingHorizontal(10).Text("SECTION 02")
				.SemiBold().FontSize(7).FontColor(TextMuted).LetterSpacing(0.18f);

			row.RelativeItem().AlignMiddle().LineHorizontal(0.8f).LineColor(BorderSoft);
		});
	}

	private void BuildReleaseSection(ColumnDescriptor column)
	{
		SectionHeader(column, PdfIcons.Document, null, ConsentFormTextConstants.ReleaseTitle);

		column.Item().Element(BuildPurposePill);

		column.Item().Text(ConsentFormTextConstants.ReleaseText)
			.FontColor(TextSecondary)
			.Justify()
			.LineHeight(1.32f);

		column.Item().Element(BuildCertifyLine);

		column.Item().PaddingTop(2).Element(BuildDpoNotice);
	}

	private void BuildPurposePill(IContainer container)
	{
		container.AlignLeft()
			.CornerRadius(9)
			.Background(FieldBg)
			.Border(1)
			.BorderColor(NoticeBorder)
			.PaddingVertical(5)
			.PaddingHorizontal(12)
			.Text(text =>
			{
				text.DefaultTextStyle(x => x.FontSize(9));
				text.Span("Purpose of Consent:  ").SemiBold().FontColor(TextPrimary);
				text.Span("Background Screening / Credit (Due Diligence) Check").SemiBold().FontColor(Blue600);
			});
	}

	private void BuildCertifyLine(IContainer container)
	{
		container.Row(row =>
		{
			row.ConstantItem(3).Background(Blue600);

			row.RelativeItem().PaddingLeft(9).AlignMiddle()
				.Text(ConsentFormTextConstants.CertifyText)
				.SemiBold()
				.FontColor(TextPrimary);
		});
	}

	private void BuildDpoNotice(IContainer container)
	{
		container
			.CornerRadius(8)
			.Background(NoticeBg)
			.Border(1)
			.BorderColor(NoticeBorder)
			.Padding(9)
			.Row(r =>
			{
				r.ConstantItem(20).AlignMiddle().Element(e => e
					.Width(14).Height(14)
					.CornerRadius(7)
					.Background(Blue600)
					.Padding(3)
					.Svg(PdfIcons.Info));

				r.RelativeItem().AlignMiddle().Text(text =>
				{
					text.DefaultTextStyle(x => x.FontSize(9).FontColor(TextSecondary));
					text.Span("Questions about how your data is handled? Reach the DPO at ");
					text.Span("dpo@cibi.com.ph").SemiBold().FontColor(Blue600);
				});
			});
	}

	// ==================== SIGNATURE ====================

	private void BuildSignatureSection(ColumnDescriptor column)
	{
		column.Item().Row(row =>
		{
			SignatureBox(
				row.RelativeItem(),
				"SIGNATURE",
				c =>
				{
					if (_signatureImage is not null && _signatureImage.Length > 0)
					{
						c.Padding(5)
						 .AlignCenter()
						 .AlignMiddle()
						 .Image(_signatureImage)
						 .FitArea();
					}
				});

			row.ConstantItem(16);

			SignatureBox(
				row.RelativeItem(),
				"NAME",
				c =>
				{
					c.AlignCenter()
					 .AlignMiddle()
					 .Text(_applicantName)
					 .FontSize(10.5f)
					 .FontColor(TextPrimary);
				});

			row.ConstantItem(16);

			SignatureBox(
				row.RelativeItem(),
				"DATE",
				c =>
				{
					c.AlignCenter()
					 .AlignMiddle()
					 .Text(_signedDate.ToString("MMMM dd, yyyy"))
					 .FontSize(10.5f)
					 .FontColor(TextPrimary);
				});
		});
	}

	private void SignatureBox(IContainer container, string title, Action<IContainer> content)
	{
		container.Column(column =>
		{
			column.Item()
				.Text(title)
				.SemiBold()
				.FontSize(7.5f)
				.FontColor(TextMuted)
				.LetterSpacing(0.12f);

			column.Item()
				.PaddingTop(5)
				.Height(44)
				.CornerRadius(8)
				.Background(FieldBg)
				.Border(1)
				.BorderColor(BorderSoft)
				.Element(content);
		});
	}

	// ==================== SHARED PRIMITIVES ====================

	// Circular brand-gradient badge behind each section icon, matching the icon-badge
	// language used elsewhere in the product. Icon SVGs are white for contrast.
	private void SectionIconBadge(IContainer container, string svgIcon)
	{
		container
			.Width(28).Height(28)
			.CornerRadius(14)
			.BackgroundLinearGradient(100, BrandGradient)
			.Padding(7)
			.Svg(svgIcon);
	}

	private void NumberedItem(ColumnDescriptor column, int number, string text)
	{
		column.Item().Row(row =>
		{
			row.ConstantItem(26).AlignTop().Element(c => c
				.Width(16).Height(16)
				.CornerRadius(8)
				.Background(NoticeBg)
				.Border(1)
				.BorderColor(NoticeBorder)
				.AlignCenter()
				.AlignMiddle()
				.Text(number.ToString())
				.SemiBold()
				.FontSize(8)
				.FontColor(Blue600));

			row.RelativeItem()
				.Text(text)
				.FontColor(TextSecondary)
				.Justify()
				.LineHeight(1.32f);
		});
	}

}
