// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Exceptions;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessorRegistry"/>.
/// </summary>
[UnitTest]
public sealed class DataProcessorRegistryShould : UnitTestBase
{
	[DataTaskRecordType("TestRecord")]
	private sealed class TestProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose() { }
	}

	[DataTaskRecordType("AnotherRecord")]
	private sealed class AnotherProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose() { }
	}

	[DataTaskRecordType("TestRecord")]
	private sealed class DuplicateProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose() { }
	}

	private sealed class NoAttributeProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose() { }
	}

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy test setup
	[Fact]
	public void TryGetFactory_ReturnsTrueForRegisteredType()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor());

		// Act
		var found = registry.TryGetFactory("TestRecord", out var factory);

		// Assert
		found.ShouldBeTrue();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void TryGetFactory_ReturnsFalseForUnregisteredType()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor());

		// Act
		var found = registry.TryGetFactory("NonExistentRecord", out _);

		// Assert
		found.ShouldBeFalse();
	}

	[Fact]
	public void TryGetFactory_IsCaseInsensitive()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor());

		// Act
		var found = registry.TryGetFactory("TESTRECORD", out var factory);

		// Assert
		found.ShouldBeTrue();
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void TryGetFactory_ThrowsOnNullRecordType()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor());

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.TryGetFactory(null!, out _));
	}

	[Fact]
	public void TryGetFactory_ThrowsOnEmptyRecordType()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor());

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.TryGetFactory(string.Empty, out _));
	}

	[Fact]
	public void GetFactory_ReturnsFactoryForRegisteredType()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor());

		// Act
		var factory = registry.GetFactory("TestRecord");

		// Assert
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void GetFactory_ThrowsMissingDataProcessorException_ForUnregisteredType()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor());

		// Act & Assert
		Should.Throw<MissingDataProcessorException>(() => registry.GetFactory("NonExistentRecord"));
	}

	[Fact]
	public void RegisterMultipleProcessors()
	{
		// Arrange
		var registry = CreateRegistry(new TestProcessor(), new AnotherProcessor());

		// Act & Assert
		registry.TryGetFactory("TestRecord", out _).ShouldBeTrue();
		registry.TryGetFactory("AnotherRecord", out _).ShouldBeTrue();
	}

	[Fact]
	public void ThrowMultipleDataProcessorException_ForDuplicateRecordTypes()
	{
		// Act & Assert
		Should.Throw<MultipleDataProcessorException>(() =>
			CreateRegistry(new TestProcessor(), new DuplicateProcessor()));
	}

	[Fact]
	public void ThrowInvalidDataProcessorException_ForProcessorWithoutRecordType()
	{
		// Act & Assert
		Should.Throw<InvalidDataProcessorException>(() =>
			CreateRegistry(new NoAttributeProcessor()));
	}

	[Fact]
	public void Throw_WhenProcessorsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessorRegistry(null!));
	}

	[Fact]
	public void AcceptEmptyProcessorCollection()
	{
		// Arrange & Act
		var registry = CreateRegistry();

		// Assert — no exception, but no factories registered
		registry.TryGetFactory("anything", out _).ShouldBeFalse();
	}
#pragma warning restore CA2012

	private static DataProcessorRegistry CreateRegistry(params IDataProcessor[] processors)
		=> new(processors);
}
