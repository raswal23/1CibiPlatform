using ATS.Constants;
using ATS.Data.DTO;
using ATS.Services.AuditTrail;
using ClosedXML.Excel;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The writer is pure - rows in, workbook out - so the rendering is asserted directly by
/// reading the produced file back rather than through the service.
/// </summary>
public class AtsAuditWorkbookWriterTests
{
	private const int CauseColumn = 5;
	private const int DetailsColumn = 10;

	private static AuditTrailListDTO Entry(
		string action = "AddClient",
		string outcome = "Success",
		string? failureReason = null,
		string payload = "{}") => new()
		{
			AuditEntryId = Guid.CreateVersion7(),
			OccurredAt = DateTime.UtcNow,
			Action = action,
			Area = "ClientManagement",
			Outcome = outcome,
			FailureReason = failureReason,
			UserFullName = "Russel Gutierrez",
			Payload = payload
		};

	private static XLWorkbook Render(params AuditTrailListDTO[] entries)
	{
		var stream = AtsAuditWorkbookWriter.Write(entries);

		return new XLWorkbook(stream);
	}

	[Fact]
	public void Write_ShouldPutTheFailureCauseInItsOwnColumn()
	{
		// Arrange: the cause is the reason anyone opens this file, so it is a column rather
		// than something buried in the payload.
		var entry = Entry(
			outcome: AuditOutcome.Failure,
			failureReason: "SMTP 454 Too many login attempts");

		// Act
		using var workbook = Render(entry);

		// Assert
		var sheet = workbook.Worksheets.First();

		sheet.Cell(1, CauseColumn).GetString().Should().Be("Cause of failure");
		sheet.Cell(2, CauseColumn).GetString().Should().Contain("454");
	}

	[Fact]
	public void Write_ShouldHighlightFailedRows()
	{
		// Arrange: a reader scanning a thousand rows should find the failures without
		// reading a column, so the tint covers the whole row.
		var success = Entry();
		var failure = Entry(outcome: AuditOutcome.Failure, failureReason: "Boom");

		// Act
		using var workbook = Render(success, failure);

		// Assert
		var sheet = workbook.Worksheets.First();

		var successFill = sheet.Cell(2, 1).Style.Fill.BackgroundColor;
		var failureFill = sheet.Cell(3, 1).Style.Fill.BackgroundColor;

		failureFill.Should().NotBe(successFill);
	}

	[Fact]
	public void Write_ShouldRenderAnAssistantTranscriptAsQuestionAndAnswer()
	{
		// Arrange: raw JSON in a spreadsheet cell is unreadable, and "what did they ask and
		// what were they told" is the whole reason these rows are exported.
		var payload = """
			{
				"Question": "show me today's failures",
				"Answer": "There were 12 failed actions today.",
				"WasRefused": false,
				"OrderResultCount": 0,
				"AuditResultCount": 12,
				"StagedOrderDraft": false
			}
			""";

		var entry = Entry(action: "AskAtsAssistant", payload: payload);

		// Act
		using var workbook = Render(entry);

		// Assert
		var details = workbook.Worksheets.First().Cell(2, DetailsColumn).GetString();

		details.Should().Contain("show me today's failures");
		details.Should().Contain("There were 12 failed actions today.");
		details.Should().StartWith("Q:");
		details.Should().Contain("A:");
	}

	[Fact]
	public void Write_ShouldMarkARefusedTranscript()
	{
		// Arrange: surfaced as its own line rather than left to be inferred from the
		// answer's wording, which may change.
		var payload = """
			{
				"Question": "what is the weather",
				"Answer": "I can't answer that because it isn't related to ATS.",
				"WasRefused": true
			}
			""";

		var entry = Entry(action: "AskAtsAssistant", payload: payload);

		// Act
		using var workbook = Render(entry);

		// Assert
		workbook.Worksheets.First()
			.Cell(2, DetailsColumn)
			.GetString()
			.Should()
			.Contain("refused");
	}

	[Fact]
	public void Write_ShouldFallBackToRawJson_WhenThePayloadIsNotATranscript()
	{
		// Arrange: an ordinary command keeps the payload the detail dialog shows.
		var entry = Entry(payload: """{"ClientName":"Acme"}""");

		// Act
		using var workbook = Render(entry);

		// Assert
		workbook.Worksheets.First()
			.Cell(2, DetailsColumn)
			.GetString()
			.Should()
			.Contain("Acme");
	}

	[Fact]
	public void Write_ShouldNotTreatLeadingEqualsAsAFormula()
	{
		// Arrange: a failure reason or transcript beginning '=' must not execute when the
		// file is opened. This is the CSV-injection class of bug, in a workbook.
		var entry = Entry(
			outcome: AuditOutcome.Failure,
			failureReason: "=1+1");

		// Act
		using var workbook = Render(entry);

		// Assert
		var cell = workbook.Worksheets.First().Cell(2, CauseColumn);

		cell.HasFormula.Should().BeFalse();
		cell.GetString().Should().Be("=1+1");
	}

	[Fact]
	public void Write_ShouldFreezeTheHeaderAndEnableAutoFilter()
	{
		// Arrange: the two features that make this a workbook rather than a CSV.
		var entry = Entry();

		// Act
		using var workbook = Render(entry);

		// Assert
		var sheet = workbook.Worksheets.First();

		sheet.SheetView.SplitRow.Should().Be(1);
		sheet.AutoFilter.IsEnabled.Should().BeTrue();
	}

	[Fact]
	public void Write_ShouldProduceAValidWorkbook_WhenThereAreNoRows()
	{
		// Arrange: a filter that matches nothing still has to open in Excel rather than
		// producing a corrupt file.
		using var workbook = Render();

		// Assert
		var sheet = workbook.Worksheets.First();

		sheet.Cell(1, 1).GetString().Should().Be("When (UTC)");
	}
}
