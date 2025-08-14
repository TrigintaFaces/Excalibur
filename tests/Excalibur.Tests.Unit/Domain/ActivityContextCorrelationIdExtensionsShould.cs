using Excalibur.Core.Concurrency;
using Excalibur.Domain;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain;

public class ActivityContextCorrelationIdExtensionsShould
{
	[Fact]
	public void ReturnEmptyGuidWhenCorrelationIdIsNotSet()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		A.CallTo(() => context.Get(nameof(CorrelationId), A<ICorrelationId?>._)).Returns(null);

		// Act
		var result = context.CorrelationId();

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public void ReturnEmptyGuidWhenCorrelationIdValueIsNull()
	{
		// Arrange
		var context = A.Fake<IActivityContext>();
		var correlationId = A.Fake<ICorrelationId>();
		A.CallTo(() => correlationId.Value).Returns(null as Guid?);
		A.CallTo(() => context.Get(nameof(CorrelationId), A<ICorrelationId?>._)).Returns(correlationId);

		// Act
		var result = context.CorrelationId();

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public void ReturnCorrelationIdValueWhenSet()
	{
		// Arrange
		var expectedId = Guid.NewGuid();
		var context = A.Fake<IActivityContext>();
		var correlationId = A.Fake<ICorrelationId>();
		A.CallTo(() => correlationId.Value).Returns(expectedId);
		A.CallTo(() => context.Get(nameof(CorrelationId), A<ICorrelationId?>._)).Returns(correlationId);

		// Act
		var result = context.CorrelationId();

		// Assert
		result.ShouldBe(expectedId);
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext context = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.CorrelationId())
			.ParamName.ShouldBe("context");
	}
}
