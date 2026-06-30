// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Boundary.Tests;

/// <summary>
/// Curated source of truth for the "advertised implementation-selecting strategy" meta-guard (ADR-336 clause 2).
/// </summary>
/// <remarks>
/// <para>
/// A framework that advertises an enum value (a public strategy/mode an app can configure) makes a promise: selecting
/// that value does something. The meta-guard enforces that promise structurally — every value of every
/// implementation-selecting strategy enum must be <em>covered</em>, where covered means one of:
/// </para>
/// <list type="number">
/// <item><b>(a) Implementor</b> — a live implementor resolves for the value (a real code path consumes it).</item>
/// <item><b>(a') DocumentedFallback</b> — the value auto-derives a real implementor from the present infrastructure
/// (safe by construction; it cannot select something that is absent).</item>
/// <item><b>(b) FailLoudGuard</b> — a <c>ValidateOnStart</c>/<c>IValidateOptions</c>/<c>IHostedService</c> guard
/// rejects the value at startup when its required implementor/infrastructure is absent (advertised-or-fail-loud).</item>
/// <item><b>(c) Allowlist</b> — a named dev-only allowlist entry with a written reason (the reviewed escape hatch).
/// There are intentionally <em>zero</em> allowlist entries today — an unwired value is a reviewed entry, never a
/// silent omission.</item>
/// </list>
/// <para>
/// This registry is the declarative input to the arch-test guard (<c>AdvertisedStrategyWiredOrFailLoudShould</c>),
/// which loads the framework assemblies by reflection (this project carries no project references — it reasons over
/// built assemblies), enumerates <c>Enum.GetValues&lt;T&gt;()</c> for each <see cref="Included"/> enum, and asserts
/// each value has a coverage entry whose <see cref="StrategyValueCoverage.EvidenceTypeFullName"/> actually exists in
/// the loaded assemblies (NFR-2 non-vacuity: removing the guard/implementor type makes the guard go RED, naming the
/// uncovered <c>(enum, value)</c>).
/// </para>
/// <para>
/// <b>Curation rule (SoftwareArchitect, ADR-336 GUIDE):</b> an enum is <see cref="Included"/> iff one of its values
/// <em>selects an implementor or required infrastructure</em>; it is <see cref="Excluded"/> (with a written reason)
/// when every value merely <em>parameterizes</em> a single existing code path (pure shape/data/behavior — no
/// implementor selection). Consumer-defined custom values are out of scope (the guard asserts framework-shipped
/// values only — EC-1), and an implementor that lives in an unreferenced optional package is covered-by-fail-loud,
/// not a false RED (EC-2).
/// </para>
/// </remarks>
internal static class AdvertisedStrategyRegistry
{
	/// <summary>How a single advertised enum value is covered.</summary>
	public enum CoverageKind
	{
		/// <summary>(a) A registered implementor resolves for / consumes this value.</summary>
		Implementor,

		/// <summary>(a') The value auto-derives a real implementor from present infrastructure (safe by construction).</summary>
		DocumentedFallback,

		/// <summary>(b) A startup guard rejects this value when its implementor/infrastructure is absent.</summary>
		FailLoudGuard,

		/// <summary>(c) A named dev-only allowlist entry with a written reason (none today).</summary>
		Allowlist,
	}

	/// <summary>Coverage classification for one value of an advertised strategy enum.</summary>
	/// <param name="ValueName">The enum member name (e.g. <c>Transactional</c>).</param>
	/// <param name="Value">The underlying numeric value.</param>
	/// <param name="Kind">How the value is covered.</param>
	/// <param name="EvidenceTypeFullName">
	/// The full type name the arch-test reflectively confirms exists (and, for guards, is wired): the guard type for
	/// <see cref="CoverageKind.FailLoudGuard"/>, or a representative implementor/consumer for
	/// <see cref="CoverageKind.Implementor"/>/<see cref="CoverageKind.DocumentedFallback"/>. Empty for
	/// <see cref="CoverageKind.Allowlist"/>.
	/// </param>
	/// <param name="EvidenceAssemblyName">The assembly that contains <paramref name="EvidenceTypeFullName"/>.</param>
	/// <param name="Reason">Human-readable justification for the classification.</param>
	public sealed record StrategyValueCoverage(
		string ValueName,
		long Value,
		CoverageKind Kind,
		string EvidenceTypeFullName,
		string EvidenceAssemblyName,
		string Reason);

