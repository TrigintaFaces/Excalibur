// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CachingSchemaRegistryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new CachingSchemaRegistryOptions();

		// Assert
		options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
		options.CacheCompatibilityResults.ShouldBeTrue();
		options.MaxCacheSize.ShouldBe(1000);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new CachingSchemaRegistryOptions
		{
			CacheDuration = TimeSpan.FromMinutes(10),
			CacheCompatibilityResults = false,
			MaxCacheSize = 500,
		};

		// Assert
		options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(10));
		options.CacheCompatibilityResults.ShouldBeFalse();
		options.MaxCacheSize.ShouldBe(500);
	}
}
