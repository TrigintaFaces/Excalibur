using Excalibur.DataAccess.DataProcessing;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class DataProcessorDiscoveryShould
{
	private interface IFakeProcessor : IDataProcessor
	{
	}

	[Fact]
	public void DiscoverProcessorsShouldReturnValidProcessors()
	{
		// Act
		var result = DataProcessorDiscovery.DiscoverProcessors([typeof(FakeProcessorWithAttribute).Assembly]).ToArray();

		// Assert
		result.ShouldContain(typeof(FakeProcessorWithAttribute));
		result.ShouldNotContain(typeof(IFakeProcessor));
		result.ShouldNotContain(typeof(AbstractProcessor));
		result.ShouldNotContain(typeof(UnrelatedClass));
	}

	[Fact]
	public void TryGetRecordTypeShouldReturnTrueFromAttribute()
	{
		// Act
		var success = DataProcessorDiscovery.TryGetRecordType(typeof(FakeProcessorWithAttribute), out var recordType);

		// Assert
		success.ShouldBeTrue();
		recordType.ShouldBe("FakeTypeAttribute");
	}

	[Fact]
	public void TryGetRecordTypeShouldReturnTrueFromProperty()
	{
		// Act
		var success = DataProcessorDiscovery.TryGetRecordType(typeof(FakeProcessorWithProperty), out var recordType);

		// Assert
		success.ShouldBeTrue();
		recordType.ShouldBe("FakeTypeProperty");
	}

	[Fact]
	public void TryGetRecordTypeShouldReturnFalseIfNoAttributeOrProperty()
	{
		// Act
		var success = DataProcessorDiscovery.TryGetRecordType(typeof(FakeProcessorWithNone), out var recordType);

		// Assert
		success.ShouldBeFalse();
		recordType.ShouldBeNull();
	}

	[Fact]
	public void TryGetRecordTypeShouldReturnFalseForWrongPropertyType()
	{
		// Act
		var success = DataProcessorDiscovery.TryGetRecordType(typeof(FakeProcessorWithWrongPropertyType), out var recordType);

		// Assert
		success.ShouldBeFalse();
		recordType.ShouldBeNull();
	}

	// ======== Test Stubs ==========

	[DataTaskRecordType("FakeTypeAttribute")]
	private sealed class FakeProcessorWithAttribute : IFakeProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}

	private sealed class FakeProcessorWithProperty : IDataProcessor
	{
		public string RecordType => "FakeTypeProperty";

		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}

	private sealed class FakeProcessorWithNone : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}

	private sealed class FakeProcessorWithWrongPropertyType : IDataProcessor
	{
		public int RecordType => 123;

		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}

	private abstract class AbstractProcessor : IDataProcessor
	{
		public abstract Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount,
			CancellationToken cancellationToken);

		public abstract ValueTask DisposeAsync();

		public abstract void Dispose();
	}

	private sealed class UnrelatedClass
	{
	}
}