	/// <summary>An advertised implementation-selecting strategy enum and the coverage for each of its values.</summary>
	/// <param name="EnumFullName">Fully-qualified enum type name.</param>
	/// <param name="AssemblyName">The assembly that declares the enum.</param>
	/// <param name="Coverage">Coverage for every framework-shipped value of the enum.</param>
	public sealed record AdvertisedStrategyEnum(
		string EnumFullName,
		string AssemblyName,
		IReadOnlyList<StrategyValueCoverage> Coverage);

	/// <summary>An enum considered but deliberately excluded, with the written reason it selects no implementor.</summary>
	/// <param name="EnumFullName">Fully-qualified enum type name.</param>
	/// <param name="AssemblyName">The assembly that declares the enum.</param>
	/// <param name="Reason">Why the enum is excluded (pure shape/data/behavior — no implementor selection).</param>
	public sealed record ExcludedStrategyEnum(string EnumFullName, string AssemblyName, string Reason);

	// ---- Evidence type/assembly constants (single source so the strings stay consistent) ----------------------------

	private const string EsAsm = "Excalibur.EventSourcing";
	private const string DispatchAsm = "Excalibur.Dispatch";
	private const string OutboxAsm = "Excalibur.Outbox";
	private const string KafkaAsm = "Excalibur.Dispatch.Transport.Kafka";

