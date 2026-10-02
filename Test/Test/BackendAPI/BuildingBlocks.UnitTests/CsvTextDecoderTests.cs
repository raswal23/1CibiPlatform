// Not Test.BackendAPI.BuildingBlocks.*: a "BuildingBlocks" segment under Test.BackendAPI
// would shadow the root BuildingBlocks namespace for every test in the project.
namespace Test.BackendAPI.BuildingBlocksUnitTests;

using System.Text;
using BuildingBlocks.Text;
using FluentAssertions;

/// <summary>
/// Bulk-upload ingest must accept what operators actually upload: Excel's ANSI
/// "CSV (Comma delimited)" export (Windows-1252, no BOM) as well as UTF-8 with and
/// without a BOM. Mirrors Test\UI\CsvTextDecoderTests.cs (frontend copy of the decoder).
/// </summary>
public class CsvTextDecoderTests
{
	[Fact]
	public void Decode_PlainAsciiUtf8_RoundTrips()
	{
		var bytes = Encoding.ASCII.GetBytes("LastName,FirstName\nDela Cruz,Juan");

		CsvTextDecoder.Decode(bytes).Should().Be("LastName,FirstName\nDela Cruz,Juan");
	}

	[Fact]
	public void Decode_Utf8WithoutBom_KeepsSpanishCharacters()
	{
		// Valid UTF-8 must not hit the ANSI fallback: 0xC3 0x91 stays Ñ, not "Ã‘".
		var bytes = Encoding.UTF8.GetBytes("Ñuñez,Niño");

		CsvTextDecoder.Decode(bytes).Should().Be("Ñuñez,Niño");
	}

	[Fact]
	public void Decode_Utf8WithBom_StripsBomFromFirstCell()
	{
		// StreamReader stripped the BOM implicitly; Encoding.GetString does not. A
		// leaked U+FEFF on "LastName" would break CsvHelper header matching.
		var bytes = Encoding.UTF8.GetPreamble()
			.Concat(Encoding.UTF8.GetBytes("LastName,FirstName"))
			.ToArray();

		CsvTextDecoder.Decode(bytes).Should().Be("LastName,FirstName");
	}

	[Fact]
	public void Decode_Windows1252Bytes_DecodesEnyeCorrectly()
	{
		// The original bug: Excel ANSI writes Ñ as 0xD1 and ñ as 0xF1 with no BOM,
		// which a UTF-8 StreamReader turned into U+FFFD at insert time.
		byte[] bytes = [0xD1, 0x75, 0xF1, 0x65, 0x7A];

		var result = CsvTextDecoder.Decode(bytes);

		result.Should().Be("Ñuñez");
		result.Should().NotContain("�");
	}

	[Fact]
	public void Decode_Windows1252SmartQuotesAndEuro_Decode()
	{
		// 0x80-0x9F distinguish 1252 from Latin1 (which maps them to C1 controls).
		byte[] bytes = [0x93, 0x94, 0x80];

		CsvTextDecoder.Decode(bytes).Should().Be("“”€");
	}

	[Fact]
	public void Decode_Utf16LeBom_Decodes()
	{
		var bytes = Encoding.Unicode.GetPreamble()
			.Concat(Encoding.Unicode.GetBytes("Ñuñez"))
			.ToArray();

		CsvTextDecoder.Decode(bytes).Should().Be("Ñuñez");
	}

	[Fact]
	public void Decode_Utf16BeBom_Decodes()
	{
		var bytes = Encoding.BigEndianUnicode.GetPreamble()
			.Concat(Encoding.BigEndianUnicode.GetBytes("Ñuñez"))
			.ToArray();

		CsvTextDecoder.Decode(bytes).Should().Be("Ñuñez");
	}

	[Fact]
	public void Decode_EmptyInput_ReturnsEmptyString()
	{
		CsvTextDecoder.Decode(ReadOnlySpan<byte>.Empty).Should().BeEmpty();
	}

	[Fact]
	public async Task DecodeAsync_NonSeekableStream_Decodes()
	{
		// OSS download/IFormFile streams cannot be rewound; the decoder must buffer
		// once rather than re-read the source for the fallback pass. (The seekable
		// MemoryStream in MockObjectStorageService would mask a double-read bug.)
		byte[] bytes = [0xD1, 0x75, 0xF1, 0x65, 0x7A];
		await using var stream = new NonSeekableStream(bytes);

		var result = await CsvTextDecoder.DecodeAsync(stream);

		result.Should().Be("Ñuñez");
	}

	private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
	{
		public override bool CanSeek => false;

		public override long Seek(long offset, SeekOrigin loc) =>
			throw new NotSupportedException();

		public override long Position
		{
			get => base.Position;
			set => throw new NotSupportedException();
		}
	}
}
