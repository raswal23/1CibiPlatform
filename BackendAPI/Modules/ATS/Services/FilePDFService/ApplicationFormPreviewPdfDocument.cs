namespace ATS.Services.FilePDFService;

/// <summary>
/// The downloadable counterpart of the on-screen application form preview
/// (ApplicationFormPreviewComponent). Section order, numbering, empty-section
/// skipping, field labels and pills mirror the dialog one-for-one, so what the
/// admin sees is what the PDF says.
/// </summary>
public class ApplicationFormPreviewPdfDocument : IDocument
{
	private readonly ApplicationFormPreviewDTO _preview;

	// Theme tokens shared with ConsentFormPdfDocument - the same brand gradient
	// the preview's hero band uses.
	private static readonly string Navy950 = "#0B1B3D";
	private static readonly string Navy800 = "#1C3A70";
	private static readonly string Blue600 = "#1D5FD1";
	private static readonly string Blue400 = "#4F93EA";
	private static readonly Color[] BrandGradient = [Navy950, Navy800, Blue600, Blue400];
	private static readonly string TextPrimary = "#101828";
	private static readonly string TextSecondary = "#4B5468";
	private static readonly string TextMuted = "#8992A6";
	private static readonly string BorderSoft = "#E6EAF2";
	private static readonly string NoticeBorder = "#D7E4FC";
	private static readonly string FieldBg = "#FAFBFD";
	private static readonly string WatermarkColor = "#121D5FD1";
	private static readonly string GreenBg = "#E7F6EE";
	private static readonly string GreenText = "#177245";
	private static readonly string AmberBg = "#FDF3E1";
	private static readonly string AmberText = "#9A6700";
	private static readonly string BlueBg = "#EEF3FE";