	/// <summary>
	/// The implementation-selecting strategy enums the meta-guard enforces (curation rule: a value SELECTS an
	/// implementor/infrastructure). Note: <c>CatchUpStrategy</c> was an include candidate but its entire feature
	/// cluster (advertised-yet-never-wired) was removed in this wave, so it no longer exists and carries no entry.
	/// </summary>
	public static IReadOnlyList<AdvertisedStrategyEnum> Included { get; } =
	[
		new AdvertisedStrategyEnum(
			"Excalibur.Dispatch.OutboxStagingStrategy",
			"Excalibur.Dispatch.Abstractions",
			[
				new StrategyValueCoverage(
					"Auto", 0, CoverageKind.DocumentedFallback,
					"Excalibur.EventSourcing.Implementation.EventSourcedRepository", EsAsm,
					"Auto picks Transactional/EventuallyConsistent/Deferred from the registered infrastructure at runtime "
					+ "(ResolveEffectiveStagingStrategy) — it can only resolve to a strategy whose infrastructure is present, "
					+ "so it is safe by construction."),
				new StrategyValueCoverage(
					"Transactional", 1, CoverageKind.FailLoudGuard,
					"Excalibur.EventSourcing.DependencyInjection.TransactionalStagingCapabilityValidator", EsAsm,
					"Explicit Transactional without an ITransactionalOutboxWriter + transactional event store fails fast at "
					+ "startup (instead of silently degrading to non-atomic staging)."),
				new StrategyValueCoverage(
					"EventuallyConsistent", 2, CoverageKind.FailLoudGuard,
					"Excalibur.EventSourcing.DependencyInjection.EventSourcedRepositoryStagingCapabilityValidator", EsAsm,
					"Explicit EventuallyConsistent without a registered IOutboxStore fails fast at startup (GAP-1 fix-in-band; "
					+ "previously the repository silently skipped staging and lost integration events)."),
				new StrategyValueCoverage(
					"Deferred", 3, CoverageKind.Implementor,
					"Excalibur.EventSourcing.Implementation.EventSourcedRepository", EsAsm,
					"Deferred stages nothing during save (a background service processes events later) — it requires no "
					+ "save-time infrastructure, so the repository's deferred path always satisfies it."),
			]),

		new AdvertisedStrategyEnum(
			"Excalibur.Dispatch.Outbox.OutboxConsistencyMode",
			"Excalibur.Dispatch.Abstractions",
			[
				new StrategyValueCoverage(
					"EventuallyConsistent", 0, CoverageKind.Implementor,
					"Excalibur.Dispatch.Middleware.Outbox.DeferredOutboxWriter", DispatchAsm,
					"The default buffered/after-handler staging path is implemented by DeferredOutboxWriter."),
				new StrategyValueCoverage(
					"Transactional", 1, CoverageKind.FailLoudGuard,
					"Excalibur.Dispatch.Options.Middleware.OutboxStagingOptionsValidator", DispatchAsm,
					"Explicit Transactional consistency is validated at startup for its required transactional outbox "
					+ "infrastructure (TransactionMiddleware + IOutboxStore)."),
			]),

		new AdvertisedStrategyEnum(
			"Excalibur.Outbox.Partitioning.OutboxPartitionStrategy",
			"Excalibur.Outbox",
			[
				new StrategyValueCoverage(
					"None", 0, CoverageKind.Implementor,
					"Microsoft.Extensions.DependencyInjection.PartitionedOutboxBuilderExtensions", OutboxAsm,
					"Single-table (non-partitioned) default path; handled explicitly by the partitioned-outbox wiring."),
				new StrategyValueCoverage(
					"PerShard", 1, CoverageKind.Implementor,
					"Microsoft.Extensions.DependencyInjection.PartitionedOutboxBuilderExtensions", OutboxAsm,
					"Selects the per-tenant-shard partition resolver; OutboxPartitionOptionsValidator additionally validates "
					+ "its required ShardIds configuration."),
				new StrategyValueCoverage(
					"ByTenantHash", 2, CoverageKind.Implementor,
					"Microsoft.Extensions.DependencyInjection.PartitionedOutboxBuilderExtensions", OutboxAsm,
					"Selects the hash(tenantId) % N partition resolver."),
			]),

		new AdvertisedStrategyEnum(
			"Excalibur.EventSourcing.ProjectionMode",
			"Excalibur.EventSourcing.Abstractions",
			[
				new StrategyValueCoverage(
					"Async", 0, CoverageKind.Implementor,
					"Excalibur.EventSourcing.Projections.AsyncProjectionProcessingHost", EsAsm,
					"Async projections are driven by the AsyncProjectionProcessingHost (GetByMode(Async))."),
				new StrategyValueCoverage(
					"Inline", 1, CoverageKind.Implementor,
					"Excalibur.EventSourcing.Projections.InlineProjectionProcessor", EsAsm,
					"Inline projections are driven by the InlineProjectionProcessor during SaveAsync (GetByMode(Inline))."),
				new StrategyValueCoverage(
					"Ephemeral", 2, CoverageKind.Implementor,
					"Excalibur.EventSourcing.Projections.ProjectionBuilder", EsAsm,
					"Ephemeral (on-demand replay, no persistence) is a real builder-selectable projection mode."),
			]),

		new AdvertisedStrategyEnum(
			"Excalibur.Dispatch.Transport.RetryStrategy",
			"Excalibur.Dispatch.Abstractions",
			[
				new StrategyValueCoverage(
					"FixedDelay", 0, CoverageKind.Implementor,
					"Excalibur.Dispatch.Resilience.FixedBackoffCalculator", DispatchAsm,
					"BackoffCalculatorFactory.Create maps FixedDelay to a FixedBackoffCalculator (IBackoffCalculator)."),
				new StrategyValueCoverage(
					"ExponentialBackoff", 1, CoverageKind.Implementor,
					"Excalibur.Dispatch.Resilience.ExponentialBackoffCalculator", DispatchAsm,
					"BackoffCalculatorFactory.Create maps ExponentialBackoff to an ExponentialBackoffCalculator; the factory's "
					+ "default arm throws ArgumentOutOfRangeException (fail-loud), so an unmapped value cannot silently no-op."),
			]),

		new AdvertisedStrategyEnum(
			"Excalibur.Dispatch.Transport.Kafka.SubjectNameStrategy",
			"Excalibur.Dispatch.Transport.Kafka",
			[
				new StrategyValueCoverage(
					"TopicName", 0, CoverageKind.Implementor,
					"Excalibur.Dispatch.Transport.Kafka.TopicNameStrategy", KafkaAsm,
					"SubjectNameStrategyExtensions.ToStrategy maps TopicName to a TopicNameStrategy (ISubjectNameStrategy)."),
				new StrategyValueCoverage(
					"RecordName", 1, CoverageKind.Implementor,
					"Excalibur.Dispatch.Transport.Kafka.RecordNameStrategy", KafkaAsm,
					"ToStrategy maps RecordName to a RecordNameStrategy (ISubjectNameStrategy)."),
				new StrategyValueCoverage(
					"TopicRecordName", 2, CoverageKind.Implementor,
					"Excalibur.Dispatch.Transport.Kafka.TopicRecordNameStrategy", KafkaAsm,
					"ToStrategy maps TopicRecordName to a TopicRecordNameStrategy; the default arm throws (fail-loud)."),
			]),
	];

