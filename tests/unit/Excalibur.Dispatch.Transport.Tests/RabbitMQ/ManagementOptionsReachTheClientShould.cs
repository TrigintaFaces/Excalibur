// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// The management API client is a consumer-implemented seam: the framework ships the contract, the
/// options and the startup validation, and the consumer supplies the
/// <see cref="IRabbitMqManagementClient"/>. No shipped type reads the base URL or the credentials, and
/// none can — the only reader is the consumer's own class.
/// </summary>
/// <remarks>
/// These arms are what makes that claim falsifiable. The first proves the values a consumer configures
/// are the values their implementation resolves; the second proves the startup refusal still fires, so
/// the registration is not merely decorative.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class ManagementOptionsReachTheClientShould : UnitTestBase
{
	/// <summary>A stand-in for the client a consumer supplies, resolving the options the same way.</summary>
	private sealed class RecordingManagementClient(IOptions<RabbitMqManagementOptions> options) : IRabbitMqManagementClient
	{
		public RabbitMqManagementOptions Observed { get; } = options.Value;

		public Task<QueueInfo> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken) =>
			Task.FromResult<QueueInfo>(null!);

		public Task<ExchangeInfo> GetExchangeInfoAsync(string exchangeName, CancellationToken cancellationToken) =>
			Task.FromResult<ExchangeInfo>(null!);

		public Task<ConnectionInfo> GetConnectionInfoAsync(string connectionName, CancellationToken cancellationToken) =>
			Task.FromResult<ConnectionInfo>(null!);

		public Task PurgeQueueAsync(string queueName, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task<BrokerOverview> GetOverviewAsync(CancellationToken cancellationToken) =>
			Task.FromResult<BrokerOverview>(null!);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	[Fact]
	public async Task HandEveryConfiguredValueToTheConsumersImplementation()
	{
		var services = new ServiceCollection();

		_ = services.AddRabbitMqManagement<RecordingManagementClient>(options =>
		{
			options.BaseUrl = "http://rabbit.internal:15672";
			options.Username = "ops";
			options.Password = "s3cret";
			options.RequestTimeout = TimeSpan.FromSeconds(17);
		});

		await using var provider = services.BuildServiceProvider();
		var client = (RecordingManagementClient)provider.GetRequiredService<IRabbitMqManagementClient>();

		client.Observed.BaseUrl.ShouldBe("http://rabbit.internal:15672");
		client.Observed.Username.ShouldBe("ops");
		client.Observed.Password.ShouldBe("s3cret");
		client.Observed.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(17));
	}

	[Fact]
	public async Task RefuseToStartOnAnInvalidConfiguration()
	{
		var services = new ServiceCollection();

		_ = services.AddRabbitMqManagement<RecordingManagementClient>(options => options.Username = string.Empty);

		await using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RabbitMqManagementOptions>>().Value);
	}
}
