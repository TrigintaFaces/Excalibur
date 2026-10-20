// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.SqlServer.Scheduling;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.SqlServer.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Pattern", "Validation")]
public sealed class SqlServerScheduleStoreOptionsShould
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task RejectBlankConnectionStringsAtHostStartup(string? connectionString)
    {
        using var host = CreateHost(options => options.ConnectionString = connectionString!);

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());

        exception.Failures.ShouldContain(failure => failure.Contains(nameof(SqlServerScheduleStoreOptions.ConnectionString), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task RejectNonPositiveLeasesAtHostStartup(long ticks)
    {
        using var host = CreateHost(options => options.LeaseDuration = TimeSpan.FromTicks(ticks));

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());

        exception.Failures.ShouldContain(failure => failure.Contains(nameof(SqlServerScheduleStoreOptions.LeaseDuration), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(nameof(SqlServerScheduleStoreOptions.CommandTimeoutSeconds), 0)]
    [InlineData(nameof(SqlServerScheduleStoreOptions.CommandTimeoutSeconds), -1)]
    [InlineData(nameof(SqlServerScheduleStoreOptions.LockTimeoutMilliseconds), -1)]
    public async Task RejectUnboundedTimeoutsAtHostStartup(string property, int value)
    {
        using var host = CreateHost(options => SetTimeout(options, property, value));

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());

        exception.Failures.ShouldContain(failure => failure.Contains(property, StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartWithPositiveMinimumsAndAnImmediateLockTimeoutWithoutConnectingToSqlServer()
    {
        using var host = CreateHost(options =>
        {
            options.LeaseDuration = TimeSpan.FromTicks(1);
            options.CommandTimeoutSeconds = 1;
            options.LockTimeoutMilliseconds = 0;
        });

        await host.StartAsync();

        var store = host.Services.GetRequiredService<IScheduleStore>();
        store.ShouldBeOfType<SqlServerScheduleStore>().LeaseDuration.ShouldBe(TimeSpan.FromTicks(1));

        await host.StopAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void RetainBlankConnectionStringGuardsForDirectConstruction(string? connectionString)
    {
        var options = ValidOptions();
        options.ConnectionString = connectionString!;

        Should.Throw<ArgumentException>(() => new SqlServerScheduleStore(Options.Create(options)));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void RetainNonPositiveLeaseGuardsForDirectConstruction(long ticks)
    {
        var options = ValidOptions();
        options.LeaseDuration = TimeSpan.FromTicks(ticks);

        Should.Throw<ArgumentOutOfRangeException>(() => new SqlServerScheduleStore(Options.Create(options)));
    }

    [Theory]
    [InlineData(nameof(SqlServerScheduleStoreOptions.CommandTimeoutSeconds), 0)]
    [InlineData(nameof(SqlServerScheduleStoreOptions.CommandTimeoutSeconds), -1)]
    [InlineData(nameof(SqlServerScheduleStoreOptions.LockTimeoutMilliseconds), -1)]
    public void RetainUnboundedTimeoutGuardsForDirectConstruction(string property, int value)
    {
        var options = ValidOptions();
        SetTimeout(options, property, value);

        Should.Throw<ArgumentOutOfRangeException>(() => new SqlServerScheduleStore(Options.Create(options)));
    }

    private static IHost CreateHost(Action<SqlServerScheduleStoreOptions> configure) => new HostBuilder()
        .ConfigureServices(services => services.AddSqlServerScheduleStore(options =>
        {
            options.ConnectionString = ValidOptions().ConnectionString;
            configure(options);
        }))
        .Build();

    private static SqlServerScheduleStoreOptions ValidOptions() => new()
    {
        ConnectionString = "Server=localhost;Database=ScheduleOptionsValidationOnly;Integrated Security=true;Encrypt=false",
    };

    private static void SetTimeout(SqlServerScheduleStoreOptions options, string property, int value)
    {
        if (property == nameof(SqlServerScheduleStoreOptions.CommandTimeoutSeconds))
        {
            options.CommandTimeoutSeconds = value;
        }
        else
        {
            options.LockTimeoutMilliseconds = value;
        }
    }
}