	/// <summary>
	/// Enums considered for inclusion but deliberately excluded — every value parameterizes a single existing code
	/// path (pure shape/data/behavior) and selects no implementor or infrastructure. Documented so each exclusion is
	/// a reviewed decision, never a silent omission.
	/// </summary>
	public static IReadOnlyList<ExcludedStrategyEnum> Excluded { get; } =
	[
		new ExcludedStrategyEnum(
			"Excalibur.EventSourcing.NotificationFailurePolicy", "Excalibur.EventSourcing.Abstractions",
			"Failure-handling policy: parameterizes how the single notification path reacts to a failure (e.g. "
			+ "ignore/throw/log); selects no implementor or infrastructure."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.AwsSqs.SqsRetryStrategy", "Excalibur.Dispatch.Transport.AwsSqs",
			"Retry shape/behavior within one resilience path (back-off timing); does not select an implementor. (Any "
			+ "inertness of the retry feature is tracked separately, not by this guard.)"),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.Kafka.CompatibilityMode", "Excalibur.Dispatch.Transport.Kafka",
			"Schema-registry compatibility setting forwarded to the SR client as configuration; parameterizes a call, "
			+ "selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Cdc.StalePositionRecoveryStrategy", "Excalibur.Cdc",
			"Recovery behavior policy within the single CDC position-recovery path; selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Cdc.CosmosDb.CosmosDbCdcMode", "Excalibur.Cdc.CosmosDb",
			"Mode within one CDC provider; parameterizes that provider's change-feed path rather than selecting a "
			+ "separate implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Compliance.EncryptionMode", "Excalibur.Compliance.Abstractions",
			"Encryption-mode shape parameterizing the encryption path; selects no implementor/infrastructure."),
		new ExcludedStrategyEnum(
			"Excalibur.Compliance.LazyMigrationMode", "Excalibur.Compliance.Abstractions",
			"Lazy key-migration behavior mode; parameterizes the single migration path."),
		new ExcludedStrategyEnum(
			"Excalibur.Compliance.ReplicationMode", "Excalibur.Compliance.Abstractions",
			"Multi-region replication mode; parameterizes replication behavior, selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Compliance.FailoverStrategy", "Excalibur.Compliance.Abstractions",
			"Multi-region failover policy; parameterizes failover behavior, selects no implementor."),

