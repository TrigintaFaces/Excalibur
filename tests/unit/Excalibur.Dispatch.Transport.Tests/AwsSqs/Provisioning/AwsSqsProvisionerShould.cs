// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores Task

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Provisioning;

/// <summary>
/// Unit tests for <see cref="AwsSqsProvisioner"/> (bead h2k02a). Pre-fix the transport never applied a
/// native DLQ redrive policy and SNS subscription filter policies were option-only and inert — there
/// was no provisioner at all.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AwsSqsProvisionerShould
{
	private const string SourceQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/orders";
	private const string DeadLetterArn = "arn:aws:sqs:us-east-1:123456789012:orders-dlq";

	[Fact]
	public async Task ApplyRedrivePolicy_WhenEnabledAndDeadLetterQueueConfigured()
	{
		// Arrange
		var fakeSqs = A.Fake<IAmazonSQS>();
		var provisioner = new AwsSqsProvisioner(fakeSqs, snsClient: null, NullLogger<AwsSqsProvisioner>.Instance);

		var options = new AwsSqsTransportAdapterOptions
		{
			Name = "orders",
			QueueOptions = new AwsSqsQueueOptions
			{
				DeadLetterQueue = new AwsSqsDeadLetterOptions
				{
					QueueArn = DeadLetterArn,
					MaxReceiveCount = 5,
				},
			},
			Provisioning = new AwsSqsProvisioningOptions { Enabled = true },
		};
		options.QueueMappings[typeof(string)] = SourceQueueUrl;

		// Act
		await provisioner.ProvisionAsync(options, CancellationToken.None);

		// Assert — the source queue's RedrivePolicy is set to point at the DLQ with the max receive count.
		A.CallTo(() => fakeSqs.SetQueueAttributesAsync(
			A<SetQueueAttributesRequest>.That.Matches(r =>
				r.QueueUrl == SourceQueueUrl
				&& r.Attributes.ContainsKey("RedrivePolicy")
				&& r.Attributes["RedrivePolicy"].Contains(DeadLetterArn)
				&& r.Attributes["RedrivePolicy"].Contains("\"maxReceiveCount\":\"5\"")),
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task NotApplyRedrivePolicy_WhenProvisioningDisabled()
	{
		// Arrange — same DLQ config, but provisioning disabled (the default).
		var fakeSqs = A.Fake<IAmazonSQS>();
		var provisioner = new AwsSqsProvisioner(fakeSqs, snsClient: null, NullLogger<AwsSqsProvisioner>.Instance);

		var options = new AwsSqsTransportAdapterOptions
		{
			Name = "orders",
			QueueOptions = new AwsSqsQueueOptions
			{
				DeadLetterQueue = new AwsSqsDeadLetterOptions { QueueArn = DeadLetterArn, MaxReceiveCount = 5 },
			},
			Provisioning = new AwsSqsProvisioningOptions { Enabled = false },
		};
		options.QueueMappings[typeof(string)] = SourceQueueUrl;

		// Act
		await provisioner.ProvisionAsync(options, CancellationToken.None);

		// Assert — the framework must not mutate infrastructure when provisioning is off.
		A.CallTo(() => fakeSqs.SetQueueAttributesAsync(
			A<SetQueueAttributesRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task CreateSnsSubscription_AndApplyFilterPolicy_WhenConfigured()
	{
		// Arrange
		var fakeSqs = A.Fake<IAmazonSQS>();
		var fakeSns = A.Fake<IAmazonSimpleNotificationService>();
		A.CallTo(() => fakeSns.SubscribeAsync(A<SubscribeRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new SubscribeResponse
			{
				SubscriptionArn = "arn:aws:sns:us-east-1:123456789012:orders:sub-1",
			}));

		var provisioner = new AwsSqsProvisioner(fakeSqs, fakeSns, NullLogger<AwsSqsProvisioner>.Instance);

		var snsOptions = new AwsSqsSnsOptions();
		var subscription = new AwsSqsSubscriptionOptions
		{
			TopicArn = "arn:aws:sns:us-east-1:123456789012:orders",
			// Use a queue ARN so ARN resolution does not require a GetQueueAttributes call.
			QueueUrl = "arn:aws:sqs:us-east-1:123456789012:orders",
			FilterPolicy = new AwsSqsFilterPolicyOptions(),
		};
		subscription.FilterPolicy.Conditions["priority"] =
		[
			new AwsSqsFilterCondition { Operator = AwsSqsFilterOperator.ExactMatch, Values = ["high"] },
		];
		snsOptions.Subscriptions.Add(subscription);

		var options = new AwsSqsTransportAdapterOptions
		{
			Name = "orders",
			SnsOptions = snsOptions,
			Provisioning = new AwsSqsProvisioningOptions { Enabled = true },
		};

		// Act
		await provisioner.ProvisionAsync(options, CancellationToken.None);

		// Assert — the SNS->SQS subscription is created and the filter policy is applied.
		A.CallTo(() => fakeSns.SubscribeAsync(
			A<SubscribeRequest>.That.Matches(r =>
				r.Protocol == "sqs"
				&& r.TopicArn == "arn:aws:sns:us-east-1:123456789012:orders"
				&& r.Endpoint == "arn:aws:sqs:us-east-1:123456789012:orders"),
			A<CancellationToken>._)).MustHaveHappened();

		A.CallTo(() => fakeSns.SetSubscriptionAttributesAsync(
			A<SetSubscriptionAttributesRequest>.That.Matches(r =>
				r.AttributeName == "FilterPolicy" && r.AttributeValue.Contains("priority")),
			A<CancellationToken>._)).MustHaveHappened();
	}
}
