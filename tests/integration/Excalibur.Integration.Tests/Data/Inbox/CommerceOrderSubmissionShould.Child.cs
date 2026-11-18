// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;

namespace Excalibur.Integration.Tests.Data.Inbox;

public sealed partial class CommerceOrderSubmissionShould
{
    private async Task<string> RunChildAsync(string mode, CancellationToken token)
    {
        var child = new CommerceChild(_connectionString, mode);
        Exception? primary = null;
        Exception? cleanupError = null;
        string output;
        try
        {
            output = await child.WaitForSuccessAsync(token);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            try
            {
                await child.DisposeAsync();
            }
            catch (Exception cleanup)
            {
                if (primary != null)
                {
                    primary.Data["CommerceChildCleanupFailure"] = cleanup;
                }
                else
                {
                    cleanupError = cleanup;
                }
            }
        }

        if (cleanupError != null)
        {
            throw cleanupError;
        }

        return output;
    }

    // Same bounded child/async-drain/owned-Job mechanism as the existing scheduler process controls.
    // This launches the actual sample, not the unrelated scheduling probe or a duplicate engine.
    private sealed class CommerceChild : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _output;
        private readonly Task<string> _error;
        private readonly Task _lifetime;
        private int _timedOut;

        internal CommerceChild(string connection, string mode)
        {
            OperatingSystem.IsWindows().ShouldBeTrue("Use the existing non-breakaway Windows Job launcher.");
            var path = Path.Combine(AppContext.BaseDirectory, "ecommerce-process-sample", "Excalibur.Dispatch.Examples.ECommerceSample.dll");
            File.Exists(path).ShouldBeTrue("The current owning sample output closure must be copied by the integration build.");
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add(path);
            start.ArgumentList.Add("--sql-orders");
            start.ArgumentList.Add(mode);
            start.Environment["ECOMMERCE_SQL_CONNECTION"] = connection;
            _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the owned commerce sample.");
            _output = _process.StandardOutput.ReadToEndAsync();
            _error = _process.StandardError.ReadToEndAsync();
            _lifetime = EnforceLifetimeAsync();
        }

        internal int Id => _process.Id;

        internal async Task<string> WaitForSuccessAsync(CancellationToken token)
        {
            await _process.WaitForExitAsync(token);
            await _lifetime.WaitAsync(token);
            Volatile.Read(ref _timedOut).ShouldBe(0, "A lifetime timeout is never a successful process control.");
            _process.ExitCode.ShouldBe(0, await _error.WaitAsync(token));
            return await _output.WaitAsync(token);
        }

        internal async Task WriteSafeStatusAsync()
        {
            try
            {
                var exited = _process.HasExited;
                TestContext.Current.TestOutputHelper?.WriteLine($"phase-child pid={Id} exited={exited} exit={(exited ? _process.ExitCode : -1)} lifetimeTimeout={Volatile.Read(ref _timedOut)}");
                if (exited)
                {
                    var error = await _error.WaitAsync(TimeSpan.FromSeconds(2));
                    var match = System.Text.RegularExpressions.Regex.Match(error,
                        @"(?m)^\s*(?:Unhandled exception\.\s*)?([A-Za-z_][A-Za-z0-9_.+`]*Exception)(?:\s|:|\()",
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                    var type = match.Success ? match.Groups[1].Value : "unclassified";
                    TestContext.Current.TestOutputHelper?.WriteLine($"phase-child exceptionType={type[..Math.Min(type.Length, 256)]}");
                }
            }
            catch (Exception diagnostic)
            {
                TestContext.Current.TestOutputHelper?.WriteLine($"phase-child diagnosticException={diagnostic.GetType().FullName}");
            }
        }

        internal async Task KillAsync(CancellationToken token)
        {
            _process.HasExited.ShouldBeFalse("The child must remain in the observed open-transaction cut.");
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(token);
            Volatile.Read(ref _timedOut).ShouldBe(0);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            await Task.WhenAll(_output, _error, _lifetime).WaitAsync(TimeSpan.FromSeconds(15));
            _process.Dispose();
        }

        private async Task EnforceLifetimeAsync()
        {
            try
            {
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(75));
            }
            catch (TimeoutException)
            {
                Interlocked.Exchange(ref _timedOut, 1);
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                await _process.WaitForExitAsync();
            }
        }
    }
}
