// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Extensions.NETCore.Setup;
using Amazon.Scheduler;

using Excalibur.Jobs.CloudProviders.Aws;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.Tests.CloudProviders;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull_WithoutAwsOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsServiceCollectionExtensions.AddAwsScheduler(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_WithoutAwsOptions()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsScheduler((Action<AwsSchedulerOptions>)null!));
	}

	[Fact]
	public void RegisterRequiredServices_WithoutAwsOptions()
	{
		var services = new ServiceCollection();

		var result = services.AddAwsScheduler(options =>
		{
			options.TargetArn = "arn:aws:lambda:us-east-1:123456789012:function:jobs";
			options.ExecutionRoleArn = "arn:aws:iam::123456789012:role/excalibur-jobs";
			options.ScheduleGroup = "production";
		});

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IAmazonScheduler));
		services.ShouldContain(sd => sd.ServiceType == typeof(AmazonSchedulerClient));
		services.ShouldContain(sd => sd.ServiceType == typeof(AwsSchedulerJobProvider));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsSchedulerOptions>>().Value;
		options.ScheduleGroup.ShouldBe("production");
	}

	[Fact]
	public void ThrowWhenServicesIsNull_WithAwsOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsServiceCollectionExtensions.AddAwsScheduler(null!, new AWSOptions(), _ => { }));
	}

	[Fact]
	public void ThrowWhenAwsOptionsIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsScheduler(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_WithAwsOptions()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsScheduler(new AWSOptions(), null!));
	}

	[Fact]
	public void RegisterRequiredServices_WithAwsOptions()
	{
		var services = new ServiceCollection();
		var awsOptions = new AWSOptions();

		var result = services.AddAwsScheduler(awsOptions, options =>
		{
			options.TargetArn = "arn:aws:sqs:us-east-1:123456789012:jobs";
			options.ExecutionRoleArn = "arn:aws:iam::123456789012:role/excalibur-jobs";
			options.TimeZone = "UTC";
		});

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IAmazonScheduler));
		services.ShouldContain(sd => sd.ServiceType == typeof(AmazonSchedulerClient));
		services.ShouldContain(sd => sd.ServiceType == typeof(AwsSchedulerJobProvider));
	}
}
