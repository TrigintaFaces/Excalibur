// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Benchmarks.Diagnostics;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
[Trait("Pattern", "Regression")]
public sealed class RetryBenchmarkFilterShould
{
    [Fact]
    public void PreferDenyListAndMatchDerivedExceptions()
    {
        var options = new RetryOptions();
        options.NonRetryableExceptions.Clear();
        options.RetryableExceptions.Add(typeof(IOException));
        var filter = RetryBenchmarkFilter.Create(options)!;
        filter(new FileNotFoundException()).ShouldBeTrue();
        options.NonRetryableExceptions.Add(typeof(IOException));
        RetryBenchmarkFilter.Create(options)!(new FileNotFoundException()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 1, 1, 4)]
    [InlineData(1, 4, 1, 1)]
    [InlineData(2, 4, 1, 4)]
    public async Task ApplyConfiguredExceptionLists(int mode, int invalidOperationAttempts, int unsupportedAttempts, int ioAttempts)
    {
        var options = new RetryOptions();
        options.NonRetryableExceptions.Clear();
        if (mode == 1)
        {
            options.RetryableExceptions.Add(typeof(InvalidOperationException));
        }
        else if (mode == 2)
        {
            options.NonRetryableExceptions.Add(typeof(NotSupportedException));
        }

        var policy = new PollyRetryPolicyAdapter(Microsoft.Extensions.Options.Options.Create(new PollyRetryOptions
        {
            MaxRetryAttempts = 3,
            BaseDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero,
            ShouldRetry = RetryBenchmarkFilter.Create(options),
        }));
        await AssertAttempts(new InvalidOperationException("measured failure"), invalidOperationAttempts);
        await AssertAttempts(new NotSupportedException("excluded failure"), unsupportedAttempts);
        await AssertAttempts(new IOException("other failure"), ioAttempts);
        await AssertAttempts(new OperationCanceledException("cancellation"), 1);
        await AssertAttempts(new Excalibur.Dispatch.Messaging.TenantIsolationViolationException(), 1);

        async Task AssertAttempts(Exception expected, int count)
        {
            var calls = 0;
            var actual = await Record.ExceptionAsync(() => policy.ExecuteAsync<int>(_ =>
            {
                calls++;
                return Task.FromException<int>(expected);
            }, CancellationToken.None));
            actual.ShouldBeSameAs(expected);
            calls.ShouldBe(count);
        }
    }
}
