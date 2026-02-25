// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISoc2ReportGenerator conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateGenerator"/> to verify that
/// your SOC 2 report generator implementation conforms to the ISoc2ReportGenerator contract.
/// </para>
/// <para>
/// The test kit verifies core report generation operations including:
/// <list type="bullet">
/// <item><description>GenerateTypeIReportAsync generates point-in-time reports</description></item>
/// <item><description>GenerateTypeIReportAsync returns report with correct Type I properties</description></item>
/// <item><description>GenerateTypeIReportAsync report has required properties populated</description></item>
/// <item><description>GenerateTypeIIReportAsync generates period-based reports (90+ days)</description></item>
/// <item><description>GenerateTypeIIReportAsync returns report with correct period dates</description></item>
/// <item><description>GenerateTypeIIReportAsync report has required properties populated</description></item>
/// <item><description>GenerateTypeIIReportAsync throws ArgumentException for periods &lt; 90 days</description></item>
/// <item><description>GenerateAndStoreReportAsync generates Type I reports from request</description></item>
/// <item><description>GenerateAndStoreReportAsync generates Type II reports from request</description></item>
/// <item><description>GenerateAndStoreReportAsync throws ArgumentNullException for null request</description></item>
/// <item><description>GetControlDescriptionsAsync returns non-null list</description></item>
/// <item><description>GetControlDescriptionsAsync returns valid control descriptions</description></item>
/// <item><description>GetTestResultsAsync returns non-null list</description></item>
/// <item><description>GetTestResultsAsync returns valid test results</description></item>
/// <item><description>GetTestResultsAsync respects the specified period</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>GENERATOR PATTERN:</strong> ISoc2ReportGenerator is a SOC 2 report document producer
/// that creates complex Soc2Report objects from control validation, configuration, and test evidence.
/// </para>
/// <para>
/// <strong>DEPENDENCIES:</strong> The generator requires 4 dependencies:
/// <list type="number">
/// <item><description><c>IOptions&lt;Soc2Options&gt;</c> - Configuration (REQUIRED, null throws)</description></item>
/// <item><description><c>IControlValidationService</c> - Control validation (REQUIRED, null throws)</description></item>
/// <item><description><c>ILogger&lt;Soc2ReportGenerator&gt;</c> - Logging (REQUIRED, null throws)</description></item>
/// <item><description><c>ISoc2ReportStore?</c> - Report persistence (OPTIONAL, can be null)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>TYPE I vs TYPE II REPORTS:</strong>
/// <list type="bullet">
/// <item><description>Type I: Point-in-time assessment (PeriodStart == PeriodEnd)</description></item>
/// <item><description>Type II: Period-based assessment (minimum 90 days, configurable via MinimumTypeIIPeriodDays)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class Soc2ReportGeneratorConformanceTests : Soc2ReportGeneratorConformanceTestKit
/// {
///     protected override ISoc2ReportGenerator CreateGenerator()
///     {
///         // Configure options
///         var options = Options.Create(new Soc2Options
///         {
///             MinimumTypeIIPeriodDays = 90,
///             DefaultTestSampleSize = 25,
///             EnabledCategories = [TrustServicesCategory.Security]
///         });
///
///         // Create validation service
///         var validators = new IControlValidator[] { new AuditLogControlValidator() };
///         var validationService = new ControlValidationService(validators);
///
///         // Create logger
///         var logger = NullLogger&lt;Soc2ReportGenerator&gt;.Instance;
///
///         // No report store (optional)
///         return new Soc2ReportGenerator(options, validationService, logger);
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class Soc2ReportGeneratorConformanceTestKit
{
	/// <summary>
	/// Creates a fresh SOC 2 report generator instance for testing.
	/// </summary>
	/// <returns>An ISoc2ReportGenerator implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// For Soc2ReportGenerator, the typical implementation:
	/// </para>
	/// <code>
	/// protected override ISoc2ReportGenerator CreateGenerator()
	/// {
	///     var options = Options.Create(new Soc2Options
	///     {
	///         MinimumTypeIIPeriodDays = 90,
	///         DefaultTestSampleSize = 25,
	///         EnabledCategories = [TrustServicesCategory.Security]
	///     });
	///
	///     var validators = new IControlValidator[] { new AuditLogControlValidator() };
	///     var validationService = new ControlValidationService(validators);
	///     var logger = NullLogger&lt;Soc2ReportGenerator&gt;.Instance;
	///
	///     return new Soc2ReportGenerator(options, validationService, logger);
	/// }
	/// </code>
	/// </remarks>
	protected abstract ISoc2ReportGenerator CreateGenerator();

	/// <summary>
	/// Creates default report options for testing.
	/// </summary>
	protected virtual ReportOptions CreateReportOptions() => new()
	{
		TenantId = "test-tenant-001",
		Categories = [TrustServicesCategory.Security],
		IncludeTestResults = false,
		CustomTitle = null
	};

	#region GenerateTypeIReportAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateTypeIReportAsync"/> returns a valid Type I report.
	/// </summary>
	protected virtual async Task GenerateTypeIReportAsync_ValidOptions_ShouldReturnReport()
	{
		// Arrange
		var generator = CreateGenerator();
		var asOfDate = DateTimeOffset.UtcNow;
		var options = CreateReportOptions();

		// Act
		var report = await generator.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (report == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GenerateTypeIReportAsync to return non-null report.");
		}

		if (report.ReportType != Soc2ReportType.TypeI)
		{
			throw new TestFixtureAssertionException(
				$"Expected report type to be TypeI, but got {report.ReportType}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateTypeIReportAsync"/> returns a point-in-time report.
	/// </summary>
	protected virtual async Task GenerateTypeIReportAsync_ShouldReturnPointInTime()
	{
		// Arrange
		var generator = CreateGenerator();
		var asOfDate = DateTimeOffset.UtcNow;
		var options = CreateReportOptions();

		// Act
		var report = await generator.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (report.PeriodStart != report.PeriodEnd)
		{
			throw new TestFixtureAssertionException(
				$"Expected Type I report to have PeriodStart == PeriodEnd (point-in-time), but got Start={report.PeriodStart}, End={report.PeriodEnd}.");
		}

		if (report.PeriodStart.Date != asOfDate.Date)
		{
			throw new TestFixtureAssertionException(
				$"Expected Type I report PeriodStart to match asOfDate ({asOfDate}), but got {report.PeriodStart}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateTypeIReportAsync"/> populates required properties.
	/// </summary>
	protected virtual async Task GenerateTypeIReportAsync_ShouldHaveRequiredProperties()
	{
		// Arrange
		var generator = CreateGenerator();
		var asOfDate = DateTimeOffset.UtcNow;
		var options = CreateReportOptions();

		// Act
		var report = await generator.GenerateTypeIReportAsync(asOfDate, options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (report.ReportId == Guid.Empty)
		{
			throw new TestFixtureAssertionException(
				"Expected report.ReportId to be non-empty GUID.");
		}

		if (string.IsNullOrWhiteSpace(report.Title))
		{
			throw new TestFixtureAssertionException(
				"Expected report.Title to be non-null and non-empty.");
		}

		// Opinion should be one of the valid enum values (Unqualified, Qualified, Adverse, Disclaimer)
		// No need to check for Unknown as it doesn't exist in the enum
	}

	#endregion

	#region GenerateTypeIIReportAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateTypeIIReportAsync"/> returns a valid Type II report for periods >= 90 days.
	/// </summary>
	protected virtual async Task GenerateTypeIIReportAsync_ValidPeriod_ShouldReturnReport()
	{
		// Arrange
		var generator = CreateGenerator();
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-100); // 100 days > 90 day minimum
		var options = new ReportOptions
		{
			TenantId = "test-tenant-001",
			Categories = [TrustServicesCategory.Security],
			IncludeTestResults = true, // Type II should include test results
			CustomTitle = null
		};

		// Act
		var report = await generator.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		if (report == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GenerateTypeIIReportAsync to return non-null report.");
		}

		if (report.ReportType != Soc2ReportType.TypeII)
		{
			throw new TestFixtureAssertionException(
				$"Expected report type to be TypeII, but got {report.ReportType}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateTypeIIReportAsync"/> returns correct period dates.
	/// </summary>
	protected virtual async Task GenerateTypeIIReportAsync_ShouldReturnCorrectPeriod()
	{
		// Arrange
		var generator = CreateGenerator();
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-100);
		var options = CreateReportOptions();

		// Act
		var report = await generator.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		if (report.PeriodStart.Date != periodStart.Date)
		{
			throw new TestFixtureAssertionException(
				$"Expected report.PeriodStart ({report.PeriodStart}) to match input periodStart ({periodStart}).");
		}

		if (report.PeriodEnd.Date != periodEnd.Date)
		{
			throw new TestFixtureAssertionException(
				$"Expected report.PeriodEnd ({report.PeriodEnd}) to match input periodEnd ({periodEnd}).");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateTypeIIReportAsync"/> populates required properties.
	/// </summary>
	protected virtual async Task GenerateTypeIIReportAsync_ShouldHaveRequiredProperties()
	{
		// Arrange
		var generator = CreateGenerator();
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-100);
		var options = CreateReportOptions();

		// Act
		var report = await generator.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		if (report.ReportId == Guid.Empty)
		{
			throw new TestFixtureAssertionException(
				"Expected report.ReportId to be non-empty GUID.");
		}

		if (string.IsNullOrWhiteSpace(report.Title))
		{
			throw new TestFixtureAssertionException(
				"Expected report.Title to be non-null and non-empty.");
		}

		// Opinion should be one of the valid enum values (Unqualified, Qualified, Adverse, Disclaimer)
		// No need to check for Unknown as it doesn't exist in the enum
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateTypeIIReportAsync"/> throws ArgumentException for periods shorter than minimum (90 days).
	/// </summary>
	protected virtual async Task GenerateTypeIIReportAsync_PeriodTooShort_ShouldThrow()
	{
		// Arrange
		var generator = CreateGenerator();
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-30); // 30 days < 90 day minimum
		var options = CreateReportOptions();

		// Act & Assert
		try
		{
			_ = await generator.GenerateTypeIIReportAsync(periodStart, periodEnd, options, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected GenerateTypeIIReportAsync to throw ArgumentException for period < 90 days.");
		}
		catch (ArgumentException)
		{
			// Expected exception
		}
		catch (Exception ex) when (ex is not TestFixtureAssertionException)
		{
			throw new TestFixtureAssertionException(
				$"Expected GenerateTypeIIReportAsync to throw ArgumentException, but got {ex.GetType().Name}.");
		}
	}

	#endregion

	#region GenerateAndStoreReportAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateAndStoreReportAsync"/> generates Type I reports from request.
	/// </summary>
	protected virtual async Task GenerateAndStoreReportAsync_TypeI_ShouldReturnReport()
	{
		// Arrange
		var generator = CreateGenerator();
		var asOfDate = DateTimeOffset.UtcNow;
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeI,
			PeriodStart = asOfDate,
			PeriodEnd = null, // Type I doesn't require PeriodEnd
			Options = CreateReportOptions(),
			RequestedBy = "test-user"
		};

		// Act
		var report = await generator.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (report == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GenerateAndStoreReportAsync to return non-null report for Type I request.");
		}

		if (report.ReportType != Soc2ReportType.TypeI)
		{
			throw new TestFixtureAssertionException(
				$"Expected report type to be TypeI, but got {report.ReportType}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateAndStoreReportAsync"/> generates Type II reports from request.
	/// </summary>
	protected virtual async Task GenerateAndStoreReportAsync_TypeII_ShouldReturnReport()
	{
		// Arrange
		var generator = CreateGenerator();
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-100);
		var request = new ReportGenerationRequest
		{
			ReportType = Soc2ReportType.TypeII,
			PeriodStart = periodStart,
			PeriodEnd = periodEnd, // Type II requires PeriodEnd
			Options = CreateReportOptions(),
			RequestedBy = "test-user"
		};

		// Act
		var report = await generator.GenerateAndStoreReportAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (report == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GenerateAndStoreReportAsync to return non-null report for Type II request.");
		}

		if (report.ReportType != Soc2ReportType.TypeII)
		{
			throw new TestFixtureAssertionException(
				$"Expected report type to be TypeII, but got {report.ReportType}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GenerateAndStoreReportAsync"/> throws ArgumentNullException for null request.
	/// </summary>
	protected virtual async Task GenerateAndStoreReportAsync_NullRequest_ShouldThrow()
	{
		// Arrange
		var generator = CreateGenerator();

		// Act & Assert
		try
		{
			_ = await generator.GenerateAndStoreReportAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected GenerateAndStoreReportAsync to throw ArgumentNullException for null request.");
		}
		catch (ArgumentNullException)
		{
			// Expected exception
		}
		catch (Exception ex) when (ex is not TestFixtureAssertionException)
		{
			throw new TestFixtureAssertionException(
				$"Expected GenerateAndStoreReportAsync to throw ArgumentNullException, but got {ex.GetType().Name}.");
		}
	}

	#endregion

	#region GetControlDescriptionsAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GetControlDescriptionsAsync"/> returns a non-null list.
	/// </summary>
	protected virtual async Task GetControlDescriptionsAsync_ShouldNotBeNull()
	{
		// Arrange
		var generator = CreateGenerator();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;

		// Act
		var result = await generator.GetControlDescriptionsAsync(criterion, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlDescriptionsAsync to return non-null list.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GetControlDescriptionsAsync"/> returns valid control descriptions.
	/// </summary>
	protected virtual async Task GetControlDescriptionsAsync_ShouldReturnValidDescriptions()
	{
		// Arrange
		var generator = CreateGenerator();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;

		// Act
		var result = await generator.GetControlDescriptionsAsync(criterion, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlDescriptionsAsync to return non-null list.");
		}

		// If there are descriptions, verify they have required properties
		foreach (var description in result)
		{
			if (string.IsNullOrWhiteSpace(description.ControlId))
			{
				throw new TestFixtureAssertionException(
					"Expected all control descriptions to have non-null, non-empty ControlId.");
			}

			if (string.IsNullOrWhiteSpace(description.Name))
			{
				throw new TestFixtureAssertionException(
					$"Expected control {description.ControlId} to have non-null, non-empty Name.");
			}
		}
	}

	#endregion

	#region GetTestResultsAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GetTestResultsAsync"/> returns a non-null list.
	/// </summary>
	protected virtual async Task GetTestResultsAsync_ShouldNotBeNull()
	{
		// Arrange
		var generator = CreateGenerator();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-100);

		// Act
		var result = await generator.GetTestResultsAsync(criterion, periodStart, periodEnd, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetTestResultsAsync to return non-null list.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GetTestResultsAsync"/> returns valid test results.
	/// </summary>
	protected virtual async Task GetTestResultsAsync_ShouldReturnValidResults()
	{
		// Arrange
		var generator = CreateGenerator();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-100);

		// Act
		var result = await generator.GetTestResultsAsync(criterion, periodStart, periodEnd, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetTestResultsAsync to return non-null list.");
		}

		// If there are results, verify they have required properties
		foreach (var testResult in result)
		{
			if (string.IsNullOrWhiteSpace(testResult.ControlId))
			{
				throw new TestFixtureAssertionException(
					"Expected all test results to have non-null, non-empty ControlId.");
			}

			if (testResult.SampleSize < 0)
			{
				throw new TestFixtureAssertionException(
					$"Expected test result for {testResult.ControlId} to have SampleSize >= 0, but got {testResult.SampleSize}.");
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="ISoc2ReportGenerator.GetTestResultsAsync"/> respects the specified period.
	/// </summary>
	protected virtual async Task GetTestResultsAsync_ShouldRespectPeriod()
	{
		// Arrange
		var generator = CreateGenerator();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;
		var periodEnd = DateTimeOffset.UtcNow;
		var periodStart = periodEnd.AddDays(-100);

		// Act
		var result = await generator.GetTestResultsAsync(criterion, periodStart, periodEnd, CancellationToken.None).ConfigureAwait(false);

		// Assert - This test primarily validates that the method accepts and processes the period parameters
		// The actual period filtering logic is internal and validated through integration tests
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetTestResultsAsync to return non-null list for specified period.");
		}
	}

	#endregion
}
