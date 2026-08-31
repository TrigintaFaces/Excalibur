// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Locks the RBAC audit stores onto the caller whose action they are recording.
/// Both stores are registered with a singleton lifetime and both took the role and actor providers as
/// constructor arguments, so the container resolved those once from the root and the instance answered
/// with one caller's identity for the life of the process. Every meta-audit entry -- the record of who
/// read or annotated the audit trail, a segregation-of-duties control -- then named that caller, and the
/// role check, which is an access-control decision, decided on that caller's role.
///
/// The providers are registered scoped here because that is the lifetime the package's own
/// AddAuditRoleProvider registers, and because scoped is what makes the capture a defect: under scope
/// validation, which is the ASP.NET Core development default, the documented wiring did not start at all.
/// Their values come from ambient state, which is the shape the package documents (claims, an
/// IHttpContextAccessor): that state flows with the call, so a scope the store opens for the operation
/// still sees the caller who is acting.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RbacStoreCallerIdentityShould
{
	private static readonly AsyncLocal<string?> AmbientActor = new();

	[Fact]
	public async Task NameEachCallerOnTheAnnotationStoreMetaAudit()
	{
		var sink = new RecordingAuditLogger();
		using var provider = BuildProvider(sink, static s => s.AddAuditAnnotations());

		await ActAsAsync(provider, "alice@example.com", TagAsync);
		await ActAsAsync(provider, "bob@example.com", TagAsync);

		sink.ActorIds.ShouldBe(["alice@example.com", "bob@example.com"]);
	}

	[Fact]
	public async Task NameEachCallerOnTheAuditStoreMetaAudit()
	{
		var sink = new RecordingAuditLogger();
		using var provider = BuildProvider(sink, static s =>
		{
			_ = s.AddAuditLogging();
			_ = s.AddRbacAuditStore();
		});

		await ActAsAsync(provider, "alice@example.com", ReadAsync);
		await ActAsAsync(provider, "bob@example.com", ReadAsync);

		sink.ActorIds.ShouldBe(["alice@example.com", "bob@example.com"]);
	}

	[Fact]
	public void StartTheDocumentedWiringUnderScopeValidation()
	{
		// The entry points the package documents, wired the way it documents them, with the role provider
		// registered scoped as its own AddAuditRoleProvider registers it. Scope validation is the ASP.NET
		// Core development default; before the stores resolved per operation this combination could not
		// start, because a singleton decorator was consuming a scoped access-control input.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddScoped<AmbientCaller>();
		_ = services.AddScoped<IAuditActorProvider>(static sp => sp.GetRequiredService<AmbientCaller>());
		_ = services.AddScoped<IAuditRoleProvider>(static sp => sp.GetRequiredService<AmbientCaller>());
		_ = services.AddAuditLogging();
		_ = services.AddRbacAuditStore();
		_ = services.AddAuditAnnotations();

		using var provider = services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateScopes = true });
		using var scope = provider.CreateScope();

		_ = Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IAuditStore>());
		_ = Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IAuditAnnotationStore>());
	}

	private static Task TagAsync(IServiceProvider scoped) =>
		scoped.GetRequiredService<IAuditAnnotationStore>()
			.TagAsync("evt-1", ["tag"], CancellationToken.None);

	private static Task ReadAsync(IServiceProvider scoped) =>
		scoped.GetRequiredService<IAuditStore>()
			.GetByIdAsync("evt-1", CancellationToken.None);

	/// <summary>Runs one operation as one caller, in that caller's own request scope.</summary>
	private static async Task ActAsAsync(
		IServiceProvider provider,
		string actor,
		Func<IServiceProvider, Task> operation)
	{
		AmbientActor.Value = actor;

		await using var scope = provider.CreateAsyncScope();
		await operation(scope.ServiceProvider);
	}

	private static ServiceProvider BuildProvider(
		RecordingAuditLogger sink,
		Action<IServiceCollection> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddScoped<AmbientCaller>();
		_ = services.AddScoped<IAuditActorProvider>(static sp => sp.GetRequiredService<AmbientCaller>());
		_ = services.AddScoped<IAuditRoleProvider>(static sp => sp.GetRequiredService<AmbientCaller>());

		configure(services);

		// Registered after the entry point so it supersedes the audit logger the package wires, letting the
		// meta-audit entries be observed. The stores resolve IAuditLogger at call time.
		_ = services.AddScoped<IAuditLogger>(_ => sink);

		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
	}

	/// <summary>
	/// A per-request identity backed by ambient state: exactly the shape the package documents, and
	/// registered with the scoped lifetime the package's own registration helper uses.
	/// </summary>
	private sealed class AmbientCaller : IAuditActorProvider, IAuditRoleProvider
	{
		public Task<string> GetCurrentActorIdAsync(CancellationToken cancellationToken) =>
			Task.FromResult(AmbientActor.Value ?? "unset");

		public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken) =>
			Task.FromResult(AuditLogRole.Administrator);
	}

	private sealed class RecordingAuditLogger : IAuditLogger
	{
		private readonly ConcurrentQueue<string?> _actorIds = new();

		public IReadOnlyList<string?> ActorIds => [.. _actorIds];

		public Task<AuditEventId> LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
		{
			_actorIds.Enqueue(auditEvent.ActorId);
			return Task.FromResult(new AuditEventId
			{
				EventId = auditEvent.EventId,
				EventHash = string.Empty,
				SequenceNumber = 0,
				RecordedAt = auditEvent.Timestamp,
			});
		}

		public Task<AuditIntegrityResult> VerifyIntegrityAsync(
			DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
			Task.FromResult(AuditIntegrityResult.NoEventsInScope(from, to));
	}
}
