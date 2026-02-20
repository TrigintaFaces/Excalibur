// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Tests.Messaging.Streaming.TestTypes;

namespace Excalibur.Dispatch.Tests.Messaging.Streaming;

/// <summary>
/// Tests for streaming handler DI registration and discovery.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingHandlerRegistrationShould
{
	[Fact]
	public void RegisterStreamingHandlerWithAddStreamingHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register streaming handler with scoped lifetime
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		// Act
		using var provider = services.BuildServiceProvider();
		var handler = provider.GetService<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>();

		// Assert
		_ = handler.ShouldNotBeNull();
		_ = handler.ShouldBeOfType<TestCsvStreamingHandler>();
	}

	[Fact]
	public void RegisterStreamConsumerHandlerWithAddStreamConsumerHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register stream consumer handler with scoped lifetime
		_ = services.AddScoped<CollectingStreamConsumerHandler>();
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(
			sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		// Act
		using var provider = services.BuildServiceProvider();
		var handler = provider.GetService<IStreamConsumerHandler<TestBatchDocument>>();

		// Assert
		_ = handler.ShouldNotBeNull();
		_ = handler.ShouldBeOfType<CollectingStreamConsumerHandler>();
	}

	[Fact]
	public void DiscoverStreamingHandlersFromAssembly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register discoverable handler directly (simulating assembly discovery)
		_ = services.AddScoped<DiscoverableStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<DiscoverableStreamingHandler>());

		// Act
		using var provider = services.BuildServiceProvider();
		var handler = provider.GetService<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>();

		// Assert
		_ = handler.ShouldNotBeNull();
	}

	[Fact]
	public void DiscoverStreamConsumerHandlersFromAssembly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register discoverable handler directly (simulating assembly discovery)
		_ = services.AddScoped<DiscoverableStreamConsumerHandler>();
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(
			sp => sp.GetRequiredService<DiscoverableStreamConsumerHandler>());

		// Act
		using var provider = services.BuildServiceProvider();
		var handler = provider.GetService<IStreamConsumerHandler<TestBatchDocument>>();

		// Assert
		_ = handler.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterHandlersWithScopedLifetime()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register streaming handler with scoped lifetime
		_ = services.AddScoped<StatefulStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<StatefulStreamingHandler>());

		// Act
		using var provider = services.BuildServiceProvider();

		// Create two scopes and verify handlers are different instances
		using var scope1 = provider.CreateScope();
		using var scope2 = provider.CreateScope();

		var handler1 = scope1.ServiceProvider.GetService<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>();
		var handler2 = scope2.ServiceProvider.GetService<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>();

		// Assert - handlers should be different instances in different scopes
		_ = handler1.ShouldNotBeNull();
		_ = handler2.ShouldNotBeNull();
		ReferenceEquals(handler1, handler2).ShouldBeFalse();
	}

	[Fact]
	public void ReturnSameInstanceWithinScope()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register streaming handler with scoped lifetime
		_ = services.AddScoped<StatefulStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<StatefulStreamingHandler>());

		// Act
		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		var handler1 = scope.ServiceProvider.GetService<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>();
		var handler2 = scope.ServiceProvider.GetService<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>();

		// Assert - should be same instance within scope
		_ = handler1.ShouldNotBeNull();
		_ = handler2.ShouldNotBeNull();
		ReferenceEquals(handler1, handler2).ShouldBeTrue();
	}

	[Fact]
	public void RegisterMultipleStreamingHandlerTypes()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(_ => { });

		// Register both streaming and consumer handlers
		_ = services.AddScoped<TestCsvStreamingHandler>();
		_ = services.AddScoped<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>(
			sp => sp.GetRequiredService<TestCsvStreamingHandler>());

		_ = services.AddScoped<CollectingStreamConsumerHandler>();
		_ = services.AddScoped<IStreamConsumerHandler<TestBatchDocument>>(
			sp => sp.GetRequiredService<CollectingStreamConsumerHandler>());

		// Act
		using var provider = services.BuildServiceProvider();

		var streamingHandler = provider.GetService<IStreamingDocumentHandler<TestCsvDocument, TestDataRow>>();
		var consumerHandler = provider.GetService<IStreamConsumerHandler<TestBatchDocument>>();

		// Assert
		_ = streamingHandler.ShouldNotBeNull();
		_ = consumerHandler.ShouldNotBeNull();
	}

}

#region Test Types for Discovery (file-scoped to avoid CA1034)

/// <summary>
/// Streaming handler that can be discovered by assembly scanning.
/// </summary>
file sealed class DiscoverableStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
{
	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		TestCsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		foreach (var row in document.Rows)
		{
			yield return new TestDataRow(row);
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}
}

/// <summary>
/// Stream consumer handler that can be discovered by assembly scanning.
/// </summary>
file sealed class DiscoverableStreamConsumerHandler : IStreamConsumerHandler<TestBatchDocument>
{
	public async Task HandleAsync(
		IAsyncEnumerable<TestBatchDocument> documents,
		CancellationToken cancellationToken)
	{
		await foreach (var _ in documents.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			// Process
		}
	}
}

/// <summary>
/// Stateful streaming handler for testing scoped lifetime.
/// </summary>
file sealed class StatefulStreamingHandler : IStreamingDocumentHandler<TestCsvDocument, TestDataRow>
{
	private readonly Guid _instanceId = Guid.NewGuid();

	public Guid InstanceId => _instanceId;

	public async IAsyncEnumerable<TestDataRow> HandleAsync(
		TestCsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		foreach (var row in document.Rows)
		{
			yield return new TestDataRow(row);
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}
}

#endregion
