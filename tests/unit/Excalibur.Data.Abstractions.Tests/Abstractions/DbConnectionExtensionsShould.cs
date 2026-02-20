// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

using FakeItEasy;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="DbConnectionExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "Extensions")]
public sealed class DbConnectionExtensionsShould : UnitTestBase
{
	#region ResolveAsync Tests

	[Fact]
	public async Task ResolveAsync_CallsDataRequestResolveAsync()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var request = new TestDataRequest { ExpectedResult = "result" };

		// Act
		var result = await connection.ResolveAsync(request);

		// Assert
		result.ShouldBe("result");
	}

	[Fact]
	public async Task ResolveAsync_ThrowsArgumentNullException_WhenDataRequestIsNull()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await connection.ResolveAsync<string>(null!));
	}

	[Fact]
	public async Task ResolveAsync_ThrowsOperationFailedException_OnNonApiException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var request = new ThrowingDataRequest { ExceptionToThrow = new InvalidOperationException("Database error") };

		// Act & Assert
		var exception = await Should.ThrowAsync<OperationFailedException>(async () =>
			await connection.ResolveAsync(request));

		exception.InnerException.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task ResolveAsync_PropagatesApiException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var apiException = new ApiException(404, "Not Found", null);
		var request = new ThrowingDataRequest { ExceptionToThrow = apiException };

		// Act & Assert
		var exception = await Should.ThrowAsync<ApiException>(async () =>
			await connection.ResolveAsync(request));

		exception.ShouldBe(apiException);
	}

	[Fact]
	public async Task ResolveAsync_ReturnsCorrectModelType()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var expected = new TestCustomer { Id = 1, Name = "John" };
		var request = new TypedTestDataRequest<TestCustomer> { ExpectedResult = expected };

		// Act
		var result = await connection.ResolveAsync(request);

		// Assert
		result.ShouldBe(expected);
		result.Id.ShouldBe(1);
		result.Name.ShouldBe("John");
	}

	#endregion

	#region Ready Tests

	[Fact]
	public void Ready_ThrowsArgumentNullException_WhenConnectionIsNull()
	{
		// Arrange
		IDbConnection connection = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => connection.Ready());
	}

	[Fact]
	public void Ready_OpensConnection_WhenClosed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Closed);

		// Act
		var result = connection.Ready();

		// Assert
		A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
		result.ShouldBe(connection);
	}

	[Fact]
	public void Ready_ClosesAndReopens_WhenBroken()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Broken);

		// Act
		var result = connection.Ready();

		// Assert
		A.CallTo(() => connection.Close()).MustHaveHappenedOnceExactly();
		A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
		result.ShouldBe(connection);
	}

	[Fact]
	public void Ready_DoesNotOpen_WhenAlreadyOpen()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var result = connection.Ready();

		// Assert
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
		result.ShouldBe(connection);
	}

	[Fact]
	public void Ready_DoesNotOpen_WhenConnecting()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Connecting);

		// Act
		var result = connection.Ready();

		// Assert
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
		result.ShouldBe(connection);
	}

	[Fact]
	public void Ready_DoesNotOpen_WhenExecuting()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Executing);

		// Act
		var result = connection.Ready();

		// Assert
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
		result.ShouldBe(connection);
	}

	[Fact]
	public void Ready_DoesNotOpen_WhenFetching()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Fetching);

		// Act
		var result = connection.Ready();

		// Assert
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
		result.ShouldBe(connection);
	}

	[Fact]
	public void Ready_ThrowsInvalidOperationException_WhenConnectionDisposed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Closed);
		A.CallTo(() => connection.Open()).Throws(new ObjectDisposedException("Connection"));

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => connection.Ready())
			.Message.ShouldContain("disposed");
	}

	[Fact]
	public void Ready_ReturnsConnection_ForFluentChaining()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
	}

	#endregion

	#region Test Types

	private sealed class TestCustomer
	{
		public int Id { get; init; }
		public string Name { get; init; } = string.Empty;
	}

	private sealed class TestDataRequest : IDataRequest<IDbConnection, string>
	{
		public string ExpectedResult { get; init; } = string.Empty;

		public string RequestId => Guid.NewGuid().ToString();
		public string RequestType => nameof(TestDataRequest);
		public DateTimeOffset CreatedAt => DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
		public CommandDefinition Command => default;
		public DynamicParameters Parameters => new();
		public Func<IDbConnection, Task<string>> ResolveAsync => _ => Task.FromResult(ExpectedResult);
	}

	private sealed class ThrowingDataRequest : IDataRequest<IDbConnection, string>
	{
		public Exception ExceptionToThrow { get; init; } = new InvalidOperationException();

		public string RequestId => Guid.NewGuid().ToString();
		public string RequestType => nameof(ThrowingDataRequest);
		public DateTimeOffset CreatedAt => DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
		public CommandDefinition Command => default;
		public DynamicParameters Parameters => new();
		public Func<IDbConnection, Task<string>> ResolveAsync => _ => throw ExceptionToThrow;
	}

	private sealed class TypedTestDataRequest<T> : IDataRequest<IDbConnection, T>
	{
		public T ExpectedResult { get; init; } = default!;

		public string RequestId => Guid.NewGuid().ToString();
		public string RequestType => nameof(TypedTestDataRequest<T>);
		public DateTimeOffset CreatedAt => DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
		public CommandDefinition Command => default;
		public DynamicParameters Parameters => new();
		public Func<IDbConnection, Task<T>> ResolveAsync => _ => Task.FromResult(ExpectedResult);
	}

	#endregion
}