	public ApplicationFormPreviewPdfDocument(ApplicationFormPreviewDTO preview)
	{
		_preview = preview;
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
				column.Spacing(10);

				column.Item().Element(BuildHero);

				var sectionNumber = 0;

				if (_preview.Personal is not null)
					BuildPersonalSection(column, ++sectionNumber);

				if (_preview.Address is not null || _preview.Education is not null)
					BuildResidenceEducationSection(column, ++sectionNumber);

				// Always shown: a missing license or employment history is itself
				// an answer ("none declared"), matching the preview dialog.
				BuildProfessionalBackgroundSection(column, ++sectionNumber);

				if (_preview.References.Count > 0)
					BuildReferencesSection(column, ++sectionNumber);

				if (_preview.Signature is not null)
					BuildSignatureSection(column, ++sectionNumber);
			});

			page.Footer().Element(BuildFooter);
		});
	}

	// ==================== BACKGROUND ====================

	private void BuildWatermark(IContainer container)
	{
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
				// The real CIBI hexagon (embedded copy of images/generic/cibi-icon.png)
				// rather than the placeholder ring glyph.
				row.ConstantItem(38).AlignMiddle().Element(c => c
					.Width(30).Height(30)
					.Image(PdfBrandAssets.CibiIcon)
					.FitArea());

				row.RelativeItem().AlignMiddle().Column(c =>
				{
					c.Item().Text("CIBI").Bold().FontSize(14).FontColor(Navy950);
					c.Item().Text("Information Inc.").FontSize(7.5f).FontColor(TextMuted);
				});

				row.ConstantItem(230).AlignMiddle().Column(c =>
				{
					c.Item().AlignRight().Text("Application Form").Bold().FontSize(11).FontColor(TextPrimary);
					c.Item().AlignRight().PaddingTop(2)
						.Text($"REF: AF-{DateTime.UtcNow:yyyy-MMdd}  ·  Background Screening")
						.FontSize(6.5f).FontColor(TextMuted).LetterSpacing(0.08f);
				});
			});

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
					.Text("This document contains confidential information intended solely for authorized screening use.")
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

	// ==================== HERO ====================

	private void BuildHero(IContainer container)
	{
		container
			.CornerRadius(10)
			.BackgroundLinearGradient(100, BrandGradient)
			.Padding(16)
			.Column(column =>
			{
				column.Spacing(4);

				column.Item().Text(_preview.SubjectName ?? "Applicant")
					.Bold().FontSize(17).FontColor(Colors.White);

				var position = _preview.Personal?.PositionAppliedFor;
				column.Item().Text(string.IsNullOrWhiteSpace(position)
						? "Background Screening Candidate"
						: $"{position}  |  Background Screening Candidate")
					.FontSize(9).FontColor(Colors.White);

				column.Item().PaddingTop(4).Row(row =>
				{
					row.Spacing(6);

					if (_preview.PhilSysVerified)
						HeroPill(row, "PHILSYS VERIFIED");

					if (!string.IsNullOrWhiteSpace(_preview.FilledFormAt))
						HeroPill(row, $"SUBMITTED {_preview.FilledFormAt.ToUpperInvariant()}");
				});
			});
	}

	private static void HeroPill(RowDescriptor row, string text)
	{
		row.AutoItem()
			.CornerRadius(8)
			.Background("#33FFFFFF")
			.PaddingVertical(3)
			.PaddingHorizontal(9)
			.Text(text)
			.SemiBold().FontSize(6.5f).FontColor(Colors.White).LetterSpacing(0.12f);
	}

	// ==================== SECTIONS ====================

	private void BuildPersonalSection(ColumnDescriptor column, int number)
	{
		var p = _preview.Personal!;

		column.Item().Element(c => SectionCard(c, sectionColumn =>
		{
			SectionHeading(sectionColumn, number, "Identity & Candidate Overview", "Personal information provided by the applicant");

			GroupTitle(sectionColumn, "Candidate Information");
			FieldGrid(sectionColumn,
			[
				("Position applied for", p.PositionAppliedFor),
				("First name", p.FirstName),
				("Middle name", p.MiddleName),
				("Last name", p.LastName),
				("Suffix", p.Suffix),
				("Birth date", p.DateOfBirth),
				("Sex", p.Sex),
				("Marital status", p.MaritalStatus),
				("Nationality", p.Nationality),
				("Email address", p.EmailAddress),
				("Alternate email", p.EmailAlternative),
				("Mobile number", p.MobileNumber),
				("Telephone number", p.TelephoneNumber),
				("SSS", p.SSS),
				("TIN", p.TIN)
			], columns: 3);

			if (!string.IsNullOrWhiteSpace(p.GovtIdFileName)
				|| !string.IsNullOrWhiteSpace(p.NbiClearanceFileName)
				|| !string.IsNullOrWhiteSpace(p.ResumeFileName))
			{
				GroupTitle(sectionColumn, "Supporting Documents");
				AttachmentRow(sectionColumn, "Government-issued ID", p.GovtIdFileName);
				AttachmentRow(sectionColumn, "NBI clearance", p.NbiClearanceFileName);
				AttachmentRow(sectionColumn, "Curriculum vitae (CV)", p.ResumeFileName);
			}
		}));
	}

	private void BuildResidenceEducationSection(ColumnDescriptor column, int number)
	{
		column.Item().Element(c => SectionCard(c, sectionColumn =>
		{
			SectionHeading(sectionColumn, number, "Residence & Education", "Residential information and academic profile");

			if (_preview.Address is { } addr)
			{
				GroupTitle(sectionColumn, "Residence Summary");

				AddressCard(sectionColumn, "CURRENT ADDRESS", addr.CurrentAddress, addr.CurrentCity,
					addr.CurrentProvince, addr.CurrentPostalCode, addr.CurrentCountry, addr.CurrentTypeOfOwnership);
				AddressCard(sectionColumn, "PERMANENT ADDRESS", addr.PermanentAddress, addr.PermanentCity,
					addr.PermanentProvince, addr.PermanentPostalCode, addr.PermanentCountry, null);
			}

			if (_preview.Education is { } e)
			{
				GroupTitle(sectionColumn, "Educational Background");
				FieldGrid(sectionColumn,
				[
					("Highest educational attainment", e.HighestEducationalAttainment),
					("Graduation date", e.GraduationDate),
					("Degree / major", e.Degree),
					("Academic institution", e.SchoolName)
				], columns: 2);

				AttachmentRow(sectionColumn, "Diploma / Transcript of Records (TOR)", e.DiplomaFileName);
			}
		}));
	}

	private void BuildProfessionalBackgroundSection(ColumnDescriptor column, int number)
	{
		column.Item().Element(c => SectionCard(c, sectionColumn =>
		{
			SectionHeading(sectionColumn, number, "Professional Background", "Credentials, licenses and employment history");

			GroupTitle(sectionColumn, "Credentials & Licenses");
			sectionColumn.Item().Text("Professional / Relevant License")
				.SemiBold().FontSize(8).FontColor(TextSecondary);

			if (_preview.License is { } l)
			{
				FieldGrid(sectionColumn,
				[
					("License", l.LicenseName),
					("License no.", l.LicenseNumber),
					("Expiry date", l.LicenseExpiryDate)
				], columns: 3);

				AttachmentRow(sectionColumn, "License copy", l.LicenseFileName);
			}
			else
			{
				Pill(sectionColumn, "NONE DECLARED", AmberBg, AmberText);
			}

			GroupTitle(sectionColumn, "Employment Timeline");

			if (_preview.Employers.Count > 0)
			{
				foreach (var employer in _preview.Employers)
					EmployerEntry(sectionColumn, employer);
			}
			else
			{
				Pill(sectionColumn, "NO WORK EXPERIENCE DECLARED", AmberBg, AmberText);
			}
		}));
	}

	private void EmployerEntry(ColumnDescriptor column, EmployerPreviewDTO employer)
	{
		// The dialog's vertical timeline becomes a left blue rail per entry.
		column.Item().Row(row =>
		{
			row.ConstantItem(3).Background(Blue600);

			row.RelativeItem().PaddingLeft(10).Column(entry =>
			{
				entry.Spacing(4);

				var dates = EmploymentDates(employer);
				if (dates.Length > 0)
				{
					entry.Item().Text(dates)
						.SemiBold().FontSize(7.5f).FontColor(Blue600).LetterSpacing(0.12f);
				}

				var statusLine = EmploymentStatusLine(employer);
				if (statusLine.Length > 0)
				{
					entry.Item().Text(statusLine)
						.FontSize(8).FontColor(TextSecondary);
				}

				FieldGrid(entry,
				[
					("Company name", employer.CompanyName),
					("Job title", employer.JobTitle),
					("Company address", employer.CompanyAddress),
					("Reason for leaving", employer.ReasonForLeaving),
					("Supervisor name", employer.SupervisorName),
					("Supervisor contact number", employer.SupervisorContactNumber),
					("Supervisor email", employer.SupervisorEmail)
				], columns: 2);

				AttachmentRow(entry, "Certificate of employment (COE)", employer.CoeFileName);
			});
		});
	}

	private void BuildReferencesSection(ColumnDescriptor column, int number)
	{
		column.Item().Element(c => SectionCard(c, sectionColumn =>
		{
			SectionHeading(sectionColumn, number, "Professional References", "Professional contacts provided by the applicant");

			for (var index = 0; index < _preview.References.Count; index++)
			{
				var reference = _preview.References[index];

				sectionColumn.Item()
					.CornerRadius(8)
					.Background(FieldBg)
					.Border(1)
					.BorderColor(BorderSoft)
					.Padding(9)
					.Column(card =>
					{
						card.Spacing(4);

						card.Item().Text($"REFERENCE {index + 1}")
							.SemiBold().FontSize(7).FontColor(TextMuted).LetterSpacing(0.18f);

						FieldGrid(card,
						[
							("Full name", reference.FullName),
							("Professional relationship", reference.ProfessionalRelationship),
							("Affiliated company", reference.AffiliatedCompany),
							("Email", reference.Email),
							("Mobile", reference.ContactNumber),
							("Best time to contact", reference.BestTimeToContact)
						], columns: 2);

						if (!string.IsNullOrWhiteSpace(reference.ModeOfContact))
							Pill(card, $"PREFERRED: {reference.ModeOfContact.ToUpperInvariant()}", BlueBg, Blue600);
					});
			}
		}));
	}

	private void BuildSignatureSection(ColumnDescriptor column, int number)
	{
		var signature = _preview.Signature!;

		column.Item().Element(c => SectionCard(c, sectionColumn =>
		{
			SectionHeading(sectionColumn, number, "Consent & Signature", "Data privacy consent signed by the applicant");

			FieldGrid(sectionColumn,
			[
				("Signed by", signature.SignerName),
				("Signature date", signature.SignatureDate)
			], columns: 2);

			AttachmentRow(sectionColumn, "Signed consent form", signature.ConsentFormFileName);
		}));
	}

	// ==================== SHARED PRIMITIVES ====================

	private void SectionCard(IContainer container, Action<ColumnDescriptor> content)
	{
		container
			.Border(1)
			.BorderColor(BorderSoft)
			.CornerRadius(10)
			.Padding(12)
			.Column(column =>
			{
				column.Spacing(7);
				content(column);
			});
	}

	private void SectionHeading(ColumnDescriptor column, int number, string title, string subtitle)
	{
		column.Item().Row(row =>
		{
			row.ConstantItem(26).AlignMiddle().Element(c => c
				.Width(20).Height(20)
				.CornerRadius(6)
				.BackgroundLinearGradient(100, BrandGradient)
				.AlignCenter()
				.AlignMiddle()
				.Text(number.ToString())
				.Bold()
				.FontSize(9.5f)
				.FontColor(Colors.White));

			row.RelativeItem().AlignMiddle()
				.Text(title).Bold().FontSize(12.5f).FontColor(Navy950);

			row.AutoItem().AlignMiddle()
				.Text(subtitle).FontSize(7).FontColor(TextMuted);
		});

		column.Item().LineHorizontal(0.8f).LineColor(BorderSoft);
	}

	private static void GroupTitle(ColumnDescriptor column, string title)
	{
		column.Item().PaddingTop(2).Text(title)
			.SemiBold().FontSize(9.5f).FontColor(TextPrimary);
	}

	// Blank answers are omitted so the PDF reads as what was actually filled in,
	// matching the dialog's PreviewField.
	private void FieldGrid(ColumnDescriptor column, (string Label, string? Value)[] fields, int columns)
	{
		var filled = fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)).ToArray();
		if (filled.Length == 0)
			return;

		column.Item().Table(table =>
		{
			table.ColumnsDefinition(definition =>
			{
				for (var i = 0; i < columns; i++)
					definition.RelativeColumn();
			});

			foreach (var (label, value) in filled)
			{
				table.Cell().PaddingRight(10).PaddingBottom(6).Column(cell =>
				{
					cell.Item().Text(label.ToUpperInvariant())
						.SemiBold().FontSize(6.5f).FontColor(TextMuted).LetterSpacing(0.12f);
					cell.Item().PaddingTop(1)
						.BorderBottom(0.8f).BorderColor(BorderSoft).PaddingBottom(3)
						.Text(value!).SemiBold().FontSize(9);
				});
			}
		});
	}

	private void AttachmentRow(ColumnDescriptor column, string label, string? fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
			return;

		column.Item()
			.CornerRadius(8)
			.Background(FieldBg)
			.Border(1)
			.BorderColor(BorderSoft)
			.Padding(7)
			.Row(row =>
			{
				row.ConstantItem(22).AlignMiddle().Element(e => e
					.Width(16).Height(16)
					.CornerRadius(5)
					.Background(Blue600)
					.Padding(4)
					.Svg(PdfIcons.Document));

				row.RelativeItem().AlignMiddle().Column(text =>
				{
					text.Item().Text(label).SemiBold().FontSize(8.5f).FontColor(TextPrimary);
					text.Item().Text(fileName).FontSize(7.5f).FontColor(TextMuted);
				});

				row.AutoItem().AlignMiddle()
					.CornerRadius(7)
					.Background(GreenBg)
					.PaddingVertical(3)
					.PaddingHorizontal(8)
					.Text("ATTACHED")
					.SemiBold().FontSize(6.5f).FontColor(GreenText).LetterSpacing(0.12f);
			});
	}

	private void AddressCard(ColumnDescriptor column, string eyebrow, string? address, string? city,
		string? province, string? postalCode, string? country, string? ownership)
	{
		if (string.IsNullOrWhiteSpace(address))
			return;

		var locality = string.Join(", ", new[] { city, province, postalCode, country }
			.Where(value => !string.IsNullOrWhiteSpace(value)));

		column.Item()
			.CornerRadius(8)
			.Background(FieldBg)
			.Border(1)
			.BorderColor(BorderSoft)
			.Padding(9)
			.Column(card =>
			{
				card.Spacing(3);

				card.Item().Text(eyebrow)
					.SemiBold().FontSize(7).FontColor(TextMuted).LetterSpacing(0.18f);

				card.Item().Text(address).SemiBold().FontSize(9.5f);

				if (!string.IsNullOrWhiteSpace(locality))
					card.Item().Text(locality).FontSize(8.5f).FontColor(TextSecondary);

				if (!string.IsNullOrWhiteSpace(ownership))
					Pill(card, ownership.ToUpperInvariant(), BlueBg, Blue600);
			});
	}

	private void Pill(ColumnDescriptor column, string text, string background, string color)
	{
		column.Item().AlignLeft()
			.CornerRadius(8)
			.Background(background)
			.Border(1)
			.BorderColor(NoticeBorder)
			.PaddingVertical(3)
			.PaddingHorizontal(9)
			.Text(text)
			.SemiBold().FontSize(6.5f).FontColor(color).LetterSpacing(0.12f);
	}

	// Same interpretation the dialog applies to the applicant's yes/no answers.
	private static bool IsAffirmative(string? value) =>
		string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

	private static string EmploymentDates(EmployerPreviewDTO employer)
	{
		var isCurrent = IsAffirmative(employer.CurrentlyEmployed)
			|| (string.IsNullOrWhiteSpace(employer.EndDate) && !string.IsNullOrWhiteSpace(employer.StartDate));
		var end = isCurrent ? "PRESENT" : employer.EndDate?.ToUpperInvariant();
		var start = employer.StartDate?.ToUpperInvariant();

		if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end))
			return string.Empty;

		return string.Join(" – ", new[] { start, end }.Where(value => !string.IsNullOrWhiteSpace(value)));
	}

	private static string EmploymentStatusLine(EmployerPreviewDTO employer)
	{
		var parts = new List<string>();

		if (!string.IsNullOrWhiteSpace(employer.CurrentlyEmployed))
			parts.Add(IsAffirmative(employer.CurrentlyEmployed) ? "Currently employed" : "Previous employment");

		if (!string.IsNullOrWhiteSpace(employer.PermissionToContact))
			parts.Add(IsAffirmative(employer.PermissionToContact) ? "Employer contact permitted" : "Employer contact not permitted");

		return string.Join(" | ", parts);
	}
}
