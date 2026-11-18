// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Diagnostics;

namespace Excalibur.Integration.Tests.Data.Inbox;

public sealed partial class CommerceOrderSubmissionShould
{
    private sealed class CommitDiagnostics : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly object _gate = new();
        private readonly List<IDisposable> _subscriptions = [];
        private readonly IDisposable _all;
        private readonly TaskCompletionSource<SqlClientTransactionCommitBefore> _before = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<SqlClientTransactionCommitError> _error = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private SqlConnection? _connection;
        private Guid _operation;
        private string _operationName = string.Empty;
        private int _beforeCount;
        private int _errorCount;
        private int _successCount;
        private bool _invalid;
        private Exception? _observerFailure;

        internal CommitDiagnostics() => _all = DiagnosticListener.AllListeners.Subscribe(this);
        internal Task<SqlClientTransactionCommitBefore> Before => _before.Task;
        internal Task<SqlClientTransactionCommitError> Error => _error.Task;

        internal void Bind(SqlConnection connection)
        {
            lock (_gate)
            {
                if (_connection is not null)
                {
                    throw new InvalidOperationException("Commit diagnostics bind one actual connection only.");
                }

                _connection = connection;
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "SqlClientDiagnosticListener")
            {
                lock (_gate)
                {
                    _subscriptions.Add(value.Subscribe(this, static name => name is SqlClientTransactionCommitBefore.Name
                        or SqlClientTransactionCommitAfter.Name or SqlClientTransactionCommitError.Name));
                }
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            lock (_gate)
            {
                switch (value.Value)
                {
                    case SqlClientTransactionCommitBefore before when _connection is not null && ReferenceEquals(before.Connection, _connection):
                        _beforeCount++;
                        _invalid |= before.OperationId == Guid.Empty || _beforeCount != 1
                            || !string.Equals(before.Operation, nameof(SqlTransaction.Commit), StringComparison.Ordinal);
                        _operation = before.OperationId;
                        _operationName = before.Operation;
                        _before.TrySetResult(before);
                        break;
                    case SqlClientTransactionCommitError error when _connection is not null && ReferenceEquals(error.Connection, _connection)
                        && error.OperationId == _operation:
                        _errorCount++;
                        _invalid |= _operation == Guid.Empty || _errorCount != 1
                            || !string.Equals(error.Operation, _operationName, StringComparison.Ordinal);
                        _error.TrySetResult(error);
                        break;
                    case SqlClientTransactionCommitAfter after when _connection is not null && ReferenceEquals(after.Connection, _connection)
                        && after.OperationId == _operation:
                        _successCount++;
                        _invalid |= _operation == Guid.Empty
                            || !string.Equals(after.Operation, _operationName, StringComparison.Ordinal);
                        break;
                }
            }
        }

        internal void AssertPendingCommit()
        {
            lock (_gate)
            {
                _beforeCount.ShouldBe(1);
                _operation.ShouldNotBe(Guid.Empty);
                _operationName.ShouldBe(nameof(SqlTransaction.Commit));
                _errorCount.ShouldBe(0);
                _successCount.ShouldBe(0);
                _invalid.ShouldBeFalse();
                _observerFailure.ShouldBeNull();
            }
        }

        internal void AssertCompleted(bool failed)
        {
            lock (_gate)
            {
                _beforeCount.ShouldBe(1);
                _operation.ShouldNotBe(Guid.Empty);
                _operationName.ShouldBe(nameof(SqlTransaction.Commit));
                _errorCount.ShouldBe(failed ? 1 : 0);
                _successCount.ShouldBe(failed ? 0 : 1);
                _invalid.ShouldBeFalse();
                _observerFailure.ShouldBeNull();
            }
        }

        public void OnError(Exception error)
        {
            lock (_gate)
            {
                _observerFailure = error;
            }
        }

        public void OnCompleted() { }

        public void Dispose()
        {
            _all.Dispose();
            IDisposable[] subscriptions;
            lock (_gate)
            {
                subscriptions = _subscriptions.ToArray();
            }

            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
