// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

namespace Excalibur.Hosting.Tests.GoogleCloudFunctions;

/// <summary>
/// Unit tests for <see cref="GoogleCloudFunctionsColdStartOptimizer" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
public sealed class GoogleCloudFunctionsColdStartOptimizerShould : UnitTestBase
{
	private readonly GoogleCloudFunctionsColdStartOptimizer _sut;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<GoogleCloudFunctionsColdStartOptimizer> _logger;

	public GoogleCloudFunctionsColdStartOptimizerShould()
	{
		_serviceProvider = A.Fake<IServiceProvider>();
		_logger = NullLogger<GoogleCloudFunctionsColdStartOptimizer>.Instance;
		_sut = new GoogleCloudFunctionsColdStartOptimizer(_serviceProvider, _logger);
	}

	[Fact]
	public void IsEnabled_ReturnsFalse_WhenNotInGcfEnvironment()
	{
		// Arrange - Environment variable not set (default state)
		Environment.SetEnvironmentVariable("FUNCTION_NAME", null);

		// Act
		var result = _sut.IsEnabled;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnsTrue_WhenInGcfEnvironment()
	{
		// Arrange
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "test-function");

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
			Environment.SetEnvironmentVariable("FUNCTION_NAME", null);
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
