// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Depth tests for <see cref="DataRequestExtensions"/>.
/// Covers generic ResolveAsync overloads for both IDataRequest and IDocumentDataRequest.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataRequestExtensionsDepthShould
{
	[Fact]
	public async Task ResolveDataRequestWithConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var request = A.Fake<IDataRequest<IDbConnection, int>>();
		A.CallTo(() => request.ResolveAsync).Returns(new Func<IDbConnection, Task<int>>(_ => Task.FromResult(42)));

		// Act
		var result = await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenDataRequestIsNull()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		IDataRequest<IDbConnection, int>? request = null;

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => DataRequestExtensions.ResolveAsync(request!, connection, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenDataRequestConnectionIsNull()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, int>>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => DataRequestExtensions.ResolveAsync(request, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ResolveDocumentDataRequestWithConnection()
	{
		// Arrange
		var connection = new object();
		var request = A.Fake<IDocumentDataRequest<object, string>>();
		A.CallTo(() => request.ResolveAsync).Returns(new Func<object, Task<string>>(_ => Task.FromResult("doc-result")));

		// Act
		var result = await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None);

		// Assert
		result.ShouldBe("doc-result");
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenDocumentRequestIsNull()
	{
		// Arrange
		var connection = new object();
		IDocumentDataRequest<object, string>? request = null;

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => DataRequestExtensions.ResolveAsync(request!, connection, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenDocumentRequestConnectionIsNull()
	{
		// Arrange
		var request = A.Fake<IDocumentDataRequest<object, string>>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => DataRequestExtensions.ResolveAsync(request, (object)null!, CancellationToken.None));
	}

	[Fact]
	public async Task PropagateExceptionFromDataRequestResolver()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var request = A.Fake<IDataRequest<IDbConnection, int>>();
		A.CallTo(() => request.ResolveAsync)
			.Returns(new Func<IDbConnection, Task<int>>(_ => throw new InvalidOperationException("resolver failed")));

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None));
		ex.Message.ShouldBe("resolver failed");
	}

	[Fact]
	public async Task PropagateExceptionFromDocumentRequestResolver()
	{
		// Arrange
		var connection = new object();
		var request = A.Fake<IDocumentDataRequest<object, string>>();
		A.CallTo(() => request.ResolveAsync)
			.Returns(new Func<object, Task<string>>(_ => throw new InvalidOperationException("doc resolver failed")));

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None));
		ex.Message.ShouldBe("doc resolver failed");
	}
}
