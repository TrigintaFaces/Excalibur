// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Depth tests for <see cref="DataRequestBase{TConnection, TModel}"/>.
/// Covers CreateCommand, property initialization, metadata, correlation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataRequestBaseDepthShould
{
	[Fact]
	public void GenerateUniqueRequestIdOnConstruction()
	{
		// Arrange & Act
		var request1 = new TestDataRequest();
		var request2 = new TestDataRequest();

		// Assert
		request1.RequestId.ShouldNotBeNullOrWhiteSpace();
		request2.RequestId.ShouldNotBeNullOrWhiteSpace();
		request1.RequestId.ShouldNotBe(request2.RequestId);
	}

	[Fact]
	public void ReturnTypeNameForRequestType()
	{
		// Arrange & Act
		var request = new TestDataRequest();

		// Assert
		request.RequestType.ShouldBe("TestDataRequest");
	}

	[Fact]
	public void SetCreatedAtToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var request = new TestDataRequest();

		// Assert
		var after = DateTimeOffset.UtcNow;
		request.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		request.CreatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveNullCorrelationIdByDefault()
	{
		// Arrange & Act
		var request = new TestDataRequest();

		// Assert
		request.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCorrelationId()
	{
		// Arrange
		var request = new TestDataRequest();

		// Act
		request.CorrelationId = "corr-123";

		// Assert
		request.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void HaveNullMetadataByDefault()
	{
		// Arrange & Act
		var request = new TestDataRequest();

		// Assert
		request.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMetadata()
	{
		// Arrange
		var request = new TestDataRequest();
		var metadata = new Dictionary<string, object> { ["key"] = "value" };

		// Act
		request.Metadata = metadata;

		// Assert
		request.Metadata.ShouldNotBeNull();
		request.Metadata["key"].ShouldBe("value");
	}

	[Fact]
	public void HaveDefaultDynamicParameters()
	{
		// Arrange & Act
		var request = new TestDataRequest();

		// Assert
		request.Parameters.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCommandWithRequiredTextOnly()
	{
		// Arrange
		var request = new TestDataRequest();

		// Act
		var command = request.CallCreateCommand("SELECT 1");

		// Assert
		command.CommandText.ShouldBe("SELECT 1");
	}

	[Fact]
	public void CreateCommandWithCustomParameters()
	{
		// Arrange
		var request = new TestDataRequest();
		var parameters = new DynamicParameters();
		parameters.Add("@Id", 42);

		// Act
		var command = request.CallCreateCommand("SELECT * FROM T WHERE Id = @Id", parameters);

		// Assert
		command.CommandText.ShouldBe("SELECT * FROM T WHERE Id = @Id");
		request.Parameters.ShouldBeSameAs(parameters);
	}

	[Fact]
	public void CreateCommandWithTimeout()
	{
		// Arrange
		var request = new TestDataRequest();

		// Act
		var command = request.CallCreateCommand("SELECT 1", commandTimeout: 120);

		// Assert
		command.CommandTimeout.ShouldBe(120);
	}

	[Fact]
	public void CreateCommandWithCommandType()
	{
		// Arrange
		var request = new TestDataRequest();

		// Act
		var command = request.CallCreateCommand("sp_GetUser", commandType: CommandType.StoredProcedure);

		// Assert
		command.CommandType.ShouldBe(CommandType.StoredProcedure);
	}

	[Fact]
	public void ThrowWhenCommandTextIsNull()
	{
		// Arrange
		var request = new TestDataRequest();

		// Act & Assert
		Should.Throw<ArgumentException>(() => request.CallCreateCommand(null!));
	}

	[Fact]
	public void ThrowWhenCommandTextIsEmpty()
	{
		// Arrange
		var request = new TestDataRequest();

		// Act & Assert
		Should.Throw<ArgumentException>(() => request.CallCreateCommand(""));
	}

	[Fact]
	public void ThrowWhenCommandTextIsWhitespace()
	{
		// Arrange
		var request = new TestDataRequest();

		// Act & Assert
		Should.Throw<ArgumentException>(() => request.CallCreateCommand("   "));
	}

	[Fact]
	public void PreserveExistingParametersWhenNullPassed()
	{
		// Arrange
		var request = new TestDataRequest();
		var originalParams = request.Parameters;

		// Act
		_ = request.CallCreateCommand("SELECT 1", parameters: null);

		// Assert
		request.Parameters.ShouldBeSameAs(originalParams);
	}

	/// <summary>
	/// Concrete test implementation of DataRequestBase.
	/// </summary>
	private sealed class TestDataRequest : DataRequestBase<IDbConnection, string>
	{
		public CommandDefinition CallCreateCommand(
			string commandText,
			DynamicParameters? parameters = null,
			IDbTransaction? transaction = null,
			int? commandTimeout = null,
			CommandType? commandType = null,
			CancellationToken cancellationToken = default)
		{
			return CreateCommand(commandText, parameters, transaction, commandTimeout, commandType, cancellationToken);
		}
	}
}
