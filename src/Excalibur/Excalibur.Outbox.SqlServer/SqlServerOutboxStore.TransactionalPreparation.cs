// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Excalibur.Data;
using Excalibur.Dispatch;
using Microsoft.Data.SqlClient;

namespace Excalibur.Outbox.SqlServer;

public sealed partial class SqlServerOutboxStore
{
    object? IServiceProvider.GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(ITransactionalOutboxPreparer) || serviceType == typeof(ITransactionalOutboxPayloadTransformPreparer))
        {
            // Only the generated-metadata constructor supplies the safe encoded-envelope route.
            // Do not advertise a safe facet backed by the legacy reflection metadata delegate.
            return _legacyJsonOptions is null ? new TransactionalPreparation(this) : null;
        }
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    private sealed class TransactionalPreparation(SqlServerOutboxStore owner) : ITransactionalOutboxPreparer, ITransactionalOutboxPayloadTransformPreparer
    {
        public ValueTask<IPreparedTransactionalOutboxWrite> PrepareMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            cancellationToken.ThrowIfCancellationRequested();
            var prepared = owner.FreezePreparedMessage(message);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IPreparedTransactionalOutboxWrite>(prepared);
        }

        [RequiresUnreferencedCode("The owning runtime-message codec may discover arbitrary consumer payload metadata. Use explicit encoded envelopes for the metadata-safe route.")]
        [RequiresDynamicCode("The owning runtime-message codec may require runtime code generation. Use explicit encoded envelopes for the metadata-safe route.")]
        public ValueTask<IPreparedTransactionalOutboxWrite> PrepareEnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            return PrepareMessageAsync(owner.PrepareEnqueueMessage(message, context), cancellationToken);
        }

        public ValueTask<IPreparedTransactionalOutboxWrite> PrepareMessageWithPayloadTransformAsync(OutboundMessage message,
            Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(transformPayload);
            cancellationToken.ThrowIfCancellationRequested();
            // Headers/custom values, body, tables and transport rows freeze before the first await.
            return TransformAsync(owner.FreezePreparedMessage(message), transformPayload, cancellationToken);
        }

        [RequiresUnreferencedCode("The owning runtime-message codec may discover arbitrary consumer payload metadata. Use explicit encoded envelopes for the metadata-safe route.")]
        [RequiresDynamicCode("The owning runtime-message codec may require runtime code generation. Use explicit encoded envelopes for the metadata-safe route.")]
        public ValueTask<IPreparedTransactionalOutboxWrite> PrepareEnqueueWithPayloadTransformAsync(IDispatchMessage message, IMessageContext context,
            Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(transformPayload);
            cancellationToken.ThrowIfCancellationRequested();
            return PrepareMessageWithPayloadTransformAsync(owner.PrepareEnqueueMessage(message, context), transformPayload, cancellationToken);
        }

        private static async ValueTask<IPreparedTransactionalOutboxWrite> TransformAsync(PreparedSqlWrite prepared,
            Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transformPayload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await transformPayload(prepared.BorrowPayload(), prepared.TenantId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The preparation payload transform returned null.");
            cancellationToken.ThrowIfCancellationRequested();
            return prepared.WithPayload((byte[])bytes.Clone());
        }
    }

    private PreparedSqlWrite FreezePreparedMessage(OutboundMessage message)
    {
        var schema = _options.Tables.SchemaName;
        var messageTable = _options.Tables.OutboxTableName;
        var deliveryTable = _options.Tables.TransportsTableName;
        Excalibur.Data.Validation.SqlIdentifierValidator.ThrowIfInvalid(schema, nameof(schema));
        Excalibur.Data.Validation.SqlIdentifierValidator.ThrowIfInvalid(messageTable, nameof(messageTable));
        Excalibur.Data.Validation.SqlIdentifierValidator.ThrowIfInvalid(deliveryTable, nameof(deliveryTable));
        var table = $"[{schema}].[{messageTable}]";
        var transportTable = $"[{schema}].[{deliveryTable}]";
        var timeout = _options.Processing.CommandTimeoutSeconds;
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, 1);
        var converted = _convertMessage(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(converted.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(converted.MessageType);
        var row = converted with
        {
            MessageBody = (byte[])converted.MessageBody.Clone(),
            TenantId = KeyedTenantPartition.FromStoredValue(converted.TenantId).TenantId
        };
        var transports = message.IsMultiTransport ? message.TransportDeliveries.Select(FreezeTransport).ToArray() : [];
        if (transports.Any(delivery => !string.Equals(delivery.MessageId, row.MessageId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("A prepared transport row must reference its exact owning message ID.");
        }
        var backend = CaptureClosedBackend(_connectionFactory());
        return new PreparedSqlWrite(backend.Server, backend.Database, table, transportTable, timeout, row, transports);
    }

    private static (string Server, string Database) CaptureClosedBackend(SqlConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        // A live handout is not transferred ownership. Refuse without closing a caller's session.
        if (connection.State != ConnectionState.Closed)
        {
            throw new InvalidOperationException("Transactional outbox preparation requires a new closed factory connection.");
        }
        using (connection)
        {
            var server = connection.DataSource;
            var database = connection.Database;
            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database))
            {
                throw new InvalidOperationException("Transactional outbox preparation requires explicit logical server and catalog identity.");
            }
            // No credential, token or full connection string enters the canonical representation.
            return (server, database);
        }
    }

    private static OutboundMessageTransport FreezeTransport(OutboundMessageTransport value) => new()
    {
        Id = value.Id, MessageId = value.MessageId, TransportName = value.TransportName, Destination = value.Destination,
        Status = value.Status, CreatedAt = value.CreatedAt, RetryCount = value.RetryCount, TransportMetadata = value.TransportMetadata
    };

    private sealed class PreparedSqlWrite : IPreparedTransactionalOutboxWrite
    {
        private readonly string _server;
        private readonly string _database;
        private readonly string _table;
        private readonly string _transportTable;
        private readonly int _timeout;
        private readonly OutboxMessage _row;
        private readonly OutboundMessageTransport[] _transports;
        private readonly byte[] _canonical;

        internal PreparedSqlWrite(string server, string database, string table, string transportTable, int timeout, OutboxMessage row, OutboundMessageTransport[] transports)
        {
            _server = server; _database = database;
            _table = table; _transportTable = transportTable; _timeout = timeout; _row = row; _transports = transports;
            _canonical = EncodeCanonical();
        }
        public string MessageId => _row.MessageId;
        public string? TenantId => _row.TenantId;
        public byte[] GetCanonicalWireBytes() => (byte[])_canonical.Clone();
        internal ReadOnlyMemory<byte> BorrowPayload() => _row.MessageBody;
        internal PreparedSqlWrite WithPayload(byte[] ownedPayload) => new(_server, _database, _table, _transportTable, _timeout, _row with { MessageBody = ownedPayload }, _transports);

        public async ValueTask StageAsync(IDbTransaction transaction, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            cancellationToken.ThrowIfCancellationRequested();
            if (transaction is not SqlTransaction sqlTransaction || sqlTransaction.Connection is not { State: ConnectionState.Open } connection)
            {
                throw new InvalidOperationException("Prepared SQL outbox staging requires an open owning SqlTransaction.");
            }
            if (!string.Equals(connection.DataSource, _server, StringComparison.Ordinal)
                || !string.Equals(connection.Database, _database, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Prepared SQL outbox transaction differs from its selected logical backend.");
            }
            // These existing requests bind only frozen primitive columns. No connection factory,
            // codec, transform, options read, raw StageMessageAsync, commit or rollback occurs here.
            _ = await connection.ResolveAsync(new Requests.InsertOutboxMessageRequest(_table, _row, sqlTransaction, _timeout, cancellationToken)).ConfigureAwait(false);
            foreach (var delivery in _transports)
            {
                _ = await connection.ResolveAsync(new Requests.InsertTransportDeliveryRequest(_transportTable, delivery, TenantId, sqlTransaction, _timeout, cancellationToken)).ConfigureAwait(false);
            }
        }

        private byte[] EncodeCanonical()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            // Versioned canonical typed SQL values, NOT a claim to capture network TDS bytes.
            Text("SqlServerOutboxPrepared/v2/logical-backend"); Text(_server); Text(_database); Text(_table); Text(_transportTable); writer.Write(_timeout);
            Text(_row.MessageId); Text(_row.MessageType); Bytes(_row.MessageBody); Text(_row.MessageMetadata);
            Text(_row.Destination); Date(_row.CreatedAt); NullableDate(_row.ScheduledAt);
            writer.Write((int)OutboxStatus.Staged); writer.Write(0);
            Text(_row.CorrelationId); Text(_row.CausationId); Text(_row.TenantId); writer.Write(_row.Priority);
            Text(_row.TargetTransports); writer.Write(_row.IsMultiTransport); Text(_row.PartitionKey); Text(_row.GroupKey); writer.Write(_row.SequenceNumber);
            writer.Write(_transports.Length);
            foreach (var delivery in _transports)
            {
                Text(delivery.Id); Text(delivery.MessageId); Text(delivery.TransportName); Text(delivery.Destination);
                writer.Write((int)delivery.Status); Date(delivery.CreatedAt); writer.Write(delivery.RetryCount); Text(delivery.TransportMetadata); Text(TenantId);
            }
            writer.Flush(); return stream.ToArray();
            void Text(string? value)
            {
                writer.Write(value is not null);
                if (value is null) { return; }
                // Raw UTF16 code units preserve every SQL Unicode value without replacement collisions.
                writer.Write(value.Length); foreach (var character in value) { writer.Write((ushort)character); }
            }
            void Bytes(byte[] value) { writer.Write(value.Length); writer.Write(value); }
            void Date(DateTimeOffset value) { writer.Write(value.Ticks); writer.Write(value.Offset.Ticks); }
            void NullableDate(DateTimeOffset? value) { writer.Write(value.HasValue); if (value.HasValue) { Date(value.Value); } }
        }
    }
}
