using Excalibur.DataAccess.DataProcessing;
using Excalibur.DataAccess.DataProcessing.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class DataProcessorRegistryShould
{
	[Fact]
	public void GetFactorySuccessfully()
	{
		using var processor = new ValidFakeProcessor();
		var registry = new DataProcessorRegistry([processor]);

		var found = registry.TryGetFactory("ValidType", out var factory);

		found.ShouldBeTrue();
		_ = factory.ShouldNotBeNull();
	}

	[Fact]
	public void GetFactoryThrowsForUnknownType()
	{
		var registry = new DataProcessorRegistry([]);
		_ = Should.Throw<MissingDataProcessorException>(() =>
		{
			_ = registry.GetFactory("UnknownType");
		});
	}

	[Fact]
	public void ThrowForMissingRecordType()
	{
		// Arrange
		var processors = new List<IDataProcessor> { new FakeProcessorMissingRecordType() };

		// Act & Assert
		_ = Should.Throw<InvalidDataProcessorException>(() => new DataProcessorRegistry(processors));
	}

	[Fact]
	public void ThrowIfProcessorNotFound()
	{
		// Arrange
		var processors = new List<IDataProcessor>();
		var registry = new DataProcessorRegistry(processors);

		// Act & Assert
		_ = Should.Throw<MissingDataProcessorException>(() => registry.GetFactory("UnknownType"));
	}

	[Fact]
	public void ThrowExceptionForNullProcessors()
	{
		_ = Should.Throw<ArgumentNullException>(() => new DataProcessorRegistry(null!));
	}

	[Fact]
	public void ThrowExceptionForProcessorWithoutRecordType()
	{
		var processors = new List<IDataProcessor> { new FakeProcessorMissingRecordType() };
		_ = Should.Throw<InvalidDataProcessorException>(() => new DataProcessorRegistry(processors));
	}

	[Fact]
	public void ThrowExceptionForDuplicateProcessorRecordTypes()
	{
		// Arrange
		var processor1 = new ValidFakeProcessor();
		var processor2 = new ValidFakeProcessor();

		var processors = new List<IDataProcessor> { processor1, processor2 };

		// Act & Assert
		_ = Should.Throw<MultipleDataProcessorException>(() => new DataProcessorRegistry(processors));
	}

	[DataTaskRecordType("ValidType")]
	private sealed class ValidFakeProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}

	private sealed class DuplicateFakeProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}

	private sealed class FakeProcessorMissingRecordType : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}
}
