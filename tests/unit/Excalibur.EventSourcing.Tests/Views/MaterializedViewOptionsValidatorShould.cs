using Excalibur.EventSourcing.Views;

namespace Excalibur.EventSourcing.Tests.Views;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MaterializedViewOptionsValidatorShould
{
	private readonly MaterializedViewOptionsValidator _sut = new();

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void SucceedWithValidDefaults()
	{
		// Arrange
		var options = new MaterializedViewOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(10001)]
	[InlineData(int.MinValue)]
	public void FailWhenBatchSizeIsOutOfRange(int batchSize)
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = batchSize };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchSize");
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(10000)]
	public void SucceedWithValidBatchSize(int batchSize)
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchSize = batchSize };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenBatchDelayIsNegative()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchDelay = TimeSpan.FromSeconds(-1) };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchDelay");
	}

	[Fact]
	public void SucceedWhenBatchDelayIsZero()
	{
		// Arrange
		var options = new MaterializedViewOptions { BatchDelay = TimeSpan.Zero };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReportMultipleFailures()
	{
		// Arrange
		var options = new MaterializedViewOptions
		{
			BatchSize = 0,
			BatchDelay = TimeSpan.FromSeconds(-1)
		};

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchSize");
		result.FailureMessage.ShouldContain("BatchDelay");
	}
}
