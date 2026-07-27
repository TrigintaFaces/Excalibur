// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

// bfak2b (M11) — the "running on AWS Lambda?" decision now reads an Options flag
// (AwsLambdaOptions.ColdStartOptimizationEnabled), defaulted at the composition root from
// AWS_LAMBDA_FUNCTION_NAME. The IsEnabled arms set the option DIRECTLY (no env-var mutation — that is the
// testability the seam bought); a separate pair proves the env->option default wiring in
// AddAwsLambdaServerless, so the composition-root default is not advertised-but-unwired.
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsLambdaColdStartOptimizerShould : UnitTestBase
{
	private static AwsLambdaColdStartOptimizer NewSut(bool coldStartEnabled) =>
		new(
			A.Fake<IServiceProvider>(),
			Microsoft.Extensions.Options.Options.Create(
				new AwsLambdaOptions { ColdStartOptimizationEnabled = coldStartEnabled }),
			EnabledTestLogger.Create<AwsLambdaColdStartOptimizer>());

	// IsEnabled reads the injected option — no environment-variable read on the decision path.
	[Fact]
	public void IsEnabled_ReadsTheOptionFlag_False() =>
		NewSut(coldStartEnabled: false).IsEnabled.ShouldBeFalse();

	[Fact]
	public void IsEnabled_ReadsTheOptionFlag_True() =>
		NewSut(coldStartEnabled: true).IsEnabled.ShouldBeTrue();

	// Env->option default wiring (non-vacuity): AddAwsLambdaServerless defaults ColdStartOptimizationEnabled
	// from AWS_LAMBDA_FUNCTION_NAME at the composition root. RED if the Configure<AwsLambdaOptions> is dropped.
	[Fact]
	public void AddAwsLambdaServerless_DefaultsColdStartFlagOn_WhenLambdaFunctionNamePresent()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "orders-handler");
		try
		{
			using var provider = new ServiceCollection().AddAwsLambdaServerless().BuildServiceProvider();
			provider.GetRequiredService<IOptions<AwsLambdaOptions>>().Value.ColdStartOptimizationEnabled
				.ShouldBeTrue("the flag defaults ON when AWS_LAMBDA_FUNCTION_NAME is present (running on Lambda)");
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		}
	}

	[Fact]
	public void AddAwsLambdaServerless_DefaultsColdStartFlagOff_WhenNotOnLambda()
	{
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		using var provider = new ServiceCollection().AddAwsLambdaServerless().BuildServiceProvider();
		provider.GetRequiredService<IOptions<AwsLambdaOptions>>().Value.ColdStartOptimizationEnabled
			.ShouldBeFalse("the flag defaults OFF when AWS_LAMBDA_FUNCTION_NAME is absent (not on Lambda)");
	}

	[Fact]
	public async Task OptimizeAndWarmup_CompleteWithoutException()
	{
		Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", "Root=1-67891233-abcdef012345678912345678");
		Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
		Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", "AWS_Lambda_dotnet");
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", "512");

		try
		{
			var sut = NewSut(coldStartEnabled: true);
			await sut.OptimizeAsync();
			await sut.WarmupAsync();
		}
		finally
		{
			Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", null);
			Environment.SetEnvironmentVariable("AWS_REGION", null);
			Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", null);
		}
	}
}
