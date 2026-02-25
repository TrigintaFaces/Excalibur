// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudWatchMetricsServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			CloudWatchMetricsServiceCollectionExtensions.AddAwsCloudWatchMetricsExporter(
				null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsCloudWatchMetricsExporter(null!));
	}

	[Fact]
	public void RegisterCloudWatchMetricsOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsCloudWatchMetricsExporter(opts =>
		{
			opts.Namespace = "MyApp/Dispatch";
			opts.Region = "us-east-1";
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}
}
