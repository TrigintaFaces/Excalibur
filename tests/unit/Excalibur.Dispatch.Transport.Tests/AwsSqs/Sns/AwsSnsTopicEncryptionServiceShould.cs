// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores Task

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sns;

/// <summary>
/// Unit tests for <see cref="AwsSnsTopicEncryptionService"/>.
/// </summary>
/// <remarks>
/// Before this service existed, a consumer calling <c>EnableEncryption(kmsKey)</c> had the key copied
/// from the builder into the transport options and then into the SNS options, where nothing read it —
/// the key never reached an AWS call and the topic stayed unencrypted with no error. These tests bind
/// the emitted <see cref="SetTopicAttributesRequest"/>, not the round-trip of the property, because a
/// dead option round-trips perfectly.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AwsSnsTopicEncryptionServiceShould
{
	private const string TopicArn = "arn:aws:sns:us-east-1:123456789012:orders";
	private const string TransportName = "orders";

	private static AwsSnsTopicEncryptionService CreateService(
		IAmazonSimpleNotificationService snsClient,
		AwsSnsOptions options) =>
		new(
			snsClient,
			new StaticOptionsMonitor(options),
			TransportName,
			NullLogger<AwsSnsTopicEncryptionService>.Instance);

	/// <summary>
	/// The applier reads its own NAMED options, because two named SNS transports each register their
	/// own configuration. This monitor serves the one instance under test for whatever name is asked.
	/// </summary>
	private sealed class StaticOptionsMonitor(AwsSnsOptions options)
		: Microsoft.Extensions.Options.IOptionsMonitor<AwsSnsOptions>
	{
		public AwsSnsOptions CurrentValue => options;

		public AwsSnsOptions Get(string? name) => options;

		public IDisposable? OnChange(Action<AwsSnsOptions, string?> listener) => null;
	}

	[Fact]
	public async Task ApplyTheConfiguredKeyToTheTopic_WhenEncryptionRequested()
	{
		// Arrange
		var fakeSns = A.Fake<IAmazonSimpleNotificationService>();
		var options = new AwsSnsOptions
		{
			TopicArn = TopicArn,
			EnableEncryption = true,
			KmsMasterKeyId = "alias/orders-key",
		};

		// Act
		await CreateService(fakeSns, options).StartAsync(CancellationToken.None);

		// Assert — the key reaches AWS as the topic's KmsMasterKeyId attribute.
		A.CallTo(() => fakeSns.SetTopicAttributesAsync(
			A<SetTopicAttributesRequest>.That.Matches(r =>
				r.TopicArn == TopicArn
				&& r.AttributeName == "KmsMasterKeyId"
				&& r.AttributeValue == "alias/orders-key"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData("alias/key-one")]
	[InlineData("alias/key-two")]
	public async Task SendTheKeyTheConsumerConfigured_NotAFixedValue(string configuredKey)
	{
		// Arrange — the liveness arm: two different configured values must produce two different
		// requests. An implementation that ignored the option, or hard-coded a key, fails one of these.
		var fakeSns = A.Fake<IAmazonSimpleNotificationService>();
		var options = new AwsSnsOptions
		{
			TopicArn = TopicArn,
			EnableEncryption = true,
			KmsMasterKeyId = configuredKey,
		};

		// Act
		await CreateService(fakeSns, options).StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => fakeSns.SetTopicAttributesAsync(
			A<SetTopicAttributesRequest>.That.Matches(r => r.AttributeValue == configuredKey),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotTouchTheTopic_WhenEncryptionNotRequested()
	{
		// Arrange — the safety arm: the transport must not mutate infrastructure it was not asked to.
		var fakeSns = A.Fake<IAmazonSimpleNotificationService>();
		var options = new AwsSnsOptions { TopicArn = TopicArn, EnableEncryption = false };

		// Act
		await CreateService(fakeSns, options).StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => fakeSns.SetTopicAttributesAsync(
			A<SetTopicAttributesRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task FailStartup_WhenEncryptionRequestedWithoutAKey()
	{
		// Arrange — fail closed. Continuing would leave the host publishing to an unencrypted topic
		// that the operator believes is encrypted, which is the defect this service exists to remove.
		var fakeSns = A.Fake<IAmazonSimpleNotificationService>();
		var options = new AwsSnsOptions { TopicArn = TopicArn, EnableEncryption = true, KmsMasterKeyId = null };

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => CreateService(fakeSns, options).StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("no KMS key");

		A.CallTo(() => fakeSns.SetTopicAttributesAsync(
			A<SetTopicAttributesRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task FailStartup_WhenTheTopicIsUnknown()
	{
		// Arrange — a key with nowhere to apply it must not be silently dropped.
		var fakeSns = A.Fake<IAmazonSimpleNotificationService>();
		var options = new AwsSnsOptions
		{
			TopicArn = string.Empty,
			EnableEncryption = true,
			KmsMasterKeyId = "alias/orders-key",
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => CreateService(fakeSns, options).StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task SurfaceTheFailure_WhenAwsRejectsTheKey()
	{
		// Arrange — fail closed on an AWS-side rejection rather than starting unencrypted.
		var fakeSns = A.Fake<IAmazonSimpleNotificationService>();
		A.CallTo(() => fakeSns.SetTopicAttributesAsync(
				A<SetTopicAttributesRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSimpleNotificationServiceException("key not found"));

		var options = new AwsSnsOptions
		{
			TopicArn = TopicArn,
			EnableEncryption = true,
			KmsMasterKeyId = "alias/missing",
		};

		// Act & Assert
		_ = await Should.ThrowAsync<AmazonSimpleNotificationServiceException>(
			() => CreateService(fakeSns, options).StartAsync(CancellationToken.None));
	}
}
