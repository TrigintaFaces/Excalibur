// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="DataRequestExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "Extensions")]
public sealed class DataRequestExtensionsShould : UnitTestBase
{
	#region ResolveAsync (IDataRequest) Tests

	[Fact]
	public async Task ResolveAsync_IDataRequest_CallsResolveAsyncOnRequest()
	{
		// Arrange
		var connection = new TestConnection();
		var request = new TestDataRequest { ExpectedResult = "result" };

		// Act
		var result = await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None);

		// Assert
		result.ShouldBe("result");
	}

	[Fact]
	public async Task ResolveAsync_IDataRequest_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// Arrange
		IDataRequest<TestConnection, string> request = null!;
		var connection = new TestConnection();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None));
	}

	[Fact]
	public async Task ResolveAsync_IDataRequest_ThrowsArgumentNullException_WhenConnectionIsNull()
	{
		// Arrange
		var request = new TestDataRequest { ExpectedResult = "test" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await DataRequestExtensions.ResolveAsync(request, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ResolveAsync_IDataRequest_WorksWithDefaultCancellationToken()
	{
		// Arrange
		var connection = new TestConnection();
		var request = new TestDataRequest { ExpectedResult = "default" };

		// Act
		var result = await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None);

		// Assert
		result.ShouldBe("default");
	}

	#endregion

	#region ResolveAsync (IDocumentDataRequest) Tests

	[Fact]
	public async Task ResolveAsync_IDocumentDataRequest_CallsResolveAsyncOnRequest()
	{
		// Arrange
		var connection = new TestConnection();
		var request = new TestDocumentDataRequest { ExpectedResult = "document result" };

		// Act
		var result = await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None);

		// Assert
		result.ShouldBe("document result");
	}

	[Fact]
	public async Task ResolveAsync_IDocumentDataRequest_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// Arrange
		IDocumentDataRequest<TestConnection, string> request = null!;
		var connection = new TestConnection();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None));
	}

	[Fact]
	public async Task ResolveAsync_IDocumentDataRequest_ThrowsArgumentNullException_WhenConnectionIsNull()
	{
		// Arrange
		var request = new TestDocumentDataRequest { ExpectedResult = "test" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await DataRequestExtensions.ResolveAsync(request, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ResolveAsync_IDocumentDataRequest_WorksWithDefaultCancellationToken()
	{
		// Arrange
		var connection = new TestConnection();
		var request = new TestDocumentDataRequest { ExpectedResult = "default doc" };

		// Act
		var result = await DataRequestExtensions.ResolveAsync(request, connection, CancellationToken.None);

		// Assert
		result.ShouldBe("default doc");
	}

	#endregion

	#region Test Types

	private sealed class TestConnection;

	private sealed class TestDataRequest : IDataRequest<TestConnection, string>
	{
		public string ExpectedResult { get; init; } = string.Empty;

		public string RequestId => Guid.NewGuid().ToString();
		public string RequestType => nameof(TestDataRequest);
		public DateTimeOffset CreatedAt => DateTimeOffset.UtcNow;
		public string? CorrelationId => null;
		public IDictionary<string, object>? Metadata => null;
		public CommandDefinition Command => default;
		public DynamicParameters Parameters => new();
		public Func<TestConnection, Task<string>> ResolveAsync => _ => Task.FromResult(ExpectedResult);
	}

	private sealed class TestDocumentDataRequest : IDocumentDataRequest<TestConnection, string>
	{
		public string ExpectedResult { get; init; } = string.Empty;

		public string CollectionName { get; set; } = "TestCollection";
		public string OperationType { get; set; } = "Query";
		public IReadOnlyDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
		public IReadOnlyDictionary<string, object>? Options { get; set; }
		public Func<TestConnection, Task<string>> ResolveAsync => _ => Task.FromResult(ExpectedResult);
	}

	#endregion
}
