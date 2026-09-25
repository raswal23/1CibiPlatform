using ATS.Services.Settings.EmailProcessManagement;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

/// <summary>
/// A stubbed <see cref="IEmailProcessManagementService"/> for the four suites that used to read a
/// copy list from a constant.
/// </summary>
/// <remarks>
/// Shared rather than repeated per suite because all four want the same two things - "this process
/// is copied to these addresses" and "this process resolves to nothing" - and a Moq setup written
/// four times drifts four ways.
///
/// Only <c>GetCopyListAsync</c> is stubbed. The rest of that interface is the console's write side,
/// which no notice calls; a send path reaching for one would fail this stub's strictness rather
/// than quietly getting a default, which is the behaviour to want.
///
/// What it deliberately does NOT do is reproduce the real read's behaviour. That method's whole job
/// is the failure path: a missing row, an inactive row, a database that will not answer. Faking
/// those here would test the fake. <c>EmailProcessCopyListTests</c> drives the real one against a
/// mocked repository; these suites only need it to hand back a list so the assertion can be about
/// what the notice does with one.
/// </remarks>
public static class EmailCopyListFixture
{
	/// <summary>
	/// A resolver answering <paramref name="addresses"/> for <paramref name="emailProcess"/> and an
	/// empty list for anything else.
	/// </summary>
	/// <remarks>
	/// The catch-all matters: it is what a notice asking for the WRONG process looks like. A stub
	/// that answered every process identically would let a send path read the reminder's row for an
	/// invitation and no test would notice.
	/// </remarks>
	public static Mock<IEmailProcessManagementService> Returning(
		string emailProcess,
		params string[] addresses)
	{
		var resolver = new Mock<IEmailProcessManagementService>();

		resolver
			.Setup(copyList => copyList.GetCopyListAsync(
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<string>)[]);

		return resolver.AlsoReturning(emailProcess, addresses);
	}

	/// <summary>
	/// Registers a second process on an existing stub, for the one send path that reads two rows.
	/// </summary>
	public static Mock<IEmailProcessManagementService> AlsoReturning(
		this Mock<IEmailProcessManagementService> resolver,
		string emailProcess,
		params string[] addresses)
	{
		resolver
			.Setup(copyList => copyList.GetCopyListAsync(
				emailProcess,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<string>)addresses);

		return resolver;
	}
}
