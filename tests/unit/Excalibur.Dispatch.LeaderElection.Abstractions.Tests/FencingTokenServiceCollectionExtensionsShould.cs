// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="FencingTokenServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FencingTokenServiceCollectionExtensionsShould : UnitTestBase
{
	#region AddFencingTokenSupport<TProvider> Tests

	[Fact]
	public void AddFencingTokenSupport_Generic_WithNullServices_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => FencingTokenServiceCollectionExtensions.AddFencingTokenSupport<TestFencingTokenProvider>(null!));
	}

	[Fact]
	public void AddFencingTokenSupport_Generic_RegistersProviderAndMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFencingTokenSupport<TestFencingTokenProvider>();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IFencingTokenProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(FencingTokenMiddleware));
	}

	[Fact]
	public void AddFencingTokenSupport_Generic_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFencingTokenSupport<TestFencingTokenProvider>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddFencingTokenSupport_Generic_CalledTwice_UsesFirst()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — TryAdd means second call is a no-op
		services.AddFencingTokenSupport<TestFencingTokenProvider>();
		services.AddFencingTokenSupport<TestFencingTokenProvider>();

		// Assert — Should only register once
		services.Count(sd => sd.ServiceType == typeof(IFencingTokenProvider)).ShouldBe(1);
	}

	#endregion

	#region AddFencingTokenSupport(instance) Tests

	[Fact]
	public void AddFencingTokenSupport_Instance_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		var provider = A.Fake<IFencingTokenProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => FencingTokenServiceCollectionExtensions.AddFencingTokenSupport(null!, provider));
	}

	[Fact]
	public void AddFencingTokenSupport_Instance_WithNullProvider_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => services.AddFencingTokenSupport(null!));
	}

	[Fact]
	public void AddFencingTokenSupport_Instance_RegistersProviderAndMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var provider = A.Fake<IFencingTokenProvider>();

		// Act
		services.AddFencingTokenSupport(provider);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IFencingTokenProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(FencingTokenMiddleware));
	}

	#endregion

	#region Test Helpers

	private sealed class TestFencingTokenProvider : IFencingTokenProvider
	{
		public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken) =>
			new(42L);

		public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken) =>
			new(true);

		public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken) =>
			new(43L);
	}

	#endregion
}
