// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;
using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="CloudProviderValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class CloudProviderValidatorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void SetPriorityTo20()
	{
		// Act
		var validator = new TestableCloudValidator("TestCloud");

		// Assert
		validator.Priority.ShouldBe(20);
	}

	[Fact]
	public void SetConfigurationNameFromConstructor()
	{
		// Act
		var validator = new TestableCloudValidator("MyCloud");

		// Assert
		validator.ConfigurationName.ShouldBe("MyCloud");
	}

	#endregion

	#region ValidateRegion Tests

	[Fact]
	public void ValidateRegion_ReturnsTrue_WhenRegionIsValid()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		var validRegions = new HashSet<string> { "us-east-1", "us-west-2", "eu-west-1" };

		// Act
		var result = TestableCloudValidator.TestValidateRegion("us-east-1", validRegions, errors, "Region");

		// Assert
		result.ShouldBeTrue();
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateRegion_ReturnsFalse_WhenRegionIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		var validRegions = new HashSet<string> { "us-east-1", "us-west-2" };

		// Act
		var result = TestableCloudValidator.TestValidateRegion(null, validRegions, errors, "Region");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing or empty");
	}

	[Fact]
	public void ValidateRegion_ReturnsFalse_WhenRegionIsEmpty()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		var validRegions = new HashSet<string> { "us-east-1", "us-west-2" };

		// Act
		var result = TestableCloudValidator.TestValidateRegion("", validRegions, errors, "Region");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateRegion_ReturnsFalse_WhenRegionIsInvalid()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		var validRegions = new HashSet<string> { "us-east-1", "us-west-2" };

		// Act
		var result = TestableCloudValidator.TestValidateRegion("invalid-region", validRegions, errors, "Region");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("Invalid cloud region");
	}

	[Fact]
	public void ValidateRegion_IncludesRecommendation()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		var validRegions = new HashSet<string> { "us-east-1", "us-west-2", "eu-west-1" };

		// Act
		_ = TestableCloudValidator.TestValidateRegion(null, validRegions, errors, "Region");

		// Assert
		errors[0].Recommendation.ShouldNotBeNull();
		errors[0].Recommendation.ShouldContain("us-east-1");
	}

	[Fact]
	public void ValidateRegion_ThrowsArgumentNull_WhenValidRegionsIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableCloudValidator.TestValidateRegion("us-east-1", null!, errors, "Region"));
	}

	[Fact]
	public void ValidateRegion_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Arrange
		var validRegions = new HashSet<string> { "us-east-1" };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableCloudValidator.TestValidateRegion("us-east-1", validRegions, null!, "Region"));
	}

	#endregion

	#region ValidateArn Tests

	[Fact]
	public void ValidateArn_ReturnsTrue_WhenArnIsValid()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		const string validArn = "arn:aws:iam::123456789012:role/MyRole";

		// Act
		var result = TestableCloudValidator.TestValidateArn(validArn, errors, "Arn");

		// Assert
		result.ShouldBeTrue();
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateArn_ReturnsTrue_WhenArnHasNoRegion()
	{
		// Arrange - IAM ARNs don't have region
		var errors = new List<ConfigurationValidationError>();
		const string validArn = "arn:aws:iam::123456789012:user/TestUser";

		// Act
		var result = TestableCloudValidator.TestValidateArn(validArn, errors, "Arn");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ValidateArn_ReturnsTrue_WhenArnHasRegion()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		const string validArn = "arn:aws:sqs:us-east-1:123456789012:my-queue";

		// Act
		var result = TestableCloudValidator.TestValidateArn(validArn, errors, "Arn");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ValidateArn_ReturnsFalse_WhenArnIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableCloudValidator.TestValidateArn(null, errors, "Arn");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing or empty");
	}

	[Fact]
	public void ValidateArn_ReturnsFalse_WhenArnIsEmpty()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableCloudValidator.TestValidateArn("", errors, "Arn");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateArn_ReturnsFalse_WhenArnHasInvalidFormat()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableCloudValidator.TestValidateArn("not-an-arn", errors, "Arn");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("Invalid ARN format");
	}

	[Fact]
	public void ValidateArn_ReturnsFalse_WhenArnDoesNotStartWithArn()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableCloudValidator.TestValidateArn("urn:aws:iam::123456789012:role/MyRole", errors, "Arn");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateArn_IncludesRecommendation()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		_ = TestableCloudValidator.TestValidateArn("invalid", errors, "Arn");

		// Assert
		errors[0].Recommendation.ShouldNotBeNull();
		errors[0].Recommendation.ShouldContain("arn:partition:service:region:account:resource");
	}

	[Fact]
	public void ValidateArn_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableCloudValidator.TestValidateArn("arn:aws:iam::123456789012:role/MyRole", null!, "Arn"));
	}

	#endregion

	#region ValidateAzureResourceId Tests

	[Fact]
	public void ValidateAzureResourceId_ReturnsTrue_WhenResourceIdIsValid()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		const string validResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/mystorageaccount";

		// Act
		var result = TestableCloudValidator.TestValidateAzureResourceId(validResourceId, errors, "ResourceId");

		// Assert
		result.ShouldBeTrue();
		errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidateAzureResourceId_ReturnsFalse_WhenResourceIdIsNull()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableCloudValidator.TestValidateAzureResourceId(null, errors, "ResourceId");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("missing or empty");
	}

	[Fact]
	public void ValidateAzureResourceId_ReturnsFalse_WhenResourceIdIsEmpty()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableCloudValidator.TestValidateAzureResourceId("", errors, "ResourceId");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
	}

	[Fact]
	public void ValidateAzureResourceId_ReturnsFalse_WhenResourceIdDoesNotStartWithSubscriptions()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		var result = TestableCloudValidator.TestValidateAzureResourceId("/resourceGroups/my-rg", errors, "ResourceId");

		// Assert
		result.ShouldBeFalse();
		errors.Count.ShouldBe(1);
		errors[0].Message.ShouldContain("Invalid Azure resource ID format");
	}

	[Fact]
	public void ValidateAzureResourceId_IncludesRecommendation()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();

		// Act
		_ = TestableCloudValidator.TestValidateAzureResourceId("/invalid", errors, "ResourceId");

		// Assert
		errors[0].Recommendation.ShouldNotBeNull();
		errors[0].Recommendation.ShouldContain("/subscriptions/");
	}

	[Fact]
	public void ValidateAzureResourceId_ThrowsArgumentNull_WhenErrorsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			TestableCloudValidator.TestValidateAzureResourceId("/subscriptions/123", null!, "ResourceId"));
	}

	[Fact]
	public void ValidateAzureResourceId_IsCaseInsensitive()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>();
		const string resourceId = "/SUBSCRIPTIONS/12345678-1234-1234-1234-123456789012/resourceGroups/my-rg";

		// Act
		var result = TestableCloudValidator.TestValidateAzureResourceId(resourceId, errors, "ResourceId");

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region Test Helper

	/// <summary>
	/// Testable concrete implementation of CloudProviderValidator.
	/// </summary>
	private sealed class TestableCloudValidator : CloudProviderValidator
	{
		public TestableCloudValidator(string configurationName)
			: base(configurationName)
		{
		}

		public override Task<ConfigurationValidationResult> ValidateAsync(
			IConfiguration configuration,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(ConfigurationValidationResult.Success());

		public static bool TestValidateRegion(
			string? region,
			IReadOnlySet<string> validRegions,
			ICollection<ConfigurationValidationError> errors,
			string configPath)
			=> ValidateRegion(region, validRegions, errors, configPath);

		public static bool TestValidateArn(
			string? arn,
			ICollection<ConfigurationValidationError> errors,
			string configPath)
			=> ValidateArn(arn, errors, configPath);

		public static bool TestValidateAzureResourceId(
			string? resourceId,
			ICollection<ConfigurationValidationError> errors,
			string configPath)
			=> ValidateAzureResourceId(resourceId, errors, configPath);
	}

	#endregion
}
