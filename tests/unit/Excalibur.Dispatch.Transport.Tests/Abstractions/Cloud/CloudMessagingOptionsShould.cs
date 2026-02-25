// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

/// <summary>
/// Unit tests for <see cref="CloudMessagingOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class CloudMessagingOptionsShould
{
	[Fact]
	public void HaveNullDefaultProvider_ByDefault()
	{
		// Arrange & Act
		var options = new CloudMessagingOptions();

		// Assert
		options.DefaultProvider.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyProvidersDictionary_ByDefault()
	{
		// Arrange & Act
		var options = new CloudMessagingOptions();

		// Assert
		options.Providers.ShouldNotBeNull();
		options.Providers.ShouldBeEmpty();
	}

	[Fact]
	public void HaveTracingEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CloudMessagingOptions();

		// Assert
		options.EnableTracing.ShouldBeTrue();
	}

	[Fact]
	public void HaveMetricsEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CloudMessagingOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Have30SecondGlobalTimeout_ByDefault()
	{
		// Arrange & Act
		var options = new CloudMessagingOptions();

		// Assert
		options.GlobalTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingDefaultProvider()
	{
		// Arrange
		var options = new CloudMessagingOptions();

		// Act
		options.DefaultProvider = "test-provider";

		// Assert
		options.DefaultProvider.ShouldBe("test-provider");
	}

	[Fact]
	public void AllowAddingProviders()
	{
		// Arrange
		var options = new CloudMessagingOptions();
		var providerOptions = new ProviderOptions();

		// Act
		options.Providers["azure"] = providerOptions;

		// Assert
		options.Providers.ShouldContainKey("azure");
		options.Providers["azure"].ShouldBe(providerOptions);
	}

	[Fact]
	public void HaveCaseInsensitiveProvidersDictionary()
	{
		// Arrange
		var options = new CloudMessagingOptions();
		var providerOptions = new ProviderOptions();

		// Act
		options.Providers["Azure"] = providerOptions;

		// Assert
		options.Providers.ShouldContainKey("azure");
		options.Providers.ShouldContainKey("AZURE");
		options.Providers["azure"].ShouldBe(providerOptions);
	}

	[Fact]
	public void AllowDisablingTracing()
	{
		// Arrange
		var options = new CloudMessagingOptions();

		// Act
		options.EnableTracing = false;

		// Assert
		options.EnableTracing.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingMetrics()
	{
		// Arrange
		var options = new CloudMessagingOptions();

		// Act
		options.EnableMetrics = false;

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllowChangingGlobalTimeout()
	{
		// Arrange
		var options = new CloudMessagingOptions();

		// Act
		options.GlobalTimeout = TimeSpan.FromMinutes(2);

		// Assert
		options.GlobalTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AllowZeroGlobalTimeout()
	{
		// Arrange
		var options = new CloudMessagingOptions();

		// Act
		options.GlobalTimeout = TimeSpan.Zero;

		// Assert
		options.GlobalTimeout.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowInfiniteGlobalTimeout()
	{
		// Arrange
		var options = new CloudMessagingOptions();

		// Act
		options.GlobalTimeout = Timeout.InfiniteTimeSpan;

		// Assert
		options.GlobalTimeout.ShouldBe(Timeout.InfiniteTimeSpan);
	}
}
