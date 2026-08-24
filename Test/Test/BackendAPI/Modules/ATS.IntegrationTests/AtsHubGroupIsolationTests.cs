using System.Security.Claims;
using ATS.Hubs;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

/// <summary>
/// Group isolation for <see cref="ATSHub"/>: a connection must only ever receive the
/// notifications belonging to the user it authenticated as.
/// </summary>
/// <remarks>
/// These exist because the original hub took its group name from
/// <c>Request.Query["userId"]</c>, so connecting with somebody else's GUID delivered
/// their bulk-upload notifications and AI-assistant replies. Nothing caught that: the
/// API host skips <c>MapHub</c> entirely under the "Testing" environment, so the shared
/// IntegrationTestWebAppFactory never exercises a hub.
///
/// So this spins up a minimal host with only the hub and a stub authentication handler.
/// No database, no Testcontainer - the subject under test is the group-assignment rule
/// in OnConnectedAsync, and nothing else needs to be real for that.
/// </remarks>
public class AtsHubGroupIsolationTests : IAsyncLifetime
{
	private const string HubPath = "/hubs/atsbulk";

	/// <summary>The user each connection will authenticate as, set per test.</summary>
	private static readonly AsyncLocal<Guid> AuthenticatedUserId = new();

	private IHost _host = default!;

	public async Task InitializeAsync()
	{
		_host = await new HostBuilder()
			.ConfigureWebHost(webBuilder =>
			{
				webBuilder
					.UseTestServer()
					.ConfigureServices(services =>
					{
						services.AddSignalR();

						// The real pipeline authenticates by JWT cookie. Here the
						// principal is injected directly - these tests are about what
						// the hub does with an identity, not how it was established.
						services.AddAuthentication(StubAuthHandler.SchemeName)
							.AddScheme<StubAuthOptions, StubAuthHandler>(
								StubAuthHandler.SchemeName, _ => { });
						services.AddAuthorization();
					})
					.Configure(app =>
					{
						app.UseRouting();
						app.UseAuthentication();
						app.UseAuthorization();
						app.UseEndpoints(endpoints => endpoints.MapHub<ATSHub>(HubPath));
					});
			})
			.StartAsync();
	}

	public async Task DisposeAsync()
	{
		await _host.StopAsync();
		_host.Dispose();
	}

	[Fact]
	public async Task Connection_ShouldReceiveMessagesForItsOwnUser()
	{
		var userId = Guid.CreateVersion7();

		await using var connection = await ConnectAsAsync(userId);
		var received = CaptureAtsResponses(connection);

		await SendToGroupAsync(userId, "your upload is processing");

		await WaitForAsync(() => received.Count > 0);
		received.Should().ContainSingle().Which.Should().Be("your upload is processing");
	}

	[Fact]
	public async Task Connection_ShouldNotReceiveAnotherUsersMessages()
	{
		// The finding itself. The victim's group is named by their own id; the attacker
		// authenticates as themselves and must not see it.
		var victimUserId = Guid.CreateVersion7();
		var attackerUserId = Guid.CreateVersion7();

		await using var attackerConnection = await ConnectAsAsync(attackerUserId);
		var attackerReceived = CaptureAtsResponses(attackerConnection);

		await using var victimConnection = await ConnectAsAsync(victimUserId);
		var victimReceived = CaptureAtsResponses(victimConnection);

		await SendToGroupAsync(victimUserId, "victim candidate data");

		// Wait on the victim, who should get it - if the attacker were also going to
		// receive it, it would have arrived by now.
		await WaitForAsync(() => victimReceived.Count > 0);

		victimReceived.Should().ContainSingle().Which.Should().Be("victim candidate data");
		attackerReceived.Should().BeEmpty();
	}

	[Fact]
	public async Task Connection_ShouldIgnoreAUserIdSuppliedInTheQueryString()
	{
		// The exact attack the old hub allowed: ?userId=<victim> joined the victim's
		// group. The query string must now have no effect at all.
		var victimUserId = Guid.CreateVersion7();
		var attackerUserId = Guid.CreateVersion7();

		await using var attackerConnection = await ConnectAsAsync(
			attackerUserId,
			queryStringUserId: victimUserId);
		var attackerReceived = CaptureAtsResponses(attackerConnection);

		await using var victimConnection = await ConnectAsAsync(victimUserId);
		var victimReceived = CaptureAtsResponses(victimConnection);

		await SendToGroupAsync(victimUserId, "victim candidate data");

		await WaitForAsync(() => victimReceived.Count > 0);

		victimReceived.Should().ContainSingle();
		attackerReceived.Should().BeEmpty();
	}

