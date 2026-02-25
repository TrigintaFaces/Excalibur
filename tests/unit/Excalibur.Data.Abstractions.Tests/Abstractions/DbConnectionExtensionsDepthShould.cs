// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Depth tests for <see cref="DbConnectionExtensions"/>.
/// Covers ResolveAsync, Ready, edge cases, and error wrapping.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DbConnectionExtensionsDepthShould
{
	[Fact]
	public async Task ResolveAsyncReturnResult()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var dataRequest = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => dataRequest.ResolveAsync).Returns(new Func<IDbConnection, Task<string>>(_ => Task.FromResult("result")));

		// Act
		var result = await connection.ResolveAsync(dataRequest);

		// Assert
		result.ShouldBe("result");
	}

	[Fact]
	public async Task ResolveAsyncThrowOperationFailedExceptionOnNonApiException()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var dataRequest = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => dataRequest.ResolveAsync)
			.Returns(new Func<IDbConnection, Task<string>>(_ => throw new InvalidOperationException("test error")));

		// Act & Assert
		var ex = await Should.ThrowAsync<OperationFailedException>(
			() => connection.ResolveAsync(dataRequest));
		ex.InnerException.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task ResolveAsyncPropagateApiExceptionDirectly()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var dataRequest = A.Fake<IDataRequest<IDbConnection, string>>();
		A.CallTo(() => dataRequest.ResolveAsync)
			.Returns(new Func<IDbConnection, Task<string>>(_ => throw new ApiException("api error")));

		// Act & Assert
		await Should.ThrowAsync<ApiException>(() => connection.ResolveAsync(dataRequest));
	}

	[Fact]
	public async Task ResolveAsyncThrowArgumentNullExceptionWhenRequestIsNull()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => connection.ResolveAsync<string>(null!));
	}

	[Fact]
	public void ReadyReturnConnectionWhenOpen()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
	}

	[Fact]
	public void ReadyOpenClosedConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Closed);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReadyCloseAndReopenBrokenConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Broken);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		A.CallTo(() => connection.Close()).MustHaveHappenedOnceExactly();
		A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReadyThrowInvalidOperationExceptionWhenConnectionDisposed()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Broken);
		A.CallTo(() => connection.Open()).Throws(new ObjectDisposedException("connection"));

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => connection.Ready());
	}

	[Fact]
	public void ReadyThrowArgumentNullExceptionWhenConnectionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => DbConnectionExtensions.Ready(null!));
	}

	[Fact]
	public void ReadyNotReopenExecutingConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Executing);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
	}

	[Fact]
	public void ReadyNotReopenFetchingConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Fetching);

		// Act
		var result = connection.Ready();

		// Assert
		result.ShouldBeSameAs(connection);
		A.CallTo(() => connection.Open()).MustNotHaveHappened();
	}
}
