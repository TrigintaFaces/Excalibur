// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Inbox;

public sealed partial class CommerceOrderSubmissionShould
{
    private sealed class AcknowledgementProxy : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly object _gate = new();
        private readonly List<ProxyFlow> _flows = [];
        private readonly List<Task> _tasks = [];
        private readonly string _host;
        private readonly int _port;
        private readonly string _database;
        private readonly Task _accept;

        internal AcknowledgementProxy(string directConnection)
        {
            var builder = new SqlConnectionStringBuilder(directConnection);
            _database = builder.InitialCatalog;
            var source = builder.DataSource;
            if (source.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                source = source[4..];
            }

            var comma = source.LastIndexOf(',');
            if (comma < 1 || !int.TryParse(source.AsSpan(comma + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _port))
            {
                throw new InvalidOperationException("The owned SQL fixture must supply an explicit TCP port.");
            }

            _host = source[..comma];
            _listener.Start();
            builder.DataSource = $"tcp:127.0.0.1,{((IPEndPoint)_listener.LocalEndpoint).Port}";
            builder.Pooling = false;
            builder.MultipleActiveResultSets = false;
            builder.Encrypt = SqlConnectionEncryptOption.Optional;
            ConnectionString = builder.ConnectionString;
            _accept = AcceptAsync();
        }

        internal string ConnectionString { get; }

        internal ProxyFlow Arm(SqlConnection connection, bool hold)
        {
            var expected = new SqlConnectionStringBuilder(ConnectionString);
            if (!string.Equals(connection.DataSource, expected.DataSource, StringComparison.Ordinal)
                || !string.Equals(connection.Database, _database, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The stage callback is not on the selected proxy/database.");
            }

            lock (_gate)
            {
                var active = _flows.Where(flow => flow.Active).ToArray();
                if (active.Length != 1)
                {
                    throw new InvalidOperationException("Arming requires exactly one active owned SQL stream.");
                }

                active[0].Arm(hold);
                return active[0];
            }
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_lifetime.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (_lifetime.IsCancellationRequested)
                        {
                            client.Dispose();
                            break;
                        }

                        if (_flows.Count >= 16)
                        {
                            client.Dispose();
                            throw new InvalidOperationException("Owned SQL proxy connection bound exceeded.");
                        }

                        var flow = new ProxyFlow(client, _host, _port, _lifetime.Token);
                        _flows.Add(flow);
                        _tasks.Add(flow.RunAsync());
                    }
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (SocketException) when (_lifetime.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
            ProxyFlow[] flows;
            lock (_gate)
            {
                flows = _flows.ToArray();
            }

            foreach (var flow in flows)
            {
                flow.Sever();
            }

            var failures = new List<Exception>();
            try
            {
                try { await _accept.ConfigureAwait(false); }
                catch (Exception failure) { failures.Add(failure); }
                try { await Task.WhenAll(_tasks).ConfigureAwait(false); }
                catch (Exception failure) { failures.Add(failure); }
            }
            finally
            {
                foreach (var flow in flows)
                {
                    flow.Dispose();
                }

                _listener.Dispose();
                _lifetime.Dispose();
            }

            if (failures.Count != 0)
            {
                throw new AggregateException("Owned SQL proxy cleanup failed.", failures);
            }
        }

        internal sealed class ProxyFlow(TcpClient client, string host, int port, CancellationToken lifetime) : IDisposable
        {
            private readonly TcpClient _upstream = new();
            private readonly SemaphoreSlim _serverWrites = new(1, 1);
            private readonly TaskCompletionSource _forwarded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly List<byte> _heldBytes = [];
            private int _closed;
            private int _armState;
            private int _severed;
            private int _commitCount;
            private bool _holding;
            private int _heldCount;
            private long _forwardedBytes;
            private long _forwardedAtGate;

            internal bool Active => Volatile.Read(ref _closed) == 0;
            internal int CommitCount => Volatile.Read(ref _commitCount);
            internal Task CommitForwarded => _forwarded.Task;
            internal Task ResponseHeld => _held.Task;
            internal int HeldBytes => Volatile.Read(ref _heldCount);
            // Every server write takes _serverWrites, and Holding never changes back to false.
            internal long ForwardedWhileHeld => Interlocked.Read(ref _forwardedBytes) - Interlocked.Read(ref _forwardedAtGate);

            internal void Arm(bool hold)
            {
                if (Interlocked.CompareExchange(ref _armState, hold ? 2 : 1, 0) != 0)
                {
                    throw new InvalidOperationException("The SQL stream can be armed only once.");
                }

            }

            internal async Task RunAsync()
            {
                using var stop = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                try
                {
                    await _upstream.ConnectAsync(host, port, stop.Token).ConfigureAwait(false);
                    var send = SendAsync(stop.Token);
                    var receive = ReceiveAsync(stop.Token);
                    await Task.WhenAny(send, receive).ConfigureAwait(false);
                    await stop.CancelAsync().ConfigureAwait(false);
                    client.Dispose();
                    _upstream.Dispose();
                    await Task.WhenAll(send, receive).ConfigureAwait(false);
                }
                catch (Exception error) when ((stop.IsCancellationRequested || lifetime.IsCancellationRequested || Volatile.Read(ref _severed) != 0)
                    && error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
                finally
                {
                    Volatile.Write(ref _closed, 1);
                }
            }

            private async Task SendAsync(CancellationToken token)
            {
                var buffer = new byte[4096];
                var parser = new CommerceCommitFrameParser();
                while (true)
                {
                    var read = await client.GetStream().ReadAsync(buffer, token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }

                    // Consult AFTER the pending read: the stage wrapper may arm it while it waits.
                    if (Volatile.Read(ref _armState) == 0)
                    {
                        await _upstream.GetStream().WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                        continue;
                    }

                    foreach (var message in parser.Append(buffer.AsMemory(0, read)))
                    {
                        if (message.IsCommit)
                        {
                            if (Interlocked.Increment(ref _commitCount) != 1)
                            {
                                throw new InvalidOperationException("More than one COMMIT on the selected stream.");
                            }

                            await _serverWrites.WaitAsync(token).ConfigureAwait(false);
                            try
                            {
                                _holding = Volatile.Read(ref _armState) == 2;
                                Interlocked.Exchange(ref _forwardedAtGate, Interlocked.Read(ref _forwardedBytes));
                            }
                            finally
                            {
                                _serverWrites.Release();
                            }
                        }

                        await _upstream.GetStream().WriteAsync(message.Wire, token).ConfigureAwait(false);
                        if (message.IsCommit)
                        {
                            _forwarded.TrySetResult();
                        }
                    }
                }
            }

            private async Task ReceiveAsync(CancellationToken token)
            {
                var buffer = new byte[4096];
                while (true)
                {
                    var read = await _upstream.GetStream().ReadAsync(buffer, token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }

                    await _serverWrites.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        if (_holding)
                        {
                            if (_heldBytes.Count + read > 65_536)
                            {
                                throw new InvalidOperationException("Held SQL response exceeded its test bound.");
                            }

                            _heldBytes.AddRange(buffer.AsSpan(0, read).ToArray());
                            Volatile.Write(ref _heldCount, _heldBytes.Count);
                            _held.TrySetResult();
                        }
                        else
                        {
                            await client.GetStream().WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                            Interlocked.Add(ref _forwardedBytes, read);
                        }
                    }
                    finally
                    {
                        _serverWrites.Release();
                    }
                }
            }

            internal void Sever()
            {
                Volatile.Write(ref _severed, 1);
                client.Dispose();
                _upstream.Dispose();
            }

            public void Dispose()
            {
                Sever();
                _serverWrites.Dispose();
            }
        }
    }
}
