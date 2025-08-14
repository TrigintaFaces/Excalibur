using System.Data;
using System.Data.Common;

using Excalibur.DataAccess;

using FakeItEasy;

using Microsoft.Data.SqlClient;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class DbTimeoutsExtensionsShould
{
	private readonly IDbCommand _command;

	public DbTimeoutsExtensionsShould()
	{
		_command = A.Fake<IDbCommand>();
	}

	[Fact]
	public void ApplyRegularTimeoutToCommand()
	{
		// Act
		var result = _command.WithRegularTimeout();

		// Assert
		result.CommandTimeout.ShouldBe(DbTimeouts.RegularTimeoutSeconds);
		result.ShouldBeSameAs(_command); // Ensure the same command instance is returned
	}

	[Fact]
	public void ApplyLongRunningTimeoutToCommand()
	{
		// Act
		var result = _command.WithLongRunningTimeout();

		// Assert
		result.CommandTimeout.ShouldBe(DbTimeouts.LongRunningTimeoutSeconds);
		result.ShouldBeSameAs(_command); // Ensure the same command instance is returned
	}

	[Fact]
	public void ApplyExtraLongRunningTimeoutToCommand()
	{
		// Act
		var result = _command.WithExtraLongRunningTimeout();

		// Assert
		result.CommandTimeout.ShouldBe(DbTimeouts.ExtraLongRunningTimeoutSeconds);
		result.ShouldBeSameAs(_command); // Ensure the same command instance is returned
	}

	[Fact]
	public void ApplyCustomTimeoutToCommand()
	{
		// Arrange
		const int customTimeout = 300;

		// Act
		var result = _command.WithTimeout(customTimeout);

		// Assert
		result.CommandTimeout.ShouldBe(customTimeout);
		result.ShouldBeSameAs(_command); // Ensure the same command instance is returned
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenCommandIsNull()
	{
		// Arrange
		IDbCommand nullCommand = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => nullCommand.WithRegularTimeout());
		Should.Throw<ArgumentNullException>(() => nullCommand.WithLongRunningTimeout());
		Should.Throw<ArgumentNullException>(() => nullCommand.WithExtraLongRunningTimeout());
		Should.Throw<ArgumentNullException>(() => nullCommand.WithTimeout(30));
	}

	[Fact]
	public void ThrowArgumentOutOfRangeExceptionWhenCustomTimeoutIsNegative()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => _command.WithTimeout(-1));
	}
}
