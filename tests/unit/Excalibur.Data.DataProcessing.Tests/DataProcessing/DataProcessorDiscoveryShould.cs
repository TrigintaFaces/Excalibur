// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for DataProcessorDiscovery and RecordHandlerDiscovery.
/// Uses DataTaskRecordTypeAttribute to verify discovery behavior.
/// </summary>
[UnitTest]
[SuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
	Justification = "Test code uses reflection for discovery testing")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode",
	Justification = "Test code uses dynamic discovery for testing")]
public sealed class DataProcessorDiscoveryShould : UnitTestBase
{
	[Fact]
	public void DiscoverProcessors_FindsConcreteImplementations()
	{
		// Act
		var processors = DataProcessorDiscovery.DiscoverProcessors([typeof(DiscoveryAttributeProcessor).Assembly]);

		// Assert — should find at least our test processors
		processors.ShouldContain(typeof(DiscoveryAttributeProcessor));
		processors.ShouldContain(typeof(DiscoveryPropertyProcessor));
		processors.ShouldContain(typeof(DiscoveryNoRecordTypeProcessor));
	}

	[Fact]
	public void DiscoverProcessors_ExcludesAbstractAndInterfaces()
	{
		// Act
		var processors = DataProcessorDiscovery.DiscoverProcessors([typeof(IDataProcessor).Assembly]).ToList();

		// Assert — should not contain the interface itself
		processors.ShouldNotContain(typeof(IDataProcessor));
	}

	[Fact]
	public void TryGetRecordType_FindsAttributeRecordType()
	{
		// Act
		var found = DataProcessorDiscovery.TryGetRecordType(typeof(DiscoveryAttributeProcessor), out var recordType);

		// Assert
		found.ShouldBeTrue();
		recordType.ShouldBe("DiscoveryTestRecord");
	}

	[Fact]
	public void TryGetRecordType_FindsPropertyRecordType()
	{
		// Act
		var found = DataProcessorDiscovery.TryGetRecordType(typeof(DiscoveryPropertyProcessor), out var recordType);

		// Assert
		found.ShouldBeTrue();
		recordType.ShouldBe("PropertyTestRecord");
	}

	[Fact]
	public void TryGetRecordType_ReturnsFalse_WhenNoRecordType()
	{
		// Act
		var found = DataProcessorDiscovery.TryGetRecordType(typeof(DiscoveryNoRecordTypeProcessor), out var recordType);

		// Assert
		found.ShouldBeFalse();
		recordType.ShouldBeNull();
	}
}

/// <summary>
/// Unit tests for RecordHandlerDiscovery.
/// </summary>
[UnitTest]
[SuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
	Justification = "Test code uses reflection for discovery testing")]
public sealed class RecordHandlerDiscoveryShould : UnitTestBase
{
	[Fact]
	public void DiscoverHandlers_FindsRecordHandlerImplementations()
	{
		// Act
		var handlers = RecordHandlerDiscovery.DiscoverHandlers([typeof(DiscoveryStringRecordHandler).Assembly]).ToList();

		// Assert — should find our test handlers
		handlers.ShouldContain(h => h.ImplementationType == typeof(DiscoveryStringRecordHandler));
		handlers.ShouldContain(h => h.ImplementationType == typeof(DiscoveryIntRecordHandler));
	}

	[Fact]
	public void DiscoverHandlers_ReturnsCorrectInterfaceTypes()
	{
		// Act
		var handlers = RecordHandlerDiscovery.DiscoverHandlers([typeof(DiscoveryStringRecordHandler).Assembly]).ToList();

		// Assert — interface types should be generic IRecordHandler<T>
		var stringHandler = handlers.First(h => h.ImplementationType == typeof(DiscoveryStringRecordHandler));
		stringHandler.InterfaceType.ShouldBe(typeof(IRecordHandler<string>));

		var intHandler = handlers.First(h => h.ImplementationType == typeof(DiscoveryIntRecordHandler));
		intHandler.InterfaceType.ShouldBe(typeof(IRecordHandler<int>));
	}

	[Fact]
	public void TryGetRecordType_ReturnsRecordType_ForRecordHandler()
	{
		// Act
		var found = RecordHandlerDiscovery.TryGetRecordType(typeof(DiscoveryStringRecordHandler), out var recordType);

		// Assert
		found.ShouldBeTrue();
		recordType.ShouldBe(typeof(string));
	}

	[Fact]
	public void TryGetRecordType_ReturnsFalse_ForNonHandler()
	{
		// Act
		var found = RecordHandlerDiscovery.TryGetRecordType(typeof(string), out var recordType);

		// Assert
		found.ShouldBeFalse();
		recordType.ShouldBeNull();
	}
}

// ---- Test fixtures for discovery (top-level, non-nested to avoid CA1034) ----

[DataTaskRecordType("DiscoveryTestRecord")]
internal sealed class DiscoveryAttributeProcessor : IDataProcessor
{
	public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
		=> Task.FromResult(0L);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	public void Dispose() { }
}

internal sealed class DiscoveryPropertyProcessor : IDataProcessor
{
	public string RecordType => "PropertyTestRecord";

	public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
		=> Task.FromResult(0L);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	public void Dispose() { }
}

internal sealed class DiscoveryNoRecordTypeProcessor : IDataProcessor
{
	public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
		=> Task.FromResult(0L);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	public void Dispose() { }
}

internal sealed class DiscoveryStringRecordHandler : IRecordHandler<string>
{
	public Task ProcessAsync(string record, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class DiscoveryIntRecordHandler : IRecordHandler<int>
{
	public Task ProcessAsync(int record, CancellationToken cancellationToken) => Task.CompletedTask;
}
