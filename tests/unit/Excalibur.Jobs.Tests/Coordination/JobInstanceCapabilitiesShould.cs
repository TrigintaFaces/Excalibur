// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Unit tests for <see cref="JobInstanceCapabilities"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Coordination")]
public sealed class JobInstanceCapabilitiesShould : UnitTestBase
{
	private static readonly string[] TestJobTypes = ["TestJob"];
	private static readonly string[] MultipleJobTypes = ["JobA", "JobB", "JobC"];
	private static readonly string[] OrderJobTypes = ["OrderProcessing", "EmailSending"];
	private static readonly string[] WildcardJobType = ["*"];
	private static readonly string[] DuplicateJobTypes = ["TestJob", "TestJob", "TestJob"];

	[Fact]
	public void RequirePositiveMaxConcurrentJobs()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new JobInstanceCapabilities(0, TestJobTypes));
	}

	[Fact]
	public void RequireNonNullSupportedJobTypes()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new JobInstanceCapabilities(5, null!));
	}

	[Fact]
	public void StoreMaxConcurrentJobs()
	{
		// Act
		var capabilities = new JobInstanceCapabilities(10, TestJobTypes);

		// Assert
		capabilities.MaxConcurrentJobs.ShouldBe(10);
	}

	[Fact]
	public void StoreSupportedJobTypes()
	{
		// Act
		var capabilities = new JobInstanceCapabilities(5, MultipleJobTypes);

		// Assert
		capabilities.SupportedJobTypes.Count.ShouldBe(3);
		capabilities.SupportedJobTypes.ShouldContain("JobA");
		capabilities.SupportedJobTypes.ShouldContain("JobB");
		capabilities.SupportedJobTypes.ShouldContain("JobC");
	}

	[Fact]
	public void HaveDefaultPriorityOfOne()
	{
		// Act
		var capabilities = new JobInstanceCapabilities(5, TestJobTypes);

		// Assert
		capabilities.Priority.ShouldBe(1);
	}

	[Fact]
	public void AllowSettingPriority()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(5, TestJobTypes);

		// Act
		capabilities.Priority = 10;

		// Assert
		capabilities.Priority.ShouldBe(10);
	}

	[Fact]
	public void HaveEmptyTagsCollectionByDefault()
	{
		// Act
		var capabilities = new JobInstanceCapabilities(5, TestJobTypes);

		// Assert
		_ = capabilities.Tags.ShouldNotBeNull();
		capabilities.Tags.ShouldBeEmpty();
	}

	[Fact]
	public void AllowAddingTags()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(5, TestJobTypes);

		// Act
		_ = capabilities.Tags.Add("high-memory");
		_ = capabilities.Tags.Add("gpu-enabled");

		// Assert
		capabilities.Tags.Count.ShouldBe(2);
		capabilities.Tags.ShouldContain("high-memory");
		capabilities.Tags.ShouldContain("gpu-enabled");
	}

	[Fact]
	public void CanProcessSupportedJobType()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(5, OrderJobTypes);

		// Act & Assert
		capabilities.CanProcess("OrderProcessing").ShouldBeTrue();
		capabilities.CanProcess("EmailSending").ShouldBeTrue();
	}

	[Fact]
	public void CannotProcessUnsupportedJobType()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(5, TestJobTypes);

		// Act & Assert
		capabilities.CanProcess("PaymentProcessing").ShouldBeFalse();
	}

	[Fact]
	public void CanProcessAnyJobTypeWithWildcard()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(5, WildcardJobType);

		// Act & Assert
		capabilities.CanProcess("OrderProcessing").ShouldBeTrue();
		capabilities.CanProcess("PaymentProcessing").ShouldBeTrue();
		capabilities.CanProcess("AnyJob").ShouldBeTrue();
	}

	[Fact]
	public void SupportedJobTypesAreCaseSensitive()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(5, TestJobTypes);

		// Act & Assert
		capabilities.CanProcess("TestJob").ShouldBeTrue();
		capabilities.CanProcess("testjob").ShouldBeFalse();
		capabilities.CanProcess("TESTJOB").ShouldBeFalse();
	}

	[Fact]
	public void DeduplicateSupportedJobTypes()
	{
		// Act
		var capabilities = new JobInstanceCapabilities(5, DuplicateJobTypes);

		// Assert
		capabilities.SupportedJobTypes.Count.ShouldBe(1);
	}

	[Fact]
	public void AcceptEmptyJobTypes()
	{
		// Act
		var capabilities = new JobInstanceCapabilities(5, Array.Empty<string>());

		// Assert
		capabilities.SupportedJobTypes.ShouldBeEmpty();
		capabilities.CanProcess("AnyJob").ShouldBeFalse();
	}

	[Fact]
	public void AllowNegativePriority()
	{
		// Arrange
		var capabilities = new JobInstanceCapabilities(5, TestJobTypes);

		// Act
		capabilities.Priority = -5;

		// Assert
		capabilities.Priority.ShouldBe(-5);
	}

	[Fact]
	public void ThrowForNegativeMaxConcurrentJobs()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new JobInstanceCapabilities(-1, TestJobTypes));
	}

	[Fact]
	public void AllowMaxConcurrentJobsOfOne()
	{
		// Act
		var capabilities = new JobInstanceCapabilities(1, TestJobTypes);

		// Assert
		capabilities.MaxConcurrentJobs.ShouldBe(1);
	}
}
