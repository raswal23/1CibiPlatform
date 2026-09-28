using ATS.Shared;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The splitting and normalising half of <see cref="EmailCopyList"/>. The validation half is
/// covered through the commands in <c>EmailProcessValidationTests</c>, which is where it is
/// actually reached from.
/// </summary>
public class EmailCopyListTests
{
	[Theory]
	[InlineData("a@x.com", new[] { "a@x.com" })]
	[InlineData("a@x.com,b@x.com", new[] { "a@x.com", "b@x.com" })]
	// Trimmed even though Normalize never writes spaces: the column is hand-editable, and a
	// leading space makes the fragment unparseable to MimeKit - which throws for the whole
	// notice rather than for the one bad address.
	[InlineData(" a@x.com , b@x.com ", new[] { "a@x.com", "b@x.com" })]
	[InlineData("a@x.com,,b@x.com", new[] { "a@x.com", "b@x.com" })]
	public void Split_ShouldTrimEntriesAndDropBlanks(string copyList, string[] expected)
	{
		EmailCopyList.Split(copyList).Should().Equal(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(",")]
	[InlineData(" , , ")]
	public void Split_ShouldReturnNothing_WhenTheListHoldsNoAddresses(string? copyList)
	{
		EmailCopyList.Split(copyList).Should().BeEmpty();
	}

	[Theory]
	[InlineData(" a@x.com , b@x.com ", "a@x.com,b@x.com")]
	[InlineData("a@x.com,,b@x.com", "a@x.com,b@x.com")]
	[InlineData("a@x.com", "a@x.com")]
	public void Normalize_ShouldProduceABareCommaSeparatedList(string copyList, string expected)
	{
		EmailCopyList.Normalize(copyList).Should().Be(expected);
	}

	// "Nobody is copied on this notice" is stored as the empty string, never null - the column
	// is NOT NULL so a reader never has to treat the two as the same thing.
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	[InlineData(", ,")]
	public void Normalize_ShouldProduceTheEmptyString_WhenNobodyIsCopied(string? copyList)
	{
		EmailCopyList.Normalize(copyList).Should().BeEmpty();
	}

	[Fact]
	public void Validate_ShouldAcceptAnEmptyList()
	{
		// Whether an empty list may ALSO be active is the command's rule, not this one's.
		EmailCopyList.Validate(string.Empty).Should().BeNull();
	}

	// Only the spacing pushes this past the column width, and the spacing is dropped on write,
	// so the stored value fits and the list is accepted.
	//
	// The window is one character per address wide: normalising turns ", " into ",", so the
	// submitted form is only ever (count - 1) longer than the stored one. 25 addresses of 39
	// characters each - the index is zero-padded to keep every entry the same width - land the
	// two forms at 1023 and 999. The two assertions below are the arrangement, not the subject:
	// they fail loudly if a change to MaxLength ever moves the fixture off that window.
	[Fact]
	public void Validate_ShouldMeasureTheNormalisedList_NotTheSubmittedOne()
	{
		var addresses = Enumerable.Range(0, 25)
			.Select(index => $"recipient{index:D2}@somewhatlongerdomain.com.ph")
			.ToArray();

		var spaced = string.Join(", ", addresses);
		var normalized = string.Join(',', addresses);

		spaced.Length.Should().BeGreaterThan(EmailCopyList.MaxLength);
		normalized.Length.Should().BeLessThanOrEqualTo(EmailCopyList.MaxLength);

		EmailCopyList.Validate(spaced).Should().BeNull();
	}
}
