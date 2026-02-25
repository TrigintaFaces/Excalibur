// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compliance.Tests.Masking;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MaskingServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterDataMaskingWithDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDataMasking();

		// Assert
		var provider = services.BuildServiceProvider();
		var masker = provider.GetService<IDataMasker>();
		masker.ShouldNotBeNull();
		masker.ShouldBeOfType<RegexDataMasker>();
	}

	[Fact]
	public void RegisterDataMaskingWithCustomRules()
	{
		// Arrange
		var services = new ServiceCollection();
		var customRules = new MaskingRules { MaskCardNumber = true, MaskSsn = true };

		// Act
		services.AddDataMasking(customRules);

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDataMasker>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterPciDssDataMasking()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPciDssDataMasking();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDataMasker>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterHipaaDataMasking()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddHipaaDataMasking();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDataMasker>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterStrictDataMasking()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddStrictDataMasking();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDataMasker>().ShouldNotBeNull();
	}

	[Fact]
	public void NotOverrideExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var firstMasker = new RegexDataMasker(MaskingRules.Default);
		services.AddSingleton<IDataMasker>(firstMasker);

		// Act
		services.AddDataMasking(MaskingRules.Strict);

		// Assert - first registration wins (TryAdd)
		var provider = services.BuildServiceProvider();
		var resolved = provider.GetService<IDataMasker>();
		resolved.ShouldBe(firstMasker);
	}
}
