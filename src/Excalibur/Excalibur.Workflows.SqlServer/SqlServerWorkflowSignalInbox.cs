// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Excalibur.Workflows.SqlServer;

/// <summary>
/// Durable SQL Server implementation of <see cref="IWorkflowSignalInbox"/>. Admitted signals and their
/// deduplication keys are persisted, so a signal admitted but not yet drained survives a process restart and
/// a producer's redelivery of the same <c>(instanceId, signalId)</c> is still deduplicated after a restart —
/// the property the in-process default cannot provide.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TryEnqueueAsync"/> is a <em>conditional insert</em>: a unique constraint on
/// <c>(InstanceId, SignalId)</c> makes a redelivery a no-op (the unique-violation is caught and reported as
/// "not newly admitted"), never an upsert — matching the contract's idempotence promise for the lifetime of
/// the durable store. <see cref="DrainAsync"/> orders by an <c>IDENTITY</c> arrival sequence, never a
/// wall-clock timestamp, so consumption order is deterministic and reproducible.
/// </para>
/// </remarks>
public sealed class SqlServerWorkflowSignalInbox : IWorkflowSignalInbox
{
    private readonly Func<SqlConnection> _connectionFactory;
    private readonly SqlServerWorkflowSignalInboxOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerWorkflowSignalInbox"/> class.
    /// </summary>
    /// <param name="options">The configured SQL Server signal-inbox options.</param>
    public SqlServerWorkflowSignalInbox(IOptions<SqlServerWorkflowSignalInboxOptions> options)
        : this(CreateConnectionFactory(options?.Value), options!.Value)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerWorkflowSignalInbox"/> class with an explicit
    /// connection factory (used by tests against a real SQL Server instance).
    /// </summary>
    /// <param name="connectionFactory">A factory that creates open-able <see cref="SqlConnection"/> instances.</param>
    /// <param name="options">The configured SQL Server signal-inbox options.</param>
    public SqlServerWorkflowSignalInbox(
        Func<SqlConnection> connectionFactory,
        SqlServerWorkflowSignalInboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(options);

        _connectionFactory = connectionFactory;
        _options = options;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryEnqueueAsync(
        string instanceId,
        string signalId,
        string signalName,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        // Conditional insert: the UNIQUE (InstanceId, SignalId) constraint makes redelivery idempotent. The
        // table name is the only interpolated fragment and comes from trusted options, never caller input;
        // every value is a @-parameter.
#pragma warning disable CA2100 // Table identifier is from trusted configuration, not user input.
        var sql = $"""
            INSERT INTO {_options.QualifiedTableName} (InstanceId, SignalId, SignalName, PayloadJson)
            VALUES (@InstanceId, @SignalId, @SignalName, @PayloadJson)
            """;
#pragma warning restore CA2100

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var command = new CommandDefinition(
                sql,
                new { InstanceId = instanceId, SignalId = signalId, SignalName = signalName, PayloadJson = payloadJson },
                commandTimeout: _options.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);

            _ = await connection.ExecuteAsync(command).ConfigureAwait(false);
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // Unique constraint / index violation: a signal with the same (InstanceId, SignalId) is already
            // admitted. Redelivery is a no-op — report "not newly admitted", never an upsert.
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WorkflowSignalEntry>> DrainAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

#pragma warning disable CA2100 // Table identifier is from trusted configuration, not user input.
        var sql = $"""
            SELECT Sequence, SignalId, SignalName, PayloadJson
            FROM {_options.QualifiedTableName}
            WHERE InstanceId = @InstanceId
            ORDER BY Sequence ASC
            """;
#pragma warning restore CA2100

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition(
            sql,
            new { InstanceId = instanceId },
            commandTimeout: _options.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<WorkflowSignalEntry>(command).ConfigureAwait(false);
        return rows.ToArray();
    }

    private static Func<SqlConnection> CreateConnectionFactory(SqlServerWorkflowSignalInboxOptions? options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return () => new SqlConnection(options.ConnectionString);
    }
}
