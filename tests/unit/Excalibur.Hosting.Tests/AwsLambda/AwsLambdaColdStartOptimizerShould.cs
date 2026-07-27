// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Hosting.Tests.AwsLambda;

/// <summary>
/// Unit tests for <see cref="AwsLambdaColdStartOptimizer" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class AwsLambdaColdStartOptimizerShould : UnitTestBase
{
	private readonly AwsLambdaColdStartOptimizer _sut;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AwsLambdaColdStartOptimizer> _logger;

	public AwsLambdaColdStartOptimizerShould()
	{
		_serviceProvider = A.Fake<IServiceProvider>();
		_logger = NullLogger<AwsLambdaColdStartOptimizer>.Instance;
		_sut = new AwsLambdaColdStartOptimizer(_serviceProvider, Microsoft.Extensions.Options.Options.Create(new AwsLambdaOptions()), _logger);
	}

	[Fact]
	public void IsEnabled_ReturnsFalse_WhenColdStartOptimizationDisabled()
	{
		// IsEnabled reads the AwsLambdaOptions flag (defaulted once at the composition root from
		// AWS_LAMBDA_FUNCTION_NAME), NOT a direct environment read on each evaluation. Default = disabled.
		var optimizer = new AwsLambdaColdStartOptimizer(
			_serviceProvider,
			Microsoft.Extensions.Options.Options.Create(new AwsLambdaOptions { ColdStartOptimizationEnabled = false }),
			_logger);

		optimizer.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnsTrue_WhenColdStartOptimizationEnabled()
	{
		// The flag (set true at the composition root when AWS_LAMBDA_FUNCTION_NAME is present) drives IsEnabled.
		var optimizer = new AwsLambdaColdStartOptimizer(
			_serviceProvider,
			Microsoft.Extensions.Options.Options.Create(new AwsLambdaOptions { ColdStartOptimizationEnabled = true }),
			_logger);

		optimizer.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task OptimizeAsync_CompletesSuccessfully()
	{
		// Act
		var act = () => _sut.OptimizeAsync();

		// Assert
		await act.ShouldNotThrowAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task WarmupAsync_CompletesSuccessfully()
	{
		// Act
		var act = () => _sut.WarmupAsync();

		// Assert
		await act.ShouldNotThrowAsync().ConfigureAwait(false);
	}
}
