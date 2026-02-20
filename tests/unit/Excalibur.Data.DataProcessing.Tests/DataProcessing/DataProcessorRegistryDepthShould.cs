// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // Members annotated with RequiresUnreferencedCode - test code
#pragma warning disable IL3050 // Members annotated with RequiresDynamicCode - test code

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Exceptions;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Depth tests for <see cref="DataProcessorRegistry"/>.
/// Covers registration via constructor, TryGetFactory, GetFactory, and error scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataProcessorRegistryDepthShould
{
	[Fact]
	public void RegisterAndRetrieveProcessorFactory()
	{
		// Arrange
		var processor = new TestOrderProcessor();
		var registry = new DataProcessorRegistry([processor]);

		// Act
		var found = registry.TryGetFactory("orders", out var factory);

		// Assert
		found.ShouldBeTrue();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void GetFactoryReturnFactoryForRegisteredType()
	{
		// Arrange
		var processor = new TestOrderProcessor();
		var registry = new DataProcessorRegistry([processor]);

		// Act
		var factory = registry.GetFactory("orders");

		// Assert
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void GetFactoryThrowForMissingRecordType()
	{
		// Arrange
		var processor = new TestOrderProcessor();
		var registry = new DataProcessorRegistry([processor]);

		// Act & Assert
		Should.Throw<MissingDataProcessorException>(() => registry.GetFactory("nonexistent"));
	}

	[Fact]
	public void TryGetFactoryReturnFalseForMissingRecordType()
	{
		// Arrange
		var processor = new TestOrderProcessor();
		var registry = new DataProcessorRegistry([processor]);

		// Act
		var found = registry.TryGetFactory("nonexistent", out var factory);

		// Assert
		found.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenNullProcessorsCollection()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DataProcessorRegistry(null!));
	}

	[Fact]
	public void CreateWithEmptyProcessorsCollection()
	{
		// Act - should not throw
		var registry = new DataProcessorRegistry(Array.Empty<IDataProcessor>());

		// Assert
		registry.TryGetFactory("anything", out _).ShouldBeFalse();
	}

	[Fact]
	public void RegisterMultipleProcessors()
	{
		// Arrange
		var registry = new DataProcessorRegistry([new TestOrderProcessor(), new TestInvoiceProcessor()]);

		// Act & Assert
		registry.TryGetFactory("orders", out _).ShouldBeTrue();
		registry.TryGetFactory("invoices", out _).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenDuplicateRecordTypeRegistered()
	{
		// Arrange
		var processor1 = new TestOrderProcessor();
		var processor2 = new TestOrderProcessor();

		// Act & Assert
		Should.Throw<MultipleDataProcessorException>(() =>
			new DataProcessorRegistry([processor1, processor2]));
	}

	[Fact]
	public void BeCaseInsensitiveForRecordTypes()
	{
		// Arrange
		var registry = new DataProcessorRegistry([new TestOrderProcessor()]);

		// Act & Assert - "orders" was registered, "ORDERS" should also match
		registry.TryGetFactory("ORDERS", out _).ShouldBeTrue();
		registry.TryGetFactory("Orders", out _).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenTryGetFactoryRecordTypeIsNullOrEmpty()
	{
		// Arrange
		var registry = new DataProcessorRegistry([new TestOrderProcessor()]);

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.TryGetFactory(null!, out _));
		Should.Throw<ArgumentException>(() => registry.TryGetFactory("", out _));
	}

	[DataTaskRecordType("orders")]
	private sealed class TestOrderProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public void Dispose() { }
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	[DataTaskRecordType("invoices")]
	private sealed class TestInvoiceProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public void Dispose() { }
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
