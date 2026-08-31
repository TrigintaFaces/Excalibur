// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Request/reply is a consumer-implemented seam: the framework ships the contract, the options and the
/// startup validation, and the consumer supplies the <see cref="IRequestReplyClient"/>. No shipped type
/// reads these option values, and none can — the only reader is the consumer's own class.
/// </summary>
/// <remarks>
/// These arms are what makes that claim falsifiable rather than asserted. The first proves the values a
/// consumer configures are the values their implementation resolves; the second proves the startup
/// refusal still fires, so the registration is not merely decorative.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RequestReplyOptionsReachTheClientShould
{
	/// <summary>A stand-in for the client a consumer supplies, resolving the options the same way.</summary>
	private sealed class RecordingRequestReplyClient(IOptions<RequestReplyOptions> options) : IRequestReplyClient
	{
		public RequestReplyOptions Observed { get; } = options.Value;

		public Task<RequestReplyMessage> SendRequestAsync(
			RequestReplyMessage request, string destinationEntity, CancellationToken cancellationToken) =>
			Task.FromResult(request);

		public Task<RequestReplyMessage?> ReceiveReplyAsync(string sessionId, CancellationToken cancellationToken) =>
			Task.FromResult<RequestReplyMessage?>(null);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	[Fact]
	public async Task HandEveryConfiguredValueToTheConsumersImplementation()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusRequestReply<RecordingRequestReplyClient>(options =>
		{
			options.ReplyQueueName = "replies";
			options.ReplyTimeout = TimeSpan.FromSeconds(42);
			options.RequestTimeToLive = TimeSpan.FromSeconds(90);
			options.MaxConcurrentRequests = 7;
		});

		await using var provider = services.BuildServiceProvider();
		var client = (RecordingRequestReplyClient)provider.GetRequiredService<IRequestReplyClient>();

		client.Observed.ReplyQueueName.ShouldBe("replies");
		client.Observed.ReplyTimeout.ShouldBe(TimeSpan.FromSeconds(42));
		client.Observed.RequestTimeToLive.ShouldBe(TimeSpan.FromSeconds(90));
		client.Observed.MaxConcurrentRequests.ShouldBe(7);
	}

	[Fact]
	public async Task RefuseToStartWhenTheReplyQueueIsMissing()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusRequestReply<RecordingRequestReplyClient>(options =>
			options.MaxConcurrentRequests = 7);

		await using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RequestReplyOptions>>().Value);
	}

	[Fact]
	public async Task RefuseToStartWhenTheConcurrencyLimitIsOutOfRange()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusRequestReply<RecordingRequestReplyClient>(options =>
		{
			options.ReplyQueueName = "replies";
			options.MaxConcurrentRequests = 0;
		});

		await using var provider = services.BuildServiceProvider();

		_ = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<RequestReplyOptions>>().Value);
	}
}
