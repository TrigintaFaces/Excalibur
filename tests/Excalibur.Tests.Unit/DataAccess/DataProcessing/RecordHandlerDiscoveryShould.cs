using Excalibur.DataAccess.DataProcessing;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class RecordHandlerDiscoveryShould
{
	[Fact]
	public void DiscoverHandlersShouldReturnValidHandlers()
	{
		// Arrange
		var assembly = typeof(TestRecordHandler).Assembly;

		// Act
		var handlers = RecordHandlerDiscovery.DiscoverHandlers([assembly]).ToList();

		// Assert
		handlers.ShouldNotBeEmpty();
		handlers.ShouldContain(tuple => tuple.InterfaceType == typeof(IRecordHandler<TestRecord>) &&
										tuple.ImplementationType == typeof(TestRecordHandler));
	}

	[Fact]
	public void TryGetRecordTypeShouldReturnRecordTypeIfHandlerIsValid()
	{
		// Act
		var result = RecordHandlerDiscovery.TryGetRecordType(typeof(TestRecordHandler), out var recordType);

		// Assert
		result.ShouldBeTrue();
		recordType.ShouldBe(typeof(TestRecord));
	}

	[Fact]
	public void TryGetRecordTypeShouldReturnFalseForNonHandlerTypes()
	{
		// Act
		var result = RecordHandlerDiscovery.TryGetRecordType(typeof(NonHandlerClass), out var recordType);

		// Assert
		result.ShouldBeFalse();
		recordType.ShouldBeNull();
	}

	// Helper test classes
	private sealed class TestRecord
	{
	}

	private sealed class TestRecordHandler : IRecordHandler<TestRecord>
	{
		public Task HandleAsync(TestRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class NonHandlerClass
	{
	}
}
