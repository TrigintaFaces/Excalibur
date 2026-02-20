// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for the <see cref="MessageTypeResolverExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class MessageTypeResolverExtensionsShould
{
	[Fact]
	public void CanResolve_Should_ReturnTrue_WhenTypeExists()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		A.CallTo(() => resolver.ResolveType("OrderCreated")).Returns(typeof(string));

		// Act
		var result = resolver.CanResolve("OrderCreated");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanResolve_Should_ReturnFalse_WhenTypeNotFound()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		A.CallTo(() => resolver.ResolveType("Unknown")).Returns(null);

		// Act
		var result = resolver.CanResolve("Unknown");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanResolve_Should_ThrowArgumentNullException_WhenResolverIsNull()
	{
		// Arrange
		IMessageTypeResolver resolver = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => resolver.CanResolve("test"));
	}

	[Fact]
	public async Task ResolveTypeAsync_Should_ReturnType()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		A.CallTo(() => resolver.ResolveType("OrderCreated")).Returns(typeof(int));
		using var cts = new CancellationTokenSource();

		// Act
		var result = await resolver.ResolveTypeAsync("OrderCreated", cts.Token);

		// Assert
		result.ShouldBe(typeof(int));
	}

	[Fact]
	public async Task ResolveTypeAsync_Should_ThrowOnCancellation()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => resolver.ResolveTypeAsync("test", cts.Token));
	}

	[Fact]
	public async Task GetTypeIdentifierAsync_Should_ReturnIdentifier()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		A.CallTo(() => resolver.GetTypeIdentifier(typeof(string))).Returns("System.String");
		using var cts = new CancellationTokenSource();

		// Act
		var result = await resolver.GetTypeIdentifierAsync(typeof(string), cts.Token);

		// Assert
		result.ShouldBe("System.String");
	}

	[Fact]
	public async Task GetTypeIdentifierAsync_Should_ThrowOnCancellation()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => resolver.GetTypeIdentifierAsync(typeof(string), cts.Token));
	}

	[Fact]
	public async Task CanResolveAsync_Should_ReturnTrue_WhenTypeExists()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		A.CallTo(() => resolver.ResolveType("OrderCreated")).Returns(typeof(string));
		using var cts = new CancellationTokenSource();

		// Act
		var result = await resolver.CanResolveAsync("OrderCreated", cts.Token);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task CanResolveAsync_Should_ThrowOnCancellation()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => resolver.CanResolveAsync("test", cts.Token));
	}

	[Fact]
	public async Task RegisterTypeAsync_Should_DelegateToSync()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		using var cts = new CancellationTokenSource();

		// Act
		await resolver.RegisterTypeAsync(typeof(string), "System.String", cts.Token);

		// Assert
		A.CallTo(() => resolver.RegisterType(typeof(string), "System.String"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RegisterTypeAsync_Should_ThrowOnCancellation()
	{
		// Arrange
		var resolver = A.Fake<IMessageTypeResolver>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => resolver.RegisterTypeAsync(typeof(string), "test", cts.Token));
	}
}
