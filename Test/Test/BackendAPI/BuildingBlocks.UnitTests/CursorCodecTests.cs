// Not Test.BackendAPI.BuildingBlocks.*: a "BuildingBlocks" segment under Test.BackendAPI
// would shadow the root BuildingBlocks namespace for every test in the project.
namespace Test.BackendAPI.BuildingBlocksUnitTests;

using BuildingBlocks.Pagination;
using FluentAssertions;

public class CursorCodecTests
{
	[Fact]
	public void Encode_Decode_RoundTripsAllFieldShapes()
	{
		var guid = Guid.NewGuid().ToString("D");
		var timestamp = DateTime.UtcNow.ToString("O");

		var cursor = CursorCodec.Encode(guid, timestamp, "42", "plain string");

		var fields = CursorCodec.Decode(cursor, 4);
		fields.Should().Equal(guid, timestamp, "42", "plain string");
	}

	[Fact]
	public void Encode_NullField_CollapsesToEmptyString()
	{
		var cursor = CursorCodec.Encode(null, "");

		var fields = CursorCodec.Decode(cursor, 2);
		fields.Should().Equal(string.Empty, string.Empty);
	}

	[Fact]
	public void RoundTrips_UnicodeCharactersInStrings()
	{
		var tricky = "Dela Cruz O'Brien \"„Ünïcode” 日本語";

		var cursor = CursorCodec.Encode(tricky);

		var fields = CursorCodec.Decode(cursor, 1);
		fields.Should().Equal(tricky);
	}

	[Fact]
	public void Decode_ReturnsNull_WhenFieldContainsDelimiter()
	{
		// A '|' inside a field shifts the field count; the cursor degrades to first page.
		var cursor = CursorCodec.Encode("Dela Cruz | O'Brien");

		CursorCodec.Decode(cursor, 1).Should().BeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not-base64!!!")]
	public void Decode_ReturnsNull_OnMissingOrMalformedCursor(string? cursor)
	{
		CursorCodec.Decode(cursor, 1).Should().BeNull();
	}

	[Fact]
	public void Decode_ReturnsNull_OnWrongFieldCount()
	{
		var cursor = CursorCodec.Encode("a", "b");

		CursorCodec.Decode(cursor, 1).Should().BeNull();
		CursorCodec.Decode(cursor, 3).Should().BeNull();
	}
}
