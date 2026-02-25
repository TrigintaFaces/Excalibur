// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsChannelServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterChannelAdapter()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqsChannelAdapter(opts =>
		{
			opts.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
			opts.ConcurrentPollers = 5;
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RegisterBatchProcessor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqsBatchProcessor(opts =>
		{
			opts.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
			opts.MaxConcurrentReceiveBatches = 5;
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RegisterLongPollingReceiver()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqsLongPollingReceiver(opts =>
		{
			opts.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowWhenInfrastructureConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqsChannelInfrastructure<TestMessageProcessor>(null!));
	}

	[Fact]
	public void RegisterFullInfrastructure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqsChannelInfrastructure<TestMessageProcessor>(opts =>
		{
			opts.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
			opts.ConcurrentPollers = 5;
			opts.MaxConcurrentReceiveBatches = 10;
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(5); // Should register multiple services
	}

	private sealed class TestMessageProcessor : IMessageProcessor<Message>
	{
		public Task<ProcessingResult> ProcessAsync(Message message, CancellationToken cancellationToken) =>
			Task.FromResult(ProcessingResult.Ok());
	}
}
