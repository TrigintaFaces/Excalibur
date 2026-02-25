// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GoogleCloudFunctionsColdStartOptimizerShould : UnitTestBase
{
	private readonly GoogleCloudFunctionsColdStartOptimizer _sut = new(A.Fake<IServiceProvider>(), EnabledTestLogger.Create<GoogleCloudFunctionsColdStartOptimizer>());

	[Fact]
	public void IsEnabled_ReturnFalse_WhenNotInGoogleCloudEnvironment()
	{
		Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
		_sut.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnTrue_WhenInGoogleCloudEnvironment()
	{
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "orders-handler");
		_sut.IsEnabled.ShouldBeTrue();
		Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
	}

	[Fact]
	public async Task OptimizeAndWarmup_CompleteWithoutException()
	{
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "orders-handler");
		Environment.SetEnvironmentVariable("GCP_PROJECT", "project-1");
		Environment.SetEnvironmentVariable("FUNCTION_TARGET", "handler");
		Environment.SetEnvironmentVariable("FUNCTION_REGION", "us-central1");
		Environment.SetEnvironmentVariable("K_SERVICE", "orders");
		Environment.SetEnvironmentVariable("K_REVISION", "orders-0001");

		try
		{
			await _sut.OptimizeAsync();
			await _sut.WarmupAsync();
		}
		finally
		{
			Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("GCP_PROJECT", null);
			Environment.SetEnvironmentVariable("FUNCTION_TARGET", null);
			Environment.SetEnvironmentVariable("FUNCTION_REGION", null);
			Environment.SetEnvironmentVariable("K_SERVICE", null);
			Environment.SetEnvironmentVariable("K_REVISION", null);
		}
	}
}
