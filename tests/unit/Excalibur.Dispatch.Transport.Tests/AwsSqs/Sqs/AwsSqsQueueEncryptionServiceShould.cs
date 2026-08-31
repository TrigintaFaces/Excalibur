// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores Task

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

/// <summary>
/// Unit tests for <see cref="AwsSqsQueueEncryptionService"/>.
/// </summary>
/// <remarks>
/// The SNS half of this seam was wired first; SQS inherited the same <c>EnableEncryption</c> flag from
/// the shared AWS provider options and nothing read it, so a consumer who enabled encryption on a queue
/// got silence. These tests bind the emitted <see cref="SetQueueAttributesRequest"/> rather than the
/// round-trip of the property, because a dead option round-trips perfectly.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AwsSqsQueueEncryptionServiceShould
{
	private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/orders";

	private const string TransportName = "orders";

	private static AwsSqsQueueEncryptionService CreateService(IAmazonSQS sqsClient, AwsSqsOptions options)
	{
		// The service reads its options BY TRANSPORT NAME, so that a host running two named SQS transports
		// applies each transport's own KMS key to its own queue. The monitor answers only for this
		// transport's name: a service that went back to reading the unnamed instance would get null here
		// and fail, rather than passing on a value it was not entitled to.
		var monitor = A.Fake<IOptionsMonitor<AwsSqsOptions>>();
		_ = A.CallTo(() => monitor.Get(TransportName)).Returns(options);

		return new AwsSqsQueueEncryptionService(
			sqsClient, monitor, TransportName, NullLogger<AwsSqsQueueEncryptionService>.Instance);
	}

	private static AwsSqsOptions EncryptedOptions(string? key = "alias/orders-key") =>
		new()
		{
			QueueUrl = new Uri(QueueUrl),
			EnableEncryption = true,
			KmsMasterKeyId = key,
		};

	[Fact]
	public async Task ApplyTheConfiguredKeyToTheQueue_WhenEncryptionRequested()
	{
		// Arrange
		var fakeSqs = A.Fake<IAmazonSQS>();

		// Act
		await CreateService(fakeSqs, EncryptedOptions()).StartAsync(CancellationToken.None);

		// Assert — the key reaches AWS as the queue's KmsMasterKeyId attribute.
		A.CallTo(() => fakeSqs.SetQueueAttributesAsync(
			A<SetQueueAttributesRequest>.That.Matches(r =>
				r.QueueUrl == QueueUrl
				&& r.Attributes["KmsMasterKeyId"] == "alias/orders-key"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData("alias/key-one")]
	[InlineData("alias/key-two")]
	public async Task SendTheKeyTheConsumerConfigured_NotAFixedValue(string configuredKey)
	{
		// Arrange — the liveness arm: two different configured values must produce two different
		// requests. An implementation that ignored the option, or hard-coded a key, fails one of these.
		var fakeSqs = A.Fake<IAmazonSQS>();

		// Act
		await CreateService(fakeSqs, EncryptedOptions(configuredKey)).StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => fakeSqs.SetQueueAttributesAsync(
			A<SetQueueAttributesRequest>.That.Matches(r => r.Attributes["KmsMasterKeyId"] == configuredKey),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotTouchTheQueue_WhenEncryptionNotRequested()
	{
		// Arrange — the safety arm: the transport must not mutate infrastructure it was not asked to.
		var fakeSqs = A.Fake<IAmazonSQS>();
		var options = new AwsSqsOptions { QueueUrl = new Uri(QueueUrl), EnableEncryption = false };

		// Act
		await CreateService(fakeSqs, options).StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => fakeSqs.SetQueueAttributesAsync(
			A<SetQueueAttributesRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task FailStartup_WhenEncryptionRequestedWithoutAKey()
	{
		// Arrange — fail closed. Continuing would leave the host sending to an unencrypted queue that
		// the operator believes is encrypted, which is the defect this service exists to remove.
		var fakeSqs = A.Fake<IAmazonSQS>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => CreateService(fakeSqs, EncryptedOptions(key: null)).StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("no KMS key");

		A.CallTo(() => fakeSqs.SetQueueAttributesAsync(
			A<SetQueueAttributesRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task FailStartup_WhenTheQueueIsUnknown()
	{
		// Arrange — a key with nowhere to apply it must not be silently dropped.
		var fakeSqs = A.Fake<IAmazonSQS>();
		var options = new AwsSqsOptions
		{
			QueueUrl = null,
			EnableEncryption = true,
			KmsMasterKeyId = "alias/orders-key",
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => CreateService(fakeSqs, options).StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task SurfaceTheFailure_WhenAwsRejectsTheKey()
	{
		// Arrange — fail closed on an AWS-side rejection rather than starting unencrypted.
		var fakeSqs = A.Fake<IAmazonSQS>();
		A.CallTo(() => fakeSqs.SetQueueAttributesAsync(
				A<SetQueueAttributesRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSQSException("key not found"));

		// Act & Assert
		_ = await Should.ThrowAsync<AmazonSQSException>(
			() => CreateService(fakeSqs, EncryptedOptions("alias/missing")).StartAsync(CancellationToken.None));
	}

	// WIRE arm: constructing the service by hand proves only that it works when handed its dependencies.
	// This resolves it through a real container built by the production registration path — the gap that
	// let the flag ship dead in the first place was a missing registration, not a broken implementation.
	[Fact]
	public void BeResolvableFromTheProductionRegistrationPath()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		services.AddSingleton(A.Fake<IAmazonSQS>());
		_ = services.AddAwsSqsTransport(sqs => sqs
			.UseRegion("us-east-1")
			.MapQueue<SampleQueueMessage>(QueueUrl));

		using var provider = services.BuildServiceProvider();

		// Act
		var hostedServices = provider.GetServices<IHostedService>();

		// Assert — the encryption service is actually in the host's startup pipeline.
		hostedServices.ShouldContain(s => s is AwsSqsQueueEncryptionService);
	}

	private sealed class SampleQueueMessage
	{
		public string Id { get; init; } = string.Empty;
	}
}
