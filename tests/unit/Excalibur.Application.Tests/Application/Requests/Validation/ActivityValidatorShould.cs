// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Validation;

namespace Excalibur.Tests.Application.Requests.Validation;

/// <summary>
/// Unit tests for <see cref="ActivityValidator{TRequest}"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Validation")]
public sealed class ActivityValidatorShould : UnitTestBase
{
	private readonly ActivityValidator<TestActivity> _validator = new();

	#region Valid Activity Tests

	[Fact]
	public void Validate_ValidActivity_ReturnsNoErrors()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = "MyNamespace:MyActivity",
			ActivityDisplayName = "My Activity",
			ActivityDescription = "This is a test activity",
			ActivityType = ActivityType.Command
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.Count.ShouldBe(0);
	}

	#endregion

	#region ActivityName Validation Tests

	[Fact]
	public void Validate_EmptyActivityName_ReturnsError()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = string.Empty,
			ActivityDisplayName = "My Activity",
			ActivityDescription = "Description",
			ActivityType = ActivityType.Command
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "ActivityName");
	}

	[Fact]
	public void Validate_NullActivityName_ReturnsError()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = null!,
			ActivityDisplayName = "My Activity",
			ActivityDescription = "Description",
			ActivityType = ActivityType.Command
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "ActivityName");
	}

	#endregion

	#region ActivityDisplayName Validation Tests

	[Fact]
	public void Validate_EmptyActivityDisplayName_ReturnsError()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = "MyNamespace:MyActivity",
			ActivityDisplayName = string.Empty,
			ActivityDescription = "Description",
			ActivityType = ActivityType.Command
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "ActivityDisplayName");
	}

	#endregion

	#region ActivityDescription Validation Tests

	[Fact]
	public void Validate_EmptyActivityDescription_ReturnsError()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = "MyNamespace:MyActivity",
			ActivityDisplayName = "My Activity",
			ActivityDescription = string.Empty,
			ActivityType = ActivityType.Command
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "ActivityDescription");
	}

	#endregion

	#region ActivityType Validation Tests

	[Fact]
	public void Validate_UnknownActivityType_ReturnsError()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = "MyNamespace:MyActivity",
			ActivityDisplayName = "My Activity",
			ActivityDescription = "Description",
			ActivityType = ActivityType.Unknown
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "ActivityType");
	}

	[Theory]
	[InlineData(ActivityType.Command)]
	[InlineData(ActivityType.Query)]
	[InlineData(ActivityType.Notification)]
	[InlineData(ActivityType.Job)]
	public void Validate_ValidActivityType_ReturnsNoErrors(ActivityType activityType)
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = "MyNamespace:MyActivity",
			ActivityDisplayName = "My Activity",
			ActivityDescription = "Description",
			ActivityType = activityType
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Multiple Errors Tests

	[Fact]
	public void Validate_MultipleInvalidFields_ReturnsAllErrors()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = string.Empty,
			ActivityDisplayName = string.Empty,
			ActivityDescription = string.Empty,
			ActivityType = ActivityType.Unknown
		};

		// Act
		var result = _validator.Validate(activity);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(4);
	}

	#endregion

	#region Async Validation Tests

	[Fact]
	public async Task ValidateAsync_ValidActivity_ReturnsNoErrors()
	{
		// Arrange
		var activity = new TestActivity
		{
			ActivityName = "MyNamespace:MyActivity",
			ActivityDisplayName = "My Activity",
			ActivityDescription = "Description",
			ActivityType = ActivityType.Command
		};

		// Act
		var result = await _validator.ValidateAsync(activity);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Test Implementation

	private sealed class TestActivity : IActivity
	{
		public required string ActivityName { get; init; }
		public required string ActivityDisplayName { get; init; }
		public required string ActivityDescription { get; init; }
		public required ActivityType ActivityType { get; init; }

		// IAmCorrelatable
		public Guid CorrelationId { get; init; } = Guid.NewGuid();

		// IAmMultiTenant
		public string TenantId { get; init; } = "test-tenant";
	}

	#endregion
}