		// ---- F2 (ogxxk5) exhaustiveness classification: every public *Strategy enum the auto-discovery guard finds ----
		// SA-curated (run->read->cite via traced consumption sites). Each parameterizes a single existing code path
		// (formula/string/bool/DI-key) and selects NO implementor — so each is correctly Excluded from the meta-guard.
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Compliance.FailoverStrategy", "Excalibur.Dispatch.Compliance.Abstractions",
			"Multi-region failover policy; parameterizes failover behavior, selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Data.ElasticSearch.Projections.MigrationStrategy", "Excalibur.Data.ElasticSearch",
			"SchemaEvolutionHandler if/is branches within one migration handler; selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.DeduplicationStrategy", "Excalibur.Dispatch.Abstractions",
			"Parameterizes the dedup hash-key option only; IDeduplicationStrategy implementations are DI-selected "
			+ "independently, not chosen by this enum."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.FailureHandlingStrategy", "Excalibur.Dispatch.Abstractions",
			"Connection-pool failure-handling shape set on an options object; no consuming switch/factory selects an "
			+ "implementor (inertness tracked by the S862 inert-advertised-options audit)."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.MessageIdStrategy", "Excalibur.Dispatch.Abstractions",
			"IdempotentHandlerMiddleware switch computes an id string; the Custom value defers to a DI IMessageIdProvider, "
			+ "the enum itself selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Observability.Sampling.SamplingStrategy", "Excalibur.Dispatch.Observability",
			"TraceSampler switch returns a bool sampling decision; selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Pooling.Configuration.ResetStrategy", "Excalibur.Dispatch",
			"Pool object-reset shape set on an options object; no consuming switch/factory selects an implementor "
			+ "(inertness tracked by the S862 inert-advertised-options audit)."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Resilience.BackoffStrategy", "Excalibur.Dispatch",
			"RetryPolicy switch picks a delay formula (Constant/Linear/Exponential); selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Resilience.Polly.JitterStrategy", "Excalibur.Dispatch.Resilience.Polly",
			"RetryPolicy switch returns a jitter TimeSpan helper; selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Serialization.PreWarmStrategy", "Excalibur.Dispatch",
			"Utf8JsonWriterPool switch picks which pool to pre-warm; selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.Azure.PartitionKeyStrategy", "Excalibur.Dispatch.Transport.AzureServiceBus",
			"AzureEventHubsCloudEventAdapter switch returns a partition-key string; selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.BatchCompletionStrategy", "Excalibur.Dispatch.Transport.Abstractions",
			"Batch-completion shape set on an options object; no consuming switch/factory selects an implementor "
			+ "(inertness tracked by the S862 inert-advertised-options audit)."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.DeadLetterStrategy", "Excalibur.Dispatch.Transport.Abstractions",
			"Generic dead-letter shape set on an options object; no consuming switch/factory selects an implementor "
			+ "(inertness tracked by the S862 inert-advertised-options audit)."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.ErrorHandlingStrategy", "Excalibur.Dispatch.Transport.Abstractions",
			"Transport error-handling shape set on an options object; no consuming switch/factory selects an implementor "
			+ "(inertness tracked by the S862 inert-advertised-options audit)."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.Google.BatchAckStrategy", "Excalibur.Dispatch.Transport.GooglePubSub",
			"Batch-ack shape set on an options object; no consuming switch/factory selects an implementor "
			+ "(inertness tracked by the S862 inert-advertised-options audit)."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.Kafka.KafkaPartitioningStrategy", "Excalibur.Dispatch.Transport.Kafka",
			"KafkaCloudEventAdapter switch returns a partition-key string; selects no implementor."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.RabbitMQ.QuorumDeadLetterStrategy", "Excalibur.Dispatch.Transport.RabbitMQ",
			"Quorum-queue dead-letter shape set on an options object; no consuming switch/factory selects an implementor "
			+ "(inertness tracked by the S862 inert-advertised-options audit)."),
		new ExcludedStrategyEnum(
			"Excalibur.Dispatch.Transport.RabbitMQ.RabbitMqRoutingStrategy", "Excalibur.Dispatch.Transport.RabbitMQ",
			"RabbitMQ routing shape set on an options object; no consuming switch/factory selects an implementor "
			+ "(inertness tracked by the S862 inert-advertised-options audit)."),
	];
}
