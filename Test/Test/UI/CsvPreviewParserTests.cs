using FluentAssertions;
using FrontendWebassembly.SharedService;

namespace Test.UI;

/// <summary>
/// The bulk-upload preview has to agree with what CsvHelper parses server-side, or the
/// operator approves an import that does not match what they were shown.
/// </summary>
public class CsvPreviewParserTests
{
	private const string Header = "LastName,FirstName,MiddleInitial,EmailAddress,MobileNumber";

	[Fact]
	public void Parse_ShouldReadHeadersAndRows()
	{
		var csv = $"{Header}\nDela Cruz,Juan,S,juan@example.com,09171234567";

		var result = CsvPreviewParser.Parse(csv);

		result.Headers.Should().Equal(
			"LastName", "FirstName", "MiddleInitial", "EmailAddress", "MobileNumber");
		result.Rows.Should().ContainSingle();
		result.Rows[0].Should().Equal(
			"Dela Cruz", "Juan", "S", "juan@example.com", "09171234567");
	}

	[Fact]
	public void Parse_ShouldKeepCommasInsideQuotedFields()
	{
		// The bug this parser replaced: the hand-rolled split turned this into six
		// misaligned columns, so the preview did not match the import.
		var csv = $"{Header}\n\"Dela Cruz, Jr.\",Juan,S,juan@example.com,09171234567";

		var result = CsvPreviewParser.Parse(csv);

		result.Rows.Should().ContainSingle();
		result.Rows[0].Should().HaveCount(5);
		result.Rows[0][0].Should().Be("Dela Cruz, Jr.");
		result.Rows[0][1].Should().Be("Juan");
	}

	[Fact]
	public void Parse_ShouldUnescapeDoubledQuotes()
	{
		var csv = $"{Header}\n\"He said \"\"hi\"\"\",Juan,S,juan@example.com,09171234567";

		var result = CsvPreviewParser.Parse(csv);

		result.Rows[0][0].Should().Be("He said \"hi\"");
	}

	[Fact]
	public void Parse_ShouldKeepNewlinesInsideQuotedFields()
	{
		var csv = $"{Header}\n\"Line one\nLine two\",Juan,S,juan@example.com,09171234567";

		var result = CsvPreviewParser.Parse(csv);

		// One record, not two - the newline is inside the quotes.
		result.Rows.Should().ContainSingle();
		result.Rows[0][0].Should().Be("Line one\nLine two");
	}

	[Fact]
	public void Parse_ShouldHandleWindowsLineEndings()
	{
		var csv = $"{Header}\r\nDela Cruz,Juan,S,juan@example.com,09171234567\r\n";

		var result = CsvPreviewParser.Parse(csv);

		result.Rows.Should().ContainSingle();
		result.Rows[0][0].Should().Be("Dela Cruz");
		result.Rows[0][4].Should().Be("09171234567");
	}

	[Fact]
	public void Parse_ShouldIgnoreBlankRows()
	{
		var csv = $"{Header}\nDela Cruz,Juan,S,juan@example.com,09171234567\n\n,,,,\n";

		var result = CsvPreviewParser.Parse(csv);

		result.Rows.Should().ContainSingle();
		result.TotalRowCount.Should().Be(1);
	}

	[Fact]
	public void Parse_ShouldReturnEmptyForBlankInput()
	{
		CsvPreviewParser.Parse(string.Empty).Rows.Should().BeEmpty();
		CsvPreviewParser.Parse("   ").Rows.Should().BeEmpty();
	}

	[Fact]
	public void Parse_ShouldReportHeadersOnlyFileAsEmpty()
	{
		var result = CsvPreviewParser.Parse(Header);

		result.Headers.Should().HaveCount(5);
		result.Rows.Should().BeEmpty();
		result.TotalRowCount.Should().Be(0);
		result.IsTruncated.Should().BeFalse();
	}

	[Fact]
	public void Parse_ShouldCapPreviewRowsButStillReportTheTotal()
	{
		// A large upload must not build a cell list for every row just to show a dialog,
		// but the operator still needs to know the preview is a sample.
		var rows = Enumerable.Range(0, CsvPreviewParser.MaxPreviewRows + 50)
			.Select(index => $"Last{index},First{index},M,user{index}@example.com,09171234567");
		var csv = $"{Header}\n{string.Join("\n", rows)}";

		var result = CsvPreviewParser.Parse(csv);

		result.Rows.Should().HaveCount(CsvPreviewParser.MaxPreviewRows);
		result.TotalRowCount.Should().Be(CsvPreviewParser.MaxPreviewRows + 50);
		result.IsTruncated.Should().BeTrue();
	}

	[Fact]
	public void Parse_ShouldTrimUnquotedWhitespace()
	{
		var csv = $"{Header}\n  Dela Cruz , Juan ,S,juan@example.com,09171234567";

		var result = CsvPreviewParser.Parse(csv);

		result.Rows[0][0].Should().Be("Dela Cruz");
		result.Rows[0][1].Should().Be("Juan");
	}
}
