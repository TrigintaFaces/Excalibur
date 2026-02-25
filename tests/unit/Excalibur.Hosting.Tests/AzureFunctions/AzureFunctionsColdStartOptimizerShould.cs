// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AzureFunctions;

namespace Excalibur.Hosting.Tests.AzureFunctions;

/// <summary>
/// Unit tests for <see cref="AzureFunctionsColdStartOptimizer" />.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
public sealed class AzureFunctionsColdStartOptimizerShould : UnitTestBase
{
	private readonly AzureFunctionsColdStartOptimizer _sut;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<AzureFunctionsColdStartOptimizer> _logger;

	public AzureFunctionsColdStartOptimizerShould()
	{
		_serviceProvider = A.Fake<IServiceProvider>();
		_logger = NullLogger<AzureFunctionsColdStartOptimizer>.Instance;
		_sut = new AzureFunctionsColdStartOptimizer(_serviceProvider, _logger);
	}

	[Fact]
	public void IsEnabled_ReturnsFalse_WhenNotInAzureEnvironment()
	{
		// Arrange - Environment variable not set (default state)
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);

		// Act
		var result = _sut.IsEnabled;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_ReturnsTrue_WhenInAzureEnvironment()
	{
		// Arrange
		Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");

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
			Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
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
