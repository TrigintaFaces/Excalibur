// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="Soc2ReportGenerator"/> validating ISoc2ReportGenerator contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// Soc2ReportGenerator is a SOC 2 report document producer that creates complex Soc2Report objects
/// from control validation, configuration, and test evidence.
/// </para>
/// <para>
/// <strong>GENERATOR PATTERN:</strong> ISoc2ReportGenerator is the FIRST conformance kit using the GENERATOR pattern.
/// It produces document objects (Soc2Report) by combining data from multiple sources.
/// </para>
/// <para>
/// <strong>DEPENDENCIES (4 - MOST COMPLEX):</strong>
/// <list type="number">
/// <item><description><c>IOptions&lt;Soc2Options&gt;</c> - Configuration (REQUIRED, null throws)</description></item>
/// <item><description><c>IControlValidationService</c> - Control validation from Sprint 163 (REQUIRED, null throws)</description></item>
/// <item><description><c>ILogger&lt;Soc2ReportGenerator&gt;</c> - Logging (REQUIRED, null throws)</description></item>
/// <item><description><c>ISoc2ReportStore?</c> - Report persistence (OPTIONAL, can be null)</description></item>
/// </list>
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>GenerateTypeIReportAsync generates point-in-time reports (PeriodStart == PeriodEnd)</description></item>
/// <item><description>GenerateTypeIIReportAsync generates period-based reports (minimum 90 days)</description></item>
/// <item><description>GenerateTypeIIReportAsync throws ArgumentException for periods &lt; 90 days</description></item>
/// <item><description>GenerateAndStoreReportAsync handles both Type I and Type II requests</description></item>
/// <item><description>GetControlDescriptionsAsync returns control metadata for criteria</description></item>
/// <item><description>GetTestResultsAsync returns test results for specified period</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "GENERATOR")]
public sealed class Soc2ReportGeneratorConformanceTests : Soc2ReportGeneratorConformanceTestKit
{
	/// <inheritdoc />
	protected override ISoc2ReportGenerator CreateGenerator()
	{
		// Configure options
		var options = Options.Create(new Soc2Options
		{
			MinimumTypeIIPeriodDays = 90,
			DefaultTestSampleSize = 25,
			EnabledCategories = [TrustServicesCategory.Security],
			SystemDescription = null // Use default
		});

		// Create validation service (from Sprint 163)
		// Uses AuditLogControlValidator which has optional dependencies
		var validators = new IControlValidator[] { new AuditLogControlValidator() };
		var validationService = new ControlValidationService(validators);

		// Create logger
		var logger = NullLogger<Soc2ReportGenerator>.Instance;

		// No report store (optional dependency)
		return new Soc2ReportGenerator(options, validationService, logger);
	}

	#region GenerateTypeIReportAsync Method Tests

	[Fact]
	public Task GenerateTypeIReportAsync_ValidOptions_ShouldReturnReport_Test() =>
		GenerateTypeIReportAsync_ValidOptions_ShouldReturnReport();

	[Fact]
	public Task GenerateTypeIReportAsync_ShouldReturnPointInTime_Test() =>
		GenerateTypeIReportAsync_ShouldReturnPointInTime();

	[Fact]
	public Task GenerateTypeIReportAsync_ShouldHaveRequiredProperties_Test() =>
		GenerateTypeIReportAsync_ShouldHaveRequiredProperties();

	#endregion GenerateTypeIReportAsync Method Tests

	#region GenerateTypeIIReportAsync Method Tests

	[Fact]
	public Task GenerateTypeIIReportAsync_ValidPeriod_ShouldReturnReport_Test() =>
		GenerateTypeIIReportAsync_ValidPeriod_ShouldReturnReport();

	[Fact]
	public Task GenerateTypeIIReportAsync_ShouldReturnCorrectPeriod_Test() =>
		GenerateTypeIIReportAsync_ShouldReturnCorrectPeriod();

	[Fact]
	public Task GenerateTypeIIReportAsync_ShouldHaveRequiredProperties_Test() =>
		GenerateTypeIIReportAsync_ShouldHaveRequiredProperties();

	[Fact]
	public Task GenerateTypeIIReportAsync_PeriodTooShort_ShouldThrow_Test() =>
		GenerateTypeIIReportAsync_PeriodTooShort_ShouldThrow();

	#endregion GenerateTypeIIReportAsync Method Tests

	#region GenerateAndStoreReportAsync Method Tests

	[Fact]
	public Task GenerateAndStoreReportAsync_TypeI_ShouldReturnReport_Test() =>
		GenerateAndStoreReportAsync_TypeI_ShouldReturnReport();

	[Fact]
	public Task GenerateAndStoreReportAsync_TypeII_ShouldReturnReport_Test() =>
		GenerateAndStoreReportAsync_TypeII_ShouldReturnReport();

	[Fact]
	public Task GenerateAndStoreReportAsync_NullRequest_ShouldThrow_Test() =>
		GenerateAndStoreReportAsync_NullRequest_ShouldThrow();

	#endregion GenerateAndStoreReportAsync Method Tests

	#region GetControlDescriptionsAsync Method Tests

	[Fact]
	public Task GetControlDescriptionsAsync_ShouldNotBeNull_Test() =>
		GetControlDescriptionsAsync_ShouldNotBeNull();

	[Fact]
	public Task GetControlDescriptionsAsync_ShouldReturnValidDescriptions_Test() =>
		GetControlDescriptionsAsync_ShouldReturnValidDescriptions();

	#endregion GetControlDescriptionsAsync Method Tests

	#region GetTestResultsAsync Method Tests

	[Fact]
	public Task GetTestResultsAsync_ShouldNotBeNull_Test() =>
		GetTestResultsAsync_ShouldNotBeNull();

	[Fact]
	public Task GetTestResultsAsync_ShouldReturnValidResults_Test() =>
		GetTestResultsAsync_ShouldReturnValidResults();

	[Fact]
	public Task GetTestResultsAsync_ShouldRespectPeriod_Test() =>
		GetTestResultsAsync_ShouldRespectPeriod();

	#endregion GetTestResultsAsync Method Tests
}
