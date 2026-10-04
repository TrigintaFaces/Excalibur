// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Data.DataProcessing;
using Microsoft.Extensions.Configuration;
namespace Excalibur.Data.Tests.DataProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class RegistrationFidelityShould
{
    [Fact]
    public void BindTheSuppliedConfigurationWithoutRequiringOneInDI()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Processing:QueueSize"] = "321", ["Processing:ProducerBatchSize"] = "12" }).Build();
        var services = new ServiceCollection();
        services.AddRecordHandler<Handler, int>(config, "Processing");
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DataProcessingOptions>>().Value;
        options.QueueSize.ShouldBe(321);
        options.ProducerBatchSize.ShouldBe(12);
    }
    [Fact]
    public void ValidateTheSuppliedOptionsAtStartup()
    {
        var services = new ServiceCollection();
        services.AddRecordHandler<Handler, int>(new DataProcessingOptions { QueueSize = 10, ProducerBatchSize = 20 });
        using var provider = services.BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }
    [Fact]
    public void GiveMonitorAndConsumerTheSameConfiguredValues()
    {
        var services = new ServiceCollection();
        services.AddRecordHandler<Handler, int>(new DataProcessingOptions { QueueSize = 321, ProducerBatchSize = 12 });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<DataProcessingOptions>>().Value.QueueSize.ShouldBe(321);
        provider.GetRequiredService<IOptionsMonitor<DataProcessingOptions>>().CurrentValue.QueueSize.ShouldBe(321);
    }
    [Fact]
    public void KeepNamedOptionsIndependentOfTheSuppliedDefault()
    {
        var services = new ServiceCollection();
        var supplied = new DataProcessingOptions { QueueSize = 321, ProducerBatchSize = 12 };
        services.AddRecordHandler<Handler, int>(supplied);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptionsFactory<DataProcessingOptions>>();
        var first = factory.Create(Options.DefaultName);
        var second = factory.Create(Options.DefaultName);
        first.ShouldNotBeSameAs(supplied);
        second.ShouldNotBeSameAs(first);
        factory.Create("other").QueueSize.ShouldBe(new DataProcessingOptions().QueueSize);
    }
    [Fact]
    public void ComposePartialConfigurationAndReloadWithoutMutatingOldOptions()
    {
        var first = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Processing:QueueSize"] = "321", ["Processing:ProducerBatchSize"] = "12" }).Build();
        var second = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Processing:QueueSize"] = "654" }).Build();
        var services = new ServiceCollection();
        services.AddRecordHandler<Handler, int>(first, "Processing");
        services.AddRecordHandler<Handler, int>(second, "Processing");
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<DataProcessingOptions>>();
        var original = monitor.CurrentValue;
        original.QueueSize.ShouldBe(654);
        original.ProducerBatchSize.ShouldBe(12);
        second["Processing:QueueSize"] = "987";
        second.Reload();
        monitor.CurrentValue.QueueSize.ShouldBe(987);
        original.QueueSize.ShouldBe(654);
    }

    [Fact]
    public void RejectMalformedConfigurationInsteadOfUsingDefaults()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Processing:QueueSize"] = "invalid" }).Build();
        var services = new ServiceCollection();
        services.AddRecordHandler<Handler, int>(config, "Processing");
        using var provider = services.BuildServiceProvider();
        Should.Throw<FormatException>(() => provider.GetRequiredService<IOptions<DataProcessingOptions>>().Value);
    }

    internal sealed class Handler : IRecordHandler<int>
    {
        public Task ProcessAsync(int record, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
