using FluentAssertions;
using FrontendWebassembly.SharedService;

namespace Test.UI;

/// <summary>
/// The copy list is stored as ONE comma-separated string, so the chip input has to agree with the
/// server about what that string means - the delimiter, what counts as a duplicate, and which form
/// the length caps are measured against. These mirror <c>ATS.UnitTests.EmailCopyListTests</c>,
/// which pins the same rules on the backend side; the two must not drift, because the chip input is
/// the operator's early warning and <c>EmailCopyList.Validate</c> is the authority.
/// </summary>
public class EmailCopyListDraftTests
{
	#region Split

	[Fact]
	public void Split_ShouldTrimAndDropBlanks()
	{
		// The column is hand-editable and the send path hands every fragment to
		// MimeKit.MailboxAddress.Parse, which throws for the whole notice on a leading space.
		EmailCopyListDraft.Split(" a@cibi.com.ph , ,b@cibi.com.ph,")
			.Should().Equal("a@cibi.com.ph", "b@cibi.com.ph");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(",")]
	[InlineData(",,")]
	public void Split_ShouldReturnNothing_WhenNobodyIsCopied(string? copyList)
	{
		EmailCopyListDraft.Split(copyList).Should().BeEmpty();
	}

	#endregion

	#region Normalize

	[Fact]
	public void Normalize_ShouldJoinWithABareComma()
	{
		// Spacing is normalised away rather than rejected: the operator typed a list that means
		// exactly what they intended, and the spaces are not what gets stored.
		EmailCopyListDraft.Normalize("a@cibi.com.ph, b@cibi.com.ph")
			.Should().Be("a@cibi.com.ph,b@cibi.com.ph");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("   ")]
	public void Normalize_ShouldReturnEmpty_WhenNobodyIsCopied(string? copyList)
	{
		// The column is NOT NULL, so "nobody is copied" is the empty string and never null - a
		// reader never has to treat the two as the same thing.
		EmailCopyListDraft.Normalize(copyList).Should().BeEmpty();
	}

	#endregion

	#region Contains

	[Fact]
	public void Contains_ShouldMatchCaseInsensitively()
	{
		// "A@x.com" and "a@x.com" are one mailbox to every provider. Keeping both would copy that
		// person twice and charge the sending account twice against its daily cap, and the unique
		// index cannot see it - the whole list is one distinct string.
		EmailCopyListDraft.Contains(["clientsupport@cibi.com.ph"], "ClientSupport@CIBI.com.ph")
			.Should().BeTrue();
	}

	[Fact]
	public void Contains_ShouldReturnFalse_ForAnAddressThatIsNotThere()
	{
		EmailCopyListDraft.Contains(["a@cibi.com.ph"], "b@cibi.com.ph").Should().BeFalse();
	}

	#endregion

	#region NormalizedLength

	[Fact]
	public void NormalizedLength_ShouldMeasureTheStoredForm_NotTheSubmittedOne()
	{
		// The cap applies to what gets written, so a list that is only over the limit because of
		// the spaces around its commas is accepted - the server measures it the same way.
		//
		// 39 characters is the only width that lands in the gap: 25 of them are 999 stored and
		// 1023 as submitted with ", ". One character shorter and neither is over the cap; one
		// longer and both are, so the test would pass without proving anything.
		var addresses = Enumerable.Range(0, 25)
			.Select(index => $"recipient{index:D2}@somelongerdomainname.com.ph")
			.ToArray();

		addresses.Should().OnlyContain(address => address.Length == 39);

		var submitted = string.Join(", ", addresses);

		submitted.Length.Should().BeGreaterThan(EmailCopyListDraft.MaxLength);
		EmailCopyListDraft.NormalizedLength(addresses)
			.Should().BeLessThanOrEqualTo(EmailCopyListDraft.MaxLength)
			.And.Be(string.Join(',', addresses).Length);
	}

	[Fact]
	public void NormalizedLength_ShouldBeZero_WhenNobodyIsCopied()
	{
		EmailCopyListDraft.NormalizedLength([]).Should().Be(0);
	}

	#endregion

	#region Caps

	// Duplicated on purpose across the two projects, and the reason they have to stay equal: they
	// are the varchar width and the per-address bound on the same column. If one side moves and the
	// other does not, the chip input either lets through a list the server will refuse, or refuses
	// a list the server would have taken.
	[Fact]
	public void Caps_ShouldMatchTheBackendColumn()
	{
		EmailCopyListDraft.MaxLength.Should().Be(1000);
		EmailCopyListDraft.MaxAddressLength.Should().Be(255);
		EmailCopyListDraft.Delimiter.Should().Be(',');
	}

	#endregion
}
