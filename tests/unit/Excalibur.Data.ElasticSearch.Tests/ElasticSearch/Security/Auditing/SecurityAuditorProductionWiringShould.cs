// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;

using Excalibur.AuditLogging;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using Tests.Shared.Infrastructure;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

/// <summary>
/// The production-wiring half of the drain-flush locks: that the auditor a consumer actually gets from
/// <c>AddSecurityAuditing()</c> is the one whose never-drop behaviour is proven elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// The deterministic never-drop and never-deadlock arms construct the auditor directly, through the
/// constructor that accepts an <see cref="ISecurityAuditStore"/>. That proves the auditor behaves. It does
/// not prove that production wiring hands anyone that auditor, and those are different claims: a component
/// can be correct in isolation and unreachable from the composition root.
/// </para>
/// <para>
/// So these arms resolve through a real <see cref="ServiceProvider"/> built by the public registration
/// entry point, and assert what the resolved instance DOES rather than that a type was registered.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Security")]
public sealed class SecurityAuditorProductionWiringShould
{
	/// <summary>
	/// The signing key the fail-closed integrity probe requires before the audit options will bind at
	/// all. Init-only, so it is bound from configuration rather than set through Configure.
	/// </summary>
	private static IConfiguration SigningKeyConfig() =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(
				[new KeyValuePair<string, string?>("SigningKey", Convert.ToBase64String(new byte[32]))])
			.Build();

	private static ServiceProvider BuildProductionProvider(FakeTimeProvider time, ISecurityAuditStore store)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Registered BEFORE the production entry point on purpose: every registration inside it is
		// TryAdd, so a value supplied first is the one that survives. That is the seam a substitution
		// would use, and whether it is honoured is what these arms measure.
		_ = services.AddSingleton<TimeProvider>(time);
		_ = services.AddSingleton(store);

		// Nothing is listening on that port. A short timeout and no retries so that any call which does
		// reach the client returns promptly -- otherwise disposal waits on the default multi-minute
		// timeout and the arm looks hung rather than failing.
		_ = services.AddSingleton(new ElasticsearchClient(
			new ElasticsearchClientSettings(new Uri("http://localhost:9200"))
				.RequestTimeout(TimeSpan.FromMilliseconds(250))
				.MaximumRetries(0)));

		_ = services.AddSecurityAuditing();
		_ = services.Configure<AuditIntegrityOptions>(SigningKeyConfig());

		return services.BuildServiceProvider();
	}

	[Fact]
	public void ResolveTheAuditor_FromTheProductionRegistrationEntryPoint()
	{
		// The weakest of the two, and it exists so a failure in the arm below can be attributed: if
		// resolution itself is broken, that is a different defect than the store not being honoured.
		using var provider = BuildProductionProvider(new FakeTimeProvider(), new RecordingAuditStore());

		_ = Should.NotThrow(
			() => provider.GetRequiredService<SecurityAuditor>(),
			"AddSecurityAuditing() must be able to produce the auditor it registers; a registration that "
			+ "cannot be resolved is advertised and unwired.");
	}

	[Fact]
	public async Task FlushThroughTheStoreSuppliedToTheContainer_NotOneItDerivedForItself()
	{
		// THE ARM THIS CLASS EXISTS FOR. The never-drop locks substitute the flush target through the
		// internal constructor. If production wiring instead derives its own store from the Elasticsearch
		// client, those locks prove a property of an object no consumer can obtain, and nothing stands
		// behind the auditor that actually runs.
		//
		// Asserted by OBSERVING A FLUSH through the supplied store, never by asking the container what it
		// would resolve -- a registration check passes against an auditor that ignores it.
		var time = new FakeTimeProvider();
		var store = new RecordingAuditStore();
		using var provider = BuildProductionProvider(time, store);

		var auditor = provider.GetRequiredService<SecurityAuditor>();
		await using var sut = auditor.ConfigureAwait(false);

		await auditor.AuditSecurityActivityAsync(
			new SecurityActivityEvent
			{
				UserId = "production-path",
				ActivityType = "read",
				Timestamp = time.GetUtcNow()
			},
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		time.Advance(TimeSpan.FromSeconds(60));

		await WaitUntilAsync(() => store.FlushAttempts > 0).ConfigureAwait(false);

		store.Appended.Select(static e => e.UserId).ShouldContain(
			"production-path",
			"the record must reach the store supplied to the container. If it does not, the auditor built "
			+ "its own flush target and the substitutable seam is not reachable through the production "
			+ "registration.");
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		// Bounds a scheduling delay only -- the SCHEDULE is driven by FakeTimeProvider, so this never
		// waits for wall-clock time to pass, only for an already-triggered continuation to run. It
		// FAILS rather than hangs, so a drain that never reaches the store is a red test.
		var observed = await WaitHelpers.WaitUntilAsync(
			condition,
			TimeSpan.FromSeconds(30),
			TimeSpan.FromMilliseconds(10),
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		observed.ShouldBeTrue(
			"the drain loop did not reach the supplied store within the scheduling budget");
	}

	private sealed class RecordingAuditStore : ISecurityAuditStore
	{
		private readonly ConcurrentQueue<SecurityAuditEvent> _appended = new();
		private int _flushAttempts;

		public IReadOnlyList<SecurityAuditEvent> Appended => [.. _appended];

		public int FlushAttempts => Volatile.Read(ref _flushAttempts);

		public Task<bool> EnsureAuditIndexTemplateAsync(CancellationToken cancellationToken) => Task.FromResult(false);

		public Task<AuditBulkAppendResult> BulkAppendEventsAsync(
			IReadOnlyList<SecurityAuditEvent> events,
			CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _flushAttempts);

			foreach (var e in events)
			{
				_appended.Enqueue(e);
			}

			return Task.FromResult(new AuditBulkAppendResult(true, null, events.Count));
		}
	}
}
