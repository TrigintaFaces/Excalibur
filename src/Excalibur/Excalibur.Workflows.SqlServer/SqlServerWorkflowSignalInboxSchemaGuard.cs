// Copyright (c) Excalibur contributors. All rights reserved.

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Workflows.SqlServer;

/// <summary>
/// Verifies at startup that the signal-inbox table carries a UNIQUE constraint over
/// (InstanceId, SignalId), and refuses to start when it does not.
/// </summary>
/// <remarks>
/// <para>
/// The uniqueness constraint is the entire idempotency mechanism for signal delivery: the inbox
/// relies on the database rejecting a second row for the same (instance, signal) pair, and treats
/// that rejection as "already delivered". If the table exists without the constraint, nothing
/// fails and nothing logs — duplicate signals are simply accepted and applied more than once. The
/// loss is silent, it is only observable as wrong workflow state much later, and by then the
/// duplicate rows are indistinguishable from legitimate ones.
/// </para>
/// <para>
/// The shipped creation script declares the constraint, so a deployment that ran it is already
/// correct and this check is a no-op. It exists for the deployment that did not: a table created
/// by hand, restored from a backup predating the constraint, or migrated by tooling that dropped
/// it. Those are the cases where the guarantee is absent precisely because nobody noticed.
/// </para>
/// <para>
/// This runs as a startup check rather than an options validation because it asks a question about
/// the database, not about configuration — options validation is expected to be pure, and doing
/// I/O there would make a misconfigured connection string surface as a validation fault.
/// </para>
/// </remarks>
internal sealed partial class SqlServerWorkflowSignalInboxSchemaGuard : IHostedService
{
	private readonly SqlServerWorkflowSignalInboxOptions _options;
	private readonly ILogger<SqlServerWorkflowSignalInboxSchemaGuard> _logger;

	public SqlServerWorkflowSignalInboxSchemaGuard(
		IOptions<SqlServerWorkflowSignalInboxOptions> options,
		ILogger<SqlServerWorkflowSignalInboxSchemaGuard> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		// Identifiers are validated as safe SQL identifiers by the options validator before they reach
		// here; they are still passed as parameters below rather than interpolated, so this query cannot
		// carry an injection even if that validation were relaxed.
		const string ConstraintQuery = """
			SELECT COUNT(*)
			FROM sys.indexes AS i
			INNER JOIN sys.index_columns AS ic
			    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
			INNER JOIN sys.columns AS c
			    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
			WHERE i.object_id = OBJECT_ID(@QualifiedTable)
			  AND i.is_unique = 1
			  AND c.name IN (N'InstanceId', N'SignalId')
			GROUP BY i.index_id
			HAVING COUNT(DISTINCT c.name) = 2
			   AND COUNT(*) = 2;
			""";

		var qualifiedTable = $"[{_options.SchemaName}].[{_options.TableName}]";

		await using var connection = new SqlConnection(_options.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// A unique index over EXACTLY these two columns, in either order. A unique index over a SUPERSET
		// would not give the guarantee — (InstanceId, SignalId, ReceivedAt) permits the duplicate this
		// check exists to prevent — so the column count is asserted, not just the presence of both.
		var matching = await connection.QueryFirstOrDefaultAsync<int?>(
			new CommandDefinition(
				ConstraintQuery,
				new { QualifiedTable = qualifiedTable },
				commandTimeout: _options.CommandTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (matching is > 0)
		{
			LogConstraintVerified(qualifiedTable);
			return;
		}

		throw new InvalidOperationException(
			$"The workflow signal inbox table '{qualifiedTable}' has no UNIQUE constraint over "
			+ "(InstanceId, SignalId). That constraint is how duplicate signal delivery is rejected, so "
			+ "without it a redelivered signal is applied to the workflow more than once and nothing "
			+ "reports an error. Apply the shipped signal-inbox creation script, or add a unique index "
			+ "over exactly those two columns, before starting the host.");
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	// No explicit EventId: the Workflows packages have no allocated range in the event-id strategy, and
	// picking an arbitrary number risks colliding with a range reserved for another subsystem.
	[LoggerMessage(
		Level = LogLevel.Debug,
		Message = "Workflow signal inbox table {QualifiedTable} carries the required UNIQUE (InstanceId, SignalId) constraint.")]
	private partial void LogConstraintVerified(string qualifiedTable);
}
