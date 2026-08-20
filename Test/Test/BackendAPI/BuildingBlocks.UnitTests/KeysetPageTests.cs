// Not Test.BackendAPI.BuildingBlocks.*: a "BuildingBlocks" segment under Test.BackendAPI
// would shadow the root BuildingBlocks namespace for every test in the project.
namespace Test.BackendAPI.BuildingBlocksUnitTests;

using BuildingBlocks.Pagination;
using FluentAssertions;

public class KeysetPageTests
{
	[Fact]
	public void Trim_EmptyList_NoMore()
	{
		var (items, hasMore) = KeysetPage.Trim(new List<int>(), 10);

		items.Should().BeEmpty();
		hasMore.Should().BeFalse();
	}

	[Fact]
	public void Trim_ExactlyPageSize_NoMore()
	{
		var (items, hasMore) = KeysetPage.Trim([1, 2, 3], 3);

		items.Should().Equal(1, 2, 3);
		hasMore.Should().BeFalse();
	}

	[Fact]
	public void Trim_PageSizePlusOne_RemovesSentinelAndReportsMore()
	{
		var (items, hasMore) = KeysetPage.Trim([1, 2, 3, 4], 3);

		items.Should().Equal(1, 2, 3);
		hasMore.Should().BeTrue();
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(1, 1)]
	[InlineData(50, 50)]
	[InlineData(100, 100)]
	[InlineData(101, 100)]
	public void Clamp_BoundsPageSize(int input, int expected)
	{
		KeysetPage.Clamp(input).Should().Be(expected);
	}
}
