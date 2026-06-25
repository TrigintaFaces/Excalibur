// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Generic;
using System.Threading.Tasks;

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.DeadLetter;

/// <summary>
/// Author≠impl fail-fast lock for Sprint 848 Lane O1 (<c>mi6v59</c>): every <c>AddKafkaDeadLetterQueue</c>
/// registration path must chain <c>.ValidateOnStart()</c> + register
/// <see cref="KafkaDeadLetterOptions"/> validation, so an invalid configuration throws
/// <see cref="OptionsValidationException"/> at host start (Microsoft fail-fast bar) rather than being
/// silently accepted.
/// </summary>
/// <remarks>
/// RED on the pre-fix parent: the <c>Action</c> overload only called <c>services.Configure(...)</c> and the
/// <c>IConfiguration</c> overload only <c>.Bind(...)</c> — neither wired <c>ValidateOnStart</c> nor the
/// validator, so <c>host.StartAsync()</c> did NOT throw on an invalid value. GREEN once all paths wire
/// <c>.ValidateOnStart()</c> + <see cref="IValidateOptions{TOptions}"/>.
/// Drives the real host-start path (public API only); the DLQ registrations are plain singletons, so
/// starting the host runs only the options-validation startup step, not the Kafka producer/consumer.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KafkaDeadLetterValidateOnStartShould
{
	private static IHost BuildHost(Action<IServiceCollection> configureServices)
		=> new HostBuilder()
			.ConfigureServices((_, services) =>
			{
				services.AddLogging();
				configureServices(services);
			})
			.Build();

	[Fact]
	public async Task ThrowAtStartup_WhenActionOverloadConfiguresInvalidMaxDeliveryAttempts()
	{
		using var host = BuildHost(services =>
			services.AddKafkaDeadLetterQueue(dlq => dlq.MaxDeliveryAttempts = 0)); // [Range(1, int.MaxValue)] -> invalid

		await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
	}

	[Fact]
	public async Task ThrowAtStartup_WhenNamedTransportOverloadConfiguresInvalidTopicSuffix()
	{
		using var host = BuildHost(services =>
			services.AddKafkaDeadLetterQueue("orders", dlq => dlq.TopicSuffix = "")); // [Required] -> invalid

		await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
	}

	[Fact]
	public async Task ThrowAtStartup_WhenIConfigurationOverloadBindsInvalidValue()
	{
		// Third registration path (AC-O1.4): bind an invalid section via the IConfiguration overload.
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["MaxDeliveryAttempts"] = "0", // [Range(1, int.MaxValue)] -> invalid
			})
			.Build();

		using var host = BuildHost(services =>
			services.AddKafkaDeadLetterQueue("orders", configuration));

		await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
	}

	[Fact]
	public async Task StartCleanly_WhenOptionsAreValid()
	{
		using var host = BuildHost(services =>
			services.AddKafkaDeadLetterQueue(dlq =>
			{
				dlq.TopicSuffix = ".dlq";
				dlq.MaxDeliveryAttempts = 3;
			}));

		// Valid configuration must NOT trip the fail-fast path (guards against an over-strict validator).
		await host.StartAsync();
		await host.StopAsync();
	}
}
