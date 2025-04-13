using Excalibur.DataAccess.DataProcessing;
using Excalibur.DataAccess.DataProcessing.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Functional.DataAccess.DataProcessing.Exceptions;

public class InvalidDataProcessorExceptionShould
{
	[Fact]
	public void ConstructorShouldSetDefaultMessage()
	{
		// Act
		var exception = new InvalidDataProcessorException();

		// Assert
		exception.Message.ShouldBe(InvalidDataProcessorException.DefaultMessage);
	}

	[Fact]
	public void ConstructorShouldIncludeProcessorTypeInMessage()
	{
		// Arrange
		var processorType = typeof(TestProcessor);

		// Act
		var exception = new InvalidDataProcessorException(processorType);

		// Assert
		exception.Message.ShouldContain(processorType.FullName!);
	}

	private sealed class TestProcessor : IDataProcessor
	{
		public Task<long> RunAsync(long completedCount, UpdateCompletedCount updateCompletedCount,
			CancellationToken cancellationToken = default) => Task.FromResult(0L);

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void Dispose()
		{
		}
	}
}
