// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Hosting.Tests.Serverless;

/// <summary>
/// Unit tests for <see cref="AwsLambdaColdStartOptimizer"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ColdStart")]
[Collection("EnvironmentVariableTests")]
public sealed class AwsLambdaColdStartOptimizerShould : UnitTestBase
{
	private readonly IServiceProvider _serviceProvider;
	private readonly AwsLambdaColdStartOptimizer _sut;

	public AwsLambdaColdStartOptimizerShould()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();
		_sut = new AwsLambdaColdStartOptimizer(
			_serviceProvider,
			NullLogger<AwsLambdaColdStartOptimizer>.Instance);
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsLambdaColdStartOptimizer(null!, NullLogger<AwsLambdaColdStartOptimizer>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsLambdaColdStartOptimizer(_serviceProvider, null!));
	}

	[Fact]
	public void IsEnabled_ReturnsFalse_WhenEnvVarNotSet()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);

		// Act & Assert
		_sut.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnsTrue_WhenEnvVarIsSet()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

		try
		{
			// Act & Assert
			_sut.IsEnabled.ShouldBeTrue();
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		}
	}

	[Fact]
	public async Task OptimizeAsync_CompletesWithoutError_WhenDisabled()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);

		// Act & Assert - should not throw
		await _sut.OptimizeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task OptimizeAsync_CompletesWithoutError_WhenEnabled()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

		try
		{
			// Act & Assert - should not throw
			await _sut.OptimizeAsync().ConfigureAwait(false);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		}
	}

	[Fact]
	public async Task WarmupAsync_CompletesWithoutError_WhenDisabled()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);

		// Act & Assert - should return immediately without error
		await _sut.WarmupAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task WarmupAsync_CompletesWithoutError_WhenEnabled()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

		try
		{
			// Act & Assert
			await _sut.WarmupAsync().ConfigureAwait(false);
		}
		finally
		{
			Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
		}
	}

	[Fact]
	public void ImplementsIColdStartOptimizer()
	{
		_sut.ShouldBeAssignableTo<IColdStartOptimizer>();
	}
}
