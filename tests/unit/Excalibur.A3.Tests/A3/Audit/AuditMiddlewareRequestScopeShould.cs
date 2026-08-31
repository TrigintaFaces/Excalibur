// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Events;
using Excalibur.Application.Requests;
using Excalibur.Domain;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Audit;

/// <summary>
/// Binds the property that makes <see cref="AuditMiddleware"/> safe to share: one instance, many
/// callers, many tenants.
/// </summary>
/// <remarks>
/// <para>
/// A middleware instance is built once and lives for the process. <c>DispatchMiddlewareInvoker</c>
/// materialises the whole set with a single pass over <c>IEnumerable&lt;IDispatchMiddleware&gt;</c>
/// against the root provider in its constructor, and the invoker itself is a singleton, so the array is
/// built once no matter what lifetime a middleware's descriptor declares. Anything a middleware holds in
/// a field is therefore held for every message the process ever handles.
/// </para>
/// <para>
/// These tests drive one instance across two request scopes carrying two different tenants, and assert
/// on the tenant recorded in the emitted audit record. They fail if the middleware ever goes back to
/// holding the activity context: every record after the first would carry the first caller's tenant.
/// </para>
/// <para>
/// They also bind the ruling for an auditable action dispatched with no request scope — a background
/// worker, a console host, a serverless entry point. Such an action is neither denied nor silently
/// unaudited: it is recorded, against the ambient tenant when one is established and against the
/// untenanted partition when none is, because an absent tenant is a value here and not a gap.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Audit")]
public sealed class AuditMiddlewareRequestScopeShould : IDisposable
{
	private readonly RecordingAuditPublisher _publisher = new();
	private readonly ServiceProvider _serviceProvider;
	private readonly AuditMiddleware _sut;

	public AuditMiddlewareRequestScopeShould()
	{
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
		_ = services.AddTenantContext();
		_ = services.AddSingleton<IAuditMessagePublisher>(_publisher);
		_ = services.AddExcaliburAudit();

		_serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
			ValidateOnBuild = true,
		});

		// Built once, from the root, exactly as the invoker builds it.
		_sut = new AuditMiddleware(
			_publisher,
			A.Fake<IOutboxDispatcher>(),
			_serviceProvider.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<AuditMiddleware>.Instance);
	}

	public void Dispose() => _serviceProvider.Dispose();

	[Fact]
	public async Task RecordEachRequestAgainstItsOwnTenant()
	{
		// Arrange & Act - one middleware instance, two request scopes, two tenants.
		await DispatchInNewScopeAsync("tenant-a");
		await DispatchInNewScopeAsync("tenant-b");

		// Assert - the second record must not carry the first caller's tenant.
		_publisher.RecordedTenants.ShouldBe(["tenant-a", "tenant-b"]);
	}

	[Fact]
	public async Task RecordAnActionDispatchedWithoutARequestScopeAgainstTheAmbientTenant()
	{
		// Arrange - the root provider is not a request scope, which is what a worker or console host
		// hands the dispatcher. The tenant is still established for the operation.
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var reached = false;

		// Act
		using (TenantContextHolder.BeginScope("worker-tenant"))
		{
			_ = await _sut.InvokeAsync(
				new AuditableProbeMessage(),
				context,
				(_, _, _) =>
				{
					reached = true;
					return ValueTask.FromResult(A.Fake<IMessageResult>());
				},
				CancellationToken.None);
		}

		// Assert - the action runs and is audited against the tenant it actually ran under.
		reached.ShouldBeTrue();
		_publisher.RecordedTenants.ShouldBe(["worker-tenant"]);
	}

	[Fact]
	public async Task RecordAnActionWithNoTenantAgainstTheUntenantedPartition()
	{
		// Arrange - no request scope and no ambient tenant: the untenanted case.
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(_serviceProvider);
		var reached = false;

		// Act
		_ = await _sut.InvokeAsync(
			new AuditableProbeMessage(),
			context,
			(_, _, _) =>
			{
				reached = true;
				return ValueTask.FromResult(A.Fake<IMessageResult>());
			},
			CancellationToken.None);

		// Assert - untenanted is a value, so the record carries the sentinel rather than being dropped.
		reached.ShouldBeTrue();
		_publisher.RecordedTenants.ShouldBe([TenantDefaults.DefaultTenantId]);
	}

	private async Task DispatchInNewScopeAsync(string tenantId)
	{
		using var tenantScope = TenantContextHolder.BeginScope(tenantId);
		using var scope = _serviceProvider.CreateScope();

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(scope.ServiceProvider);

		_ = await _sut.InvokeAsync(
			new AuditableProbeMessage(),
			context,
			(_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>()),
			CancellationToken.None);
	}

	private sealed class AuditableProbeMessage : IDispatchMessage, IAmAuditable
	{
		public Guid MessageId { get; } = Guid.NewGuid();
	}

	private sealed class RecordingAuditPublisher : IAuditMessagePublisher
	{
		public List<string?> RecordedTenants { get; } = [];

		public Task PublishAsync<TMessage>(TMessage message, IActivityContext context, CancellationToken cancellationToken)
		{
			if (message is ActivityAudited audited)
			{
				RecordedTenants.Add(audited.TenantId);
			}

			return Task.CompletedTask;
		}
	}
}
