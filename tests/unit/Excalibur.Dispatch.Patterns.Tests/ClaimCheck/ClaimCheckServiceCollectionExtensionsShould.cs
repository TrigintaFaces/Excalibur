// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckServiceCollectionExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
[Trait("Feature", "ClaimCheck")]
public sealed class ClaimCheckServiceCollectionExtensionsShould
{
	#region AddClaimCheck Tests

	[Fact]
	public void RegisterClaimCheckProvider_WhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>();

		// Assert
		var provider = services.BuildServiceProvider();
		var claimCheckProvider = provider.GetService<IClaimCheckProvider>();
		claimCheckProvider.ShouldNotBeNull();
		claimCheckProvider.ShouldBeOfType<FakeClaimCheckProvider>();
	}

	[Fact]
	public void RegisterNamingStrategy_WhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>();

		// Assert
		var provider = services.BuildServiceProvider();
		var namingStrategy = provider.GetService<IClaimCheckNamingStrategy>();
		namingStrategy.ShouldNotBeNull();
		namingStrategy.ShouldBeOfType<DefaultClaimCheckNamingStrategy>();
	}

	[Fact]
	public void ConfigureOptions_WhenConfigureActionProvided()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>(options =>
		{
			options.PayloadThreshold = 2048;
			options.DefaultTtl = TimeSpan.FromHours(2);
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<ClaimCheckOptions>>();
		options.ShouldNotBeNull();
		options.Value.PayloadThreshold.ShouldBe(2048);
		options.Value.DefaultTtl.ShouldBe(TimeSpan.FromHours(2));
	}

	[Fact]
	public void NotConfigureOptions_WhenConfigureActionIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>(configureOptions: null);

		// Assert
		var provider = services.BuildServiceProvider();
		var claimCheckProvider = provider.GetService<IClaimCheckProvider>();
		claimCheckProvider.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterCleanupService_WhenEnableCleanupIsTrue()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>(enableCleanup: true);

		// Assert
		var provider = services.BuildServiceProvider();
		var hostedServices = provider.GetServices<IHostedService>();
		hostedServices.ShouldContain(s => s is ClaimCheckCleanupService);
	}

	[Fact]
	public void NotRegisterCleanupService_WhenEnableCleanupIsFalse()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>(enableCleanup: false);

		// Assert
		var provider = services.BuildServiceProvider();
		var hostedServices = provider.GetServices<IHostedService>();
		hostedServices.ShouldNotContain(s => s is ClaimCheckCleanupService);
	}

	[Fact]
	public void ReturnServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddClaimCheck<FakeClaimCheckProvider>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void UseTryAddSingleton_SoExistingProviderIsNotOverwritten()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IClaimCheckProvider, AnotherFakeProvider>();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>();

		// Assert
		var provider = services.BuildServiceProvider();
		var claimCheckProvider = provider.GetService<IClaimCheckProvider>();
		claimCheckProvider.ShouldBeOfType<AnotherFakeProvider>();
	}

	[Fact]
	public void UseTryAddSingleton_SoExistingNamingStrategyIsNotOverwritten()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IClaimCheckNamingStrategy, CustomNamingStrategy>();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>();

		// Assert
		var provider = services.BuildServiceProvider();
		var namingStrategy = provider.GetService<IClaimCheckNamingStrategy>();
		namingStrategy.ShouldBeOfType<CustomNamingStrategy>();
	}

	#endregion

	#region Test Helpers

	private sealed class FakeClaimCheckProvider : IClaimCheckProvider
	{
		public Task<ClaimCheckReference> StoreAsync(byte[] payload, CancellationToken cancellationToken, ClaimCheckMetadata? metadata = null)
			=> Task.FromResult(new ClaimCheckReference { Id = "fake-claim" });

		public Task<byte[]> RetrieveAsync(ClaimCheckReference reference, CancellationToken cancellationToken = default)
			=> Task.FromResult(Array.Empty<byte>());

		public Task<bool> DeleteAsync(ClaimCheckReference reference, CancellationToken cancellationToken = default)
			=> Task.FromResult(true);

		public bool ShouldUseClaimCheck(byte[] payload) => payload.Length > 256 * 1024;
	}

	private sealed class AnotherFakeProvider : IClaimCheckProvider
	{
		public Task<ClaimCheckReference> StoreAsync(byte[] payload, CancellationToken cancellationToken, ClaimCheckMetadata? metadata = null)
			=> Task.FromResult(new ClaimCheckReference { Id = "another-fake" });

		public Task<byte[]> RetrieveAsync(ClaimCheckReference reference, CancellationToken cancellationToken = default)
			=> Task.FromResult(Array.Empty<byte>());

		public Task<bool> DeleteAsync(ClaimCheckReference reference, CancellationToken cancellationToken = default)
			=> Task.FromResult(true);

		public bool ShouldUseClaimCheck(byte[] payload) => payload.Length > 512 * 1024;
	}

	private sealed class CustomNamingStrategy : IClaimCheckNamingStrategy
	{
		public string GenerateId(ClaimCheckMetadata? metadata) => $"custom-{Guid.NewGuid()}";

		public string GenerateStoragePath(string claimCheckId, ClaimCheckMetadata? metadata) => $"custom/{claimCheckId}";
	}

	#endregion
}
