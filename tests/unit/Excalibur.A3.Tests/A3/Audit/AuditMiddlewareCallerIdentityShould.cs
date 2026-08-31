// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.A3;
using Excalibur.A3.Authentication;
using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Events;
using Excalibur.Application;
using Excalibur.Application.Requests;
using Excalibur.Domain;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Audit;

/// <summary>
/// Binds the property that makes the audit trail usable: a record names the caller who acted.
/// </summary>
/// <remarks>
/// <para>
/// Both readers of the caller's identity are bound here, because a record and its outbox fallback are
/// two paths to the same evidence and a fix that reaches only one of them is half a fix: the published
/// record's Login, UserId and UserName, and the RaisedBy header written when publishing fails.
/// </para>
/// <para>
/// Two scopes carry two different callers, and each record is asserted against its own caller. That
/// binds both directions at once — an identity that never arrives shows up as "System", and one held
/// across callers shows up as the first caller's name on the second record.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Audit")]
public sealed class AuditMiddlewareCallerIdentityShould : IAsyncDisposable
{
	private readonly RecordingAuditPublisher _publisher = new();
	private readonly List<IOutboxMessage> _outboxed = [];
	private readonly IOutboxDispatcher _outbox = A.Fake<IOutboxDispatcher>();
	private readonly ServiceProvider _serviceProvider;
	private readonly AuditMiddleware _sut;

	public AuditMiddlewareCallerIdentityShould()
	{
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
		_ = services.AddTenantContext();
		_ = services.AddSingleton<IAuditMessagePublisher>(_publisher);
		_ = services.AddExcaliburAudit();

		// The caller's identity is scoped, exactly as the full A3 composition registers it
		// (TryAddScoped<IAccessToken, AccessToken>). The seed is scoped too, so each request scope
		// carries its own caller and nothing ambient decides who acted.
		_ = services.AddScoped<CallerSeed>();
		_ = services.AddScoped<IAccessToken>(static sp => sp.GetRequiredService<CallerSeed>().Build());

		_serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
			ValidateOnBuild = true,
		});

		A.CallTo(() => _outbox.SaveMessagesAsync(A<ICollection<IOutboxMessage>>._, A<CancellationToken>._))
			.Invokes((ICollection<IOutboxMessage> messages, CancellationToken _) => _outboxed.AddRange(messages))
			.Returns(1);

		// Built once, from the root, exactly as the invoker builds it.
		_sut = new AuditMiddleware(
			_publisher,
			_outbox,
			_serviceProvider.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<AuditMiddleware>.Instance);
	}

	// The fake is registered into the container but constructed here, so this class owns it.
	// IOutboxDispatcher is IAsyncDisposable, so the class disposes asynchronously too.
	public async ValueTask DisposeAsync()
	{
		await _outbox.DisposeAsync().ConfigureAwait(false);
		await _serviceProvider.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task NameTheCallerWhoActedOnEachRequest()
	{
		// Arrange & Act - one middleware instance, two request scopes, two callers.
		await DispatchInNewScopeAsync("user-alice", "alice@example.test", "Alice Adams");
		await DispatchInNewScopeAsync("user-bob", "bob@example.test", "Bob Brown");

		// Assert - each record names its own caller. "System" here means the identity never arrived;
		// alice on the second record would mean it was held across callers.
		_publisher.Recorded.Select(static r => r.UserId).ShouldBe(["user-alice", "user-bob"]);
		_publisher.Recorded.Select(static r => r.Login).ShouldBe(["alice@example.test", "bob@example.test"]);
		_publisher.Recorded.Select(static r => r.UserName).ShouldBe(["Alice Adams", "Bob Brown"]);
	}

	[Fact]
	public async Task NameTheCallerInTheOutboxHeaderWhenPublishingFails()
	{
		// Arrange - the publisher is down, so the record takes the outbox fallback path.
		_publisher.FailNextPublish = true;

		// Act
		await DispatchInNewScopeAsync("user-carol", "carol@example.test", "Carol Clark");

		// Assert - the RaisedBy header names the caller instead of reading "Unknown".
		var metadata = _outboxed.ShouldHaveSingleItem().MessageMetadata;
		var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata!)!;
		var raisedBy = JsonSerializer.Deserialize<RaisedBy>(headers[ExcaliburHeaderNames.RaisedBy])!;

		raisedBy.UserId.ShouldBe("user-carol");
	}

	[Fact]
	public async Task RecordAnActionWithNoCallerAsSystem()
	{
		// Arrange - a scope whose caller is anonymous. An anonymous token reports an empty user id
		// rather than null, so this also binds that the empty value never reaches the record.
		using var scope = _serviceProvider.CreateScope();
		scope.ServiceProvider.GetRequiredService<CallerSeed>().Anonymous = true;

		// Act
		await DispatchAsync(scope.ServiceProvider);

		// Assert
		var recorded = _publisher.Recorded.ShouldHaveSingleItem();
		recorded.UserId.ShouldBe("System");
		recorded.UserName.ShouldBe("System");
	}

	private async Task DispatchInNewScopeAsync(string userId, string login, string fullName)
	{
		using var scope = _serviceProvider.CreateScope();

		var seed = scope.ServiceProvider.GetRequiredService<CallerSeed>();
		seed.UserId = userId;
		seed.Login = login;
		seed.FullName = fullName;

		await DispatchAsync(scope.ServiceProvider);
	}

	private async Task DispatchAsync(IServiceProvider scopedServices)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(scopedServices);

		_ = await _sut.InvokeAsync(
			new AuditableProbeMessage(),
			context,
			static (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>()),
			CancellationToken.None);
	}

	private sealed class CallerSeed
	{
		public string UserId { get; set; } = string.Empty;

		public string Login { get; set; } = string.Empty;

		public string FullName { get; set; } = string.Empty;

		public bool Anonymous { get; set; }

		public IAccessToken Build()
		{
			var token = A.Fake<IAccessToken>();

			if (Anonymous)
			{
				// What AccessToken reports for an anonymous caller: empty, not null.
				A.CallTo(() => token.UserId).Returns(string.Empty);
				A.CallTo(() => ((IAuthenticationToken)token).UserId).Returns(null);
				A.CallTo(() => token.FullName).Returns(string.Empty);
				A.CallTo(() => token.Login).Returns(null);
				return token;
			}

			A.CallTo(() => token.UserId).Returns(UserId);

			// IAccessToken hides IAuthenticationToken.UserId, and the outbox RaisedBy header reads the
			// hidden one. AccessToken returns the same value through both, so the fake must too —
			// otherwise the header assertion would fail on the fake rather than on the code.
			A.CallTo(() => ((IAuthenticationToken)token).UserId).Returns(UserId);
			A.CallTo(() => token.Login).Returns(Login);
			A.CallTo(() => token.FullName).Returns(FullName);
			return token;
		}
	}

	private sealed class AuditableProbeMessage : IDispatchMessage, IAmAuditable
	{
		public Guid MessageId { get; } = Guid.NewGuid();
	}

	private sealed class RecordingAuditPublisher : IAuditMessagePublisher
	{
		public List<ActivityAudited> Recorded { get; } = [];

		public bool FailNextPublish { get; set; }

		public Task PublishAsync<TMessage>(TMessage message, IActivityContext context, CancellationToken cancellationToken)
		{
			if (message is ActivityAudited audited)
			{
				Recorded.Add(audited);
			}

			if (FailNextPublish)
			{
				FailNextPublish = false;
				throw new InvalidOperationException("Audit sink unavailable.");
			}

			return Task.CompletedTask;
		}
	}
}
