// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for <see cref="ElasticsearchMonitoringOptionsValidator"/>.
/// </summary>
/// <remarks>
/// Both halves again: the safety half is that a nonsensical configuration stops the host at startup, and the liveness
/// half is that a reasonable one does not - a validator that rejected everything would satisfy every rejection
/// assertion here while making the package impossible to use.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class ElasticsearchMonitoringOptionsValidatorShould
{
	private static readonly ElasticsearchMonitoringOptionsValidator Validator = new();

	#region Safety - a configuration that cannot work must stop the host

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void RejectANonPositiveBodySizeLimit(int maxBodySizeBytes)
	{
		// Arrange - nothing validated this before, so a mistyped limit silently produced no body logging
		var options = new ElasticsearchMonitoringOptions
		{
			RequestLogging = new RequestLoggingOptions { MaxBodySizeBytes = maxBodySizeBytes },
		};

		// Act
		var result = Validator.Validate(name: null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RequestLoggingOptions.MaxBodySizeBytes));
	}

	[Fact]
	public void RejectANullAllowedBodyPropertiesCollection()
	{
		// Arrange
		var options = new ElasticsearchMonitoringOptions
		{
			RequestLogging = new RequestLoggingOptions { AllowedBodyProperties = null! },
		};

		// Act
		var result = Validator.Validate(name: null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RequestLoggingOptions.AllowedBodyProperties));
	}

	[Fact]
	public void RejectANullRequestLoggingSection()
	{
		// Arrange
		var options = new ElasticsearchMonitoringOptions { RequestLogging = null! };

		// Act
		var result = Validator.Validate(name: null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ElasticsearchMonitoringOptions.RequestLogging));
	}

	[Fact]
	public void RejectAnUndefinedMonitoringLevel()
	{
		// Arrange - the check that existed before, kept working
		var options = new ElasticsearchMonitoringOptions { Level = (MonitoringLevel)int.MaxValue };

		// Act
		var result = Validator.Validate(name: null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ElasticsearchMonitoringOptions.Level));
	}

	#endregion

	#region Liveness - a reasonable configuration must still start

	[Fact]
	public void AcceptTheDefaultConfiguration()
	{
		// Arrange - guards against a validator that rejects everything and passes every assertion above
		var options = new ElasticsearchMonitoringOptions();

		// Act
		var result = Validator.Validate(name: null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void AcceptAFullyConfiguredRequestLoggingSection()
	{
		// Arrange
		var options = new ElasticsearchMonitoringOptions
		{
			RequestLogging = new RequestLoggingOptions
			{
				Enabled = true,
				LogRequestBody = true,
				LogResponseBody = true,
				LogTransportDebugInformation = true,
				MaxBodySizeBytes = 8192,
				AllowedBodyProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "index" },
			},
		};

		// Act
		var result = Validator.Validate(name: null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void AcceptTheSmallestUsableBodySizeLimit()
	{
		// Arrange - one byte is odd but not invalid, and the boundary is where an off-by-one would show
		var options = new ElasticsearchMonitoringOptions
		{
			RequestLogging = new RequestLoggingOptions { MaxBodySizeBytes = 1 },
		};

		// Act
		var result = Validator.Validate(name: null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	#endregion
}