	[Fact]
	public async Task Connection_WithNoIdentity_ShouldJoinNoGroupAndReceiveNothing()
	{
		// The hub carries no [Authorize] - the client connects with a bare
		// HubConnectionBuilder that sends no auth cookie cross-origin, so requiring it
		// would 401 every connection. Group assignment is what closes the hole instead:
		// no principal means no group, which means no messages.
		var victimUserId = Guid.CreateVersion7();

		await using var anonymousConnection = await ConnectAsAsync(userId: null);
		var anonymousReceived = CaptureAtsResponses(anonymousConnection);

		await using var victimConnection = await ConnectAsAsync(victimUserId);
		var victimReceived = CaptureAtsResponses(victimConnection);

		await SendToGroupAsync(victimUserId, "victim candidate data");

		await WaitForAsync(() => victimReceived.Count > 0);

		victimReceived.Should().ContainSingle();
		anonymousReceived.Should().BeEmpty();
	}

	[Fact]
	public async Task TwoConnectionsForTheSameUser_ShouldBothJoinThatUsersGroup()
	{
		// A user with the app open in two tabs. Both connections must land in the same
		// group - and, with the group derived from the principal rather than a query
		// parameter, they cannot land anywhere else.
		var userId = Guid.CreateVersion7();

		await using var firstConnection = await ConnectAsAsync(userId);
		var firstReceived = CaptureAtsResponses(firstConnection);

		await using var secondConnection = await ConnectAsAsync(userId);
		var secondReceived = CaptureAtsResponses(secondConnection);

		await SendToGroupAsync(userId, "broadcast to both tabs");

		await WaitForAsync(() => firstReceived.Count > 0 && secondReceived.Count > 0);

		firstReceived.Should().ContainSingle().Which.Should().Be("broadcast to both tabs");
		secondReceived.Should().ContainSingle().Which.Should().Be("broadcast to both tabs");
	}

	// Deliberately not tested here: reconnect-after-stop. Under TestServer with the
	// LongPolling transport, a connection that has been stopped stops group deliveries
	// to *subsequent* connections in the same host - two concurrently live connections
	// work fine, so it is a harness artifact rather than hub behaviour. Asserting
	// through it would test the transport, not ATSHub.

	/// <summary>
	/// Opens a hub connection authenticated as <paramref name="userId"/>. When
	/// <paramref name="queryStringUserId"/> is set it is appended as <c>?userId=</c>,
	/// which the hub must ignore.
	/// </summary>
	private async Task<HubConnection> ConnectAsAsync(Guid? userId, Guid? queryStringUserId = null)
	{
		var url = _host.GetTestServer().BaseAddress + HubPath.TrimStart('/');

		if (queryStringUserId.HasValue)
			url += $"?userId={queryStringUserId.Value}";

		var connection = new HubConnectionBuilder()
			.WithUrl(url, options =>
			{
				options.Transports = HttpTransportType.LongPolling;
				options.HttpMessageHandlerFactory = _ => _host.GetTestServer().CreateHandler();

				// The stub auth handler reads this; the real one reads a JWT cookie.
				if (userId.HasValue)
					options.Headers["X-Test-UserId"] = userId.Value.ToString();
			})
			.Build();

		await connection.StartAsync();
		return connection;
	}

	private static List<string> CaptureAtsResponses(HubConnection connection)
	{
		var received = new List<string>();

		connection.On<string>(
			nameof(IATSClient.ReceiveATSResponse),
			message =>
			{
				lock (received) { received.Add(message); }
			});

		return received;
	}

	private Task SendToGroupAsync(Guid userId, string message) =>
		_host.Services
			.GetRequiredService<IHubContext<ATSHub, IATSClient>>()
			.Clients.Group(userId.ToString())
			.ReceiveATSResponse(message);

	/// <summary>
	/// Polls until <paramref name="condition"/> holds or the timeout elapses. Delivery
	/// is asynchronous, so a bare assert would race; a negative assertion is only made
	/// after a positive one on the same send has already landed.
	/// </summary>
	private static async Task WaitForAsync(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);

		while (DateTime.UtcNow < deadline)
		{
			if (condition())
				return;

			await Task.Delay(25);
		}

		throw new TimeoutException("The expected hub message did not arrive within 5 seconds.");
	}

	private sealed class StubAuthOptions : Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions
	{
	}

	/// <summary>
	/// Authenticates from an <c>X-Test-UserId</c> header, emitting the same
	/// NameIdentifier claim the real JWT pipeline produces. A request without the header
	/// stays anonymous.
	/// </summary>
	private sealed class StubAuthHandler
		: Microsoft.AspNetCore.Authentication.AuthenticationHandler<StubAuthOptions>
	{
		public const string SchemeName = "TestScheme";

		public StubAuthHandler(
			Microsoft.Extensions.Options.IOptionsMonitor<StubAuthOptions> options,
			Microsoft.Extensions.Logging.ILoggerFactory logger,
			System.Text.Encodings.Web.UrlEncoder encoder)
			: base(options, logger, encoder)
		{
		}

		protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
		{
			if (!Request.Headers.TryGetValue("X-Test-UserId", out var rawUserId)
				|| !Guid.TryParse(rawUserId.ToString(), out var userId))
			{
				return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
			}

			var identity = new ClaimsIdentity(
				[new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
				SchemeName);

			var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(
				new ClaimsPrincipal(identity),
				SchemeName);

			return Task.FromResult(
				Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
		}
	}
}
