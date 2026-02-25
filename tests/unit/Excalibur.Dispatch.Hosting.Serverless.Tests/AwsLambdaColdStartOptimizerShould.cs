// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsLambdaColdStartOptimizerShould : UnitTestBase
{
	private readonly AwsLambdaColdStartOptimizer _sut = new(A.Fake<IServiceProvider>(), EnabledTestLogger.Create<AwsLambdaColdStartOptimizer>());

	[Fact]
	public void IsEnabled_ReturnFalse_WhenNotInLambdaEnvironment()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		_sut.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnTrue_WhenInLambdaEnvironment()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "orders-handler");
		_sut.IsEnabled.ShouldBeTrue();
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
	}

	[Fact]
	public async Task OptimizeAndWarmup_CompleteWithoutException()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "orders-handler");
		Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", "Root=1-67891233-abcdef012345678912345678");
		Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
		Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", "AWS_Lambda_dotnet");
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", "512");

		try
		{
			await _sut.OptimizeAsync();
			await _sut.WarmupAsync();
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
			Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", null);
			Environment.SetEnvironmentVariable("AWS_REGION", null);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", null);
		}
	}
}
