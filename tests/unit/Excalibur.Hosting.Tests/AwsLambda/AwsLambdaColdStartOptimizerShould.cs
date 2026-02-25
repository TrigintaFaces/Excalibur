// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

namespace Excalibur.Hosting.Tests.AwsLambda;

/// <summary>
/// Unit tests for <see cref="AwsLambdaColdStartOptimizer" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
public sealed class AwsLambdaColdStartOptimizerShould : UnitTestBase
{
	private readonly AwsLambdaColdStartOptimizer _sut;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AwsLambdaColdStartOptimizer> _logger;

	public AwsLambdaColdStartOptimizerShould()
	{
		_serviceProvider = A.Fake<IServiceProvider>();
		_logger = NullLogger<AwsLambdaColdStartOptimizer>.Instance;
		_sut = new AwsLambdaColdStartOptimizer(_serviceProvider, _logger);
	}

	[Fact]
	public void IsEnabled_ReturnsFalse_WhenNotInLambdaEnvironment()
	{
		// Arrange - Environment variable not set (default state)
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);

		// Act
		var result = _sut.IsEnabled;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnsTrue_WhenInLambdaEnvironment()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

		try
		{
			// Act
			var result = _sut.IsEnabled;

			// Assert
			result.ShouldBeTrue();
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		}
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
