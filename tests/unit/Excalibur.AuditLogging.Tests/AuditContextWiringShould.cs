// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Binds the wiring that makes <see cref="IAuditContext"/> worth injecting: the middleware that fills it
/// is registered by the same call that registers the context, and it fills the context belonging to the
/// message being handled.
/// </summary>
/// <remarks>
/// <para>
/// A registered-but-uninitialized audit context is worse than an absent one. Every entry a handler
/// records through it carries no correlation id, no tenant and an "unknown" actor, while the
/// registration that produced it documents the opposite — so the trail reads as evidence and identifies
/// nobody. These tests fail if the middleware registration is removed.
/// </para>
/// <para>
/// The second test drives two request scopes and asserts on the correlation id and tenant recorded in
/// each. It fails if the middleware ever holds the context or the actor provider in a field: a
/// middleware instance is materialised once from the root provider and lives for the process, so the
/// second entry would carry the first caller's values.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditContextWiringShould : IDisposable
{
	private readonly List<AuditEvent> _recorded = [];
	private readonly IAuditLogger _auditLogger = A.Fake<IAuditLogger>();
	private readonly ServiceProvider _serviceProvider;

	public AuditContextWiringShould()
	{
		A.CallTo(() => _auditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
			.Invokes((AuditEvent e, CancellationToken _) => _recorded.Add(e))
			.ReturnsLazily((AuditEvent e, CancellationToken _) => new AuditEventId
			{
				EventId = e.EventId,
				EventHash = "hash",
				SequenceNumber = _recorded.Count,
				RecordedAt = e.Timestamp,
			});

		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddSingleton<IAuditLogger>(_auditLogger);

		// The actor provider is consumer-supplied and reads the current user, so a real composition
		// registers it scoped. Registering it that way here is what makes the second test able to fail:
		// a middleware that held it in a field would report the first caller on every entry.
		_ = services.AddScoped<ActorSeed>();
		_ = services.AddScoped<IAuditActorProvider>(static sp => sp.GetRequiredService<ActorSeed>());

		_ = services.AddAuditContext();

		_serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
			ValidateOnBuild = true,
		});
	}

	public void Dispose() => _serviceProvider.Dispose();

	[Fact]
	public void RegisterTheMiddlewareThatFillsTheContext()
	{
		using var scope = _serviceProvider.CreateScope();

		var middleware = scope.ServiceProvider.GetServices<IDispatchMiddleware>();

		middleware.ShouldContain(static m => m.GetType().Name == "AuditContextMiddleware");
	}

	[Fact]
	public async Task FillEachRequestsContextWithItsOwnCallerAndCorrelation()
	{
		// Arrange - one middleware instance, resolved once from the root exactly as the invoker builds it.
		var sut = _serviceProvider.GetServices<IDispatchMiddleware>()
			.Single(static m => m.GetType().Name == "AuditContextMiddleware");

		// Act - two request scopes, two callers, two correlation ids.
		await DispatchInNewScopeAsync(sut, "corr-a", "tenant-a", "actor-a");
		await DispatchInNewScopeAsync(sut, "corr-b", "tenant-b", "actor-b");

		// Assert - each entry carries its own request's values, not the first caller's.
		_recorded.Select(static e => e.CorrelationId).ShouldBe(["corr-a", "corr-b"]);
		_recorded.Select(static e => e.TenantId).ShouldBe(["tenant-a", "tenant-b"]);
		_recorded.Select(static e => e.ActorId).ShouldBe(["actor-a", "actor-b"]);
	}

	[Fact]
	public async Task PassThroughAMessageDispatchedWithoutARequestScope()
	{
		// Arrange - the root provider is not a request scope, which is what a worker or console host
		// hands the dispatcher. Under ValidateScopes, reaching for a scoped context here would throw.
		var sut = _serviceProvider.GetServices<IDispatchMiddleware>()
			.Single(static m => m.GetType().Name == "AuditContextMiddleware");

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var reached = false;

		// Act
		_ = await sut.InvokeAsync(
			new ProbeMessage(),
			context,
			(_, _, _) =>
			{
				reached = true;
				return ValueTask.FromResult(A.Fake<IMessageResult>());
			},
			CancellationToken.None);

		// Assert - the action still runs; the middleware does not throw and does not bind a root instance.
		reached.ShouldBeTrue();
	}

	private async Task DispatchInNewScopeAsync(
		IDispatchMiddleware sut,
		string correlationId,
		string tenantId,
		string actorId)
	{
		using var scope = _serviceProvider.CreateScope();

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(scope.ServiceProvider);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>
		{
			[typeof(IMessageIdentityFeature)] = new MessageIdentityFeature { TenantId = tenantId },
		});

		scope.ServiceProvider.GetRequiredService<ActorSeed>().ActorId = actorId;

		_ = await sut.InvokeAsync(
			new ProbeMessage(),
			context,
			async (_, _, ct) =>
			{
				// The handler's view: it injects IAuditContext from its own scope and records an entry.
				var auditContext = scope.ServiceProvider.GetRequiredService<IAuditContext>();
				_ = await auditContext.ObserveAsync("probe", AuditEventType.Compliance, AuditOutcome.Success, ct);

				return A.Fake<IMessageResult>();
			},
			CancellationToken.None);
	}

	private sealed class ProbeMessage : IDispatchMessage
	{
		public Guid MessageId { get; } = Guid.NewGuid();
	}

	private sealed class ActorSeed : IAuditActorProvider
	{
		public string ActorId { get; set; } = string.Empty;

		public Task<string> GetCurrentActorIdAsync(CancellationToken cancellationToken) => Task.FromResult(ActorId);
	}
}
