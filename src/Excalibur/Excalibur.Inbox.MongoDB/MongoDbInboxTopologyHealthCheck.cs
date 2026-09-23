// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Inbox.MongoDB;

/// <summary>
/// Reports whether the configured MongoDB deployment can actually honour a multi-document transaction,
/// which <see cref="MongoDbInboxOptions.EnableTransactions"/> asks it to.
/// </summary>
/// <remarks>
/// <para>
/// MongoDB multi-document transactions require a replica set or a sharded cluster; a standalone
/// <c>mongod</c> rejects <c>StartTransaction()</c> outright. Answering that requires a real round trip,
/// which is why this is a HEALTH CHECK and not options validation.
/// </para>
/// <para>
/// <b>The distinction this type exists to make.</b> Options validation answers whether the configuration is
/// coherent -- a pure function of the configuration, no network -- and a failure there is permanent and
/// rightly prevents startup. Reachability is a different question with a different answer over time: a
/// database that is merely not accepting connections YET is the normal case under an orchestrator that does
/// not strictly order the database ahead of the application. Deciding it inside options validation turned
/// "not up yet" into "your configuration is invalid" and refused to start the host at all, where previously
/// the application started and retried into a working connection. A container that starts and reports
/// not-ready is correct; one that refuses to start because a peer is slow is not.
/// </para>
/// <para>
/// Both failure shapes report <see cref="HealthStatus.Unhealthy"/>, because in both the inbox cannot serve
/// transactional work and a readiness probe should keep traffic away. They are NOT the same condition to an
/// operator, though, so the verdict is carried in the result data under <c>topology.verdict</c>:
/// <c>misconfigured</c> never resolves without a deployment change, while <c>unreachable</c> is expected to
/// clear on its own. A check reporting one status and one sentence for both would tell an operator to page
/// for a condition that fixes itself, or to wait out one that never will.
/// </para>
/// <para>
/// Skipped -- reported healthy -- when transactions are off, or when the connection is builder-managed: the
/// consumer supplied the client directly and owns its topology, and there is no connection string here to
/// probe with.
/// </para>
/// </remarks>
internal sealed class MongoDbInboxTopologyHealthCheck : IHealthCheck
{
	private const string VerdictKey = "topology.verdict";
	private const string VerdictMisconfigured = "misconfigured";
	private const string VerdictUnreachable = "unreachable";

	private readonly IOptionsMonitor<MongoDbInboxOptions> _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbInboxTopologyHealthCheck"/> class.
	/// </summary>
	/// <param name="options"> The monitored inbox options. </param>
	public MongoDbInboxTopologyHealthCheck(IOptionsMonitor<MongoDbInboxOptions> options) =>
		_options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		var options = _options.CurrentValue;

		if (!options.EnableTransactions)
		{
			return HealthCheckResult.Healthy(
				$"{nameof(MongoDbInboxOptions.EnableTransactions)} is false, so no transaction-capable "
				+ "topology is required.");
		}

		if (string.Equals(
				options.ConnectionString,
				InboxBuilderMongoDbExtensions.BuilderManagedConnectionSentinel,
				StringComparison.Ordinal))
		{
			return HealthCheckResult.Healthy(
				"The MongoDB client was supplied directly, so its deployment topology is the consumer's to "
				+ "guarantee and there is no connection string to probe.");
		}

		try
		{
			var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(options.ServerSelectionTimeoutSeconds);
			settings.ConnectTimeout = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds);
			if (options.UseSsl)
			{
				settings.UseTls = true;
			}

			// Owned for the length of one probe, and disposed at the end of it. The health-check service
			// builds a fresh instance from the registration factory on every run and never disposes what
			// that factory constructed, so a client cached in a field would be rebuilt on every probe
			// anyway -- and its connection pool leaked on every probe.
			using var client = new MongoClient(settings);

			// The same round trip the driver itself makes on first use, run asynchronously and under the
			// caller's token so a slow or hanging server cannot pin the health-check thread.
			var hello = await client
				.GetDatabase("admin")
				.RunCommandAsync<BsonDocument>(
					new BsonDocument("hello", 1),
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			var isReplicaSet = hello.Contains("setName");
			var isSharded = hello.TryGetValue("msg", out var msg) && msg.IsString && msg.AsString == "isdbgrid";

			if (isReplicaSet || isSharded)
			{
				return HealthCheckResult.Healthy(
					"The MongoDB deployment is a replica set or sharded cluster and can honour multi-document "
					+ "transactions.");
			}

			// A DEFINITE answer, and the wrong one. This never becomes healthy without redeploying the
			// server or turning transactions off, so it is reported as the permanent verdict it is.
			return new HealthCheckResult(
				HealthStatus.Unhealthy,
				$"{nameof(MongoDbInboxOptions)}.{nameof(MongoDbInboxOptions.EnableTransactions)} is true, but "
				+ $"the MongoDB deployment at the configured {nameof(MongoDbInboxOptions.ConnectionString)} is "
				+ "standalone (no replica set or sharded cluster). MongoDB multi-document transactions require "
				+ "a replica set or sharded cluster -- a standalone mongod cannot start one. Deploy the target "
				+ $"as a replica set, or set {nameof(MongoDbInboxOptions.EnableTransactions)} to false and rely "
				+ "on the at-least-once claim protocol instead. This will not clear on its own.",
				data: new Dictionary<string, object> { [VerdictKey] = VerdictMisconfigured });
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// NO answer, rather than a wrong one. The topology may be perfectly correct and simply not
			// reachable yet, so this is transient by default and must not be read as a configuration fault.
			return new HealthCheckResult(
				HealthStatus.Unhealthy,
				"The MongoDB deployment topology could not be read yet: could not reach MongoDB at the "
				+ $"configured {nameof(MongoDbInboxOptions.ConnectionString)} to confirm it is a replica set "
				+ "or sharded cluster. This is expected while the server is still starting and is expected to "
				+ "clear without any change to configuration.",
				exception: ex,
				data: new Dictionary<string, object> { [VerdictKey] = VerdictUnreachable });
		}
	}
}
