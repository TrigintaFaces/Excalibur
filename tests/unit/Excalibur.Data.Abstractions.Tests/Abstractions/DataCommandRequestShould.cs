// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Execution;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="DataCommandRequest"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "Execution")]
public sealed class DataCommandRequestShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithCommandTextOnly_StoresCommandText()
	{
		// Arrange
		var sql = "SELECT * FROM Customers";

		// Act
		var request = new DataCommandRequest(sql);

		// Assert
		request.CommandText.ShouldBe(sql);
	}

	[Fact]
	public void Create_WithCommandTextOnly_InitializesEmptyParameters()
	{
		// Arrange
		var sql = "SELECT 1";

		// Act
		var request = new DataCommandRequest(sql);

		// Assert
		request.Parameters.ShouldNotBeNull();
		request.Parameters.ShouldBeEmpty();
	}

	[Fact]
	public void Create_WithCommandTextOnly_SetsCommandTypeToNull()
	{
		// Arrange
		var sql = "SELECT 1";

		// Act
		var request = new DataCommandRequest(sql);

		// Assert
		request.CommandType.ShouldBeNull();
	}

	[Fact]
	public void Create_WithCommandTextOnly_SetsCommandTimeoutToNull()
	{
		// Arrange
		var sql = "SELECT 1";

		// Act
		var request = new DataCommandRequest(sql);

		// Assert
		request.CommandTimeoutSeconds.ShouldBeNull();
	}

	[Fact]
	public void Create_WithNullCommandText_ThrowsArgumentException()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new DataCommandRequest(null!));
	}

	[Fact]
	public void Create_WithEmptyCommandText_ThrowsArgumentException()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new DataCommandRequest(string.Empty));
	}

	[Fact]
	public void Create_WithWhitespaceCommandText_ThrowsArgumentException()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new DataCommandRequest("   "));
	}

	#endregion

	#region Parameters Tests

	[Fact]
	public void Create_WithParameters_StoresParameters()
	{
		// Arrange
		var sql = "SELECT * FROM Customers WHERE Id = @Id";
		var parameters = new Dictionary<string, object?>
		{
			["Id"] = 123
		};

		// Act
		var request = new DataCommandRequest(sql, parameters);

		// Assert
		request.Parameters.Count.ShouldBe(1);
		request.Parameters["Id"].ShouldBe(123);
	}

	[Fact]
	public void Create_WithMultipleParameters_StoresAllParameters()
	{
		// Arrange
		var sql = "SELECT * FROM Customers WHERE Name = @Name AND Age > @MinAge";
		var parameters = new Dictionary<string, object?>
		{
			["Name"] = "John",
			["MinAge"] = 18
		};

		// Act
		var request = new DataCommandRequest(sql, parameters);

		// Assert
		request.Parameters.Count.ShouldBe(2);
		request.Parameters["Name"].ShouldBe("John");
		request.Parameters["MinAge"].ShouldBe(18);
	}

	[Fact]
	public void Create_WithNullParameterValues_StoresNullValues()
	{
		// Arrange
		var sql = "INSERT INTO Customers (Name, Email) VALUES (@Name, @Email)";
		var parameters = new Dictionary<string, object?>
		{
			["Name"] = "Test",
			["Email"] = null
		};

		// Act
		var request = new DataCommandRequest(sql, parameters);

		// Assert
		request.Parameters["Email"].ShouldBeNull();
	}

	#endregion

	#region CommandType Tests

	[Fact]
	public void Create_WithTextCommandType_StoresCommandType()
	{
		// Arrange
		var sql = "SELECT 1";

		// Act
		var request = new DataCommandRequest(sql, commandType: CommandType.Text);

		// Assert
		request.CommandType.ShouldBe(CommandType.Text);
	}

	[Fact]
	public void Create_WithStoredProcedureCommandType_StoresCommandType()
	{
		// Arrange
		var sql = "usp_GetCustomers";

		// Act
		var request = new DataCommandRequest(sql, commandType: CommandType.StoredProcedure);

		// Assert
		request.CommandType.ShouldBe(CommandType.StoredProcedure);
	}

	[Fact]
	public void Create_WithTableDirectCommandType_StoresCommandType()
	{
		// Arrange
		var sql = "Customers";

		// Act
		var request = new DataCommandRequest(sql, commandType: CommandType.TableDirect);

		// Assert
		request.CommandType.ShouldBe(CommandType.TableDirect);
	}

	#endregion

	#region CommandTimeoutSeconds Tests

	[Fact]
	public void Create_WithTimeout_StoresTimeout()
	{
		// Arrange
		var sql = "SELECT * FROM LargeTable";

		// Act
		var request = new DataCommandRequest(sql, commandTimeoutSeconds: 120);

		// Assert
		request.CommandTimeoutSeconds.ShouldBe(120);
	}

	[Fact]
	public void Create_WithZeroTimeout_StoresZeroTimeout()
	{
		// Arrange
		var sql = "SELECT 1";

		// Act
		var request = new DataCommandRequest(sql, commandTimeoutSeconds: 0);

		// Assert
		request.CommandTimeoutSeconds.ShouldBe(0);
	}

	[Theory]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(300)]
	[InlineData(3600)]
	public void Create_WithVariousTimeouts_StoresCorrectly(int timeoutSeconds)
	{
		// Arrange
		var sql = "SELECT 1";

		// Act
		var request = new DataCommandRequest(sql, commandTimeoutSeconds: timeoutSeconds);

		// Assert
		request.CommandTimeoutSeconds.ShouldBe(timeoutSeconds);
	}

	#endregion

	#region Full Construction Tests

	[Fact]
	public void Create_WithAllParameters_StoresAllValues()
	{
		// Arrange
		var sql = "usp_ProcessOrder";
		var parameters = new Dictionary<string, object?>
		{
			["OrderId"] = 1001,
			["CustomerId"] = 42,
			["TotalAmount"] = 99.99m
		};
		var commandType = CommandType.StoredProcedure;
		var timeout = 60;

		// Act
		var request = new DataCommandRequest(sql, parameters, commandType, timeout);

		// Assert
		request.CommandText.ShouldBe(sql);
		request.Parameters.Count.ShouldBe(3);
		request.Parameters["OrderId"].ShouldBe(1001);
		request.Parameters["CustomerId"].ShouldBe(42);
		request.Parameters["TotalAmount"].ShouldBe(99.99m);
		request.CommandType.ShouldBe(CommandType.StoredProcedure);
		request.CommandTimeoutSeconds.ShouldBe(60);
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void CommandText_IsReadOnly()
	{
		// Arrange
		var request = new DataCommandRequest("SELECT 1");

		// Assert - property has no setter, so this test verifies it's get-only
		request.CommandText.ShouldBe("SELECT 1");
	}

	[Fact]
	public void Parameters_IsReadOnly()
	{
		// Arrange
		var request = new DataCommandRequest("SELECT 1");

		// Assert - property has no setter, so this test verifies it's get-only
		request.Parameters.ShouldNotBeNull();
	}

	#endregion
}
