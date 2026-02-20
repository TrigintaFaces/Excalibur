// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for <see cref="GrpcTransportOptions"/> covering
/// DataAnnotations validation, Range constraint edge cases, and Required attribute behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportOptionsDepthShould
{
	[Fact]
	public void FailValidation_WhenServerAddressIsEmpty()
	{
		// Arrange
		var options = new GrpcTransportOptions { ServerAddress = string.Empty };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		isValid.ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(GrpcTransportOptions.ServerAddress)));
	}

	[Fact]
	public void PassValidation_WhenServerAddressIsSet()
	{
		// Arrange
		var options = new GrpcTransportOptions { ServerAddress = "https://localhost:5001" };
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		isValid.ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void FailValidation_WhenDeadlineSecondsIsBelowMinimum()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			DeadlineSeconds = 0,
		};
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		isValid.ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(GrpcTransportOptions.DeadlineSeconds)));
	}

	[Fact]
	public void FailValidation_WhenDeadlineSecondsExceedsMaximum()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			DeadlineSeconds = 3601,
		};
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void PassValidation_WhenDeadlineSecondsIsAtMinimum()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			DeadlineSeconds = 1,
		};
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public void PassValidation_WhenDeadlineSecondsIsAtMaximum()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			DeadlineSeconds = 3600,
		};
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public void FailValidation_WhenDeadlineSecondsIsNegative()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			DeadlineSeconds = -1,
		};
		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Act
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void HaveRequiredAttributeOnServerAddress()
	{
		// Assert
		var property = typeof(GrpcTransportOptions).GetProperty(nameof(GrpcTransportOptions.ServerAddress));
		property.ShouldNotBeNull();
		var attr = property.GetCustomAttributes(typeof(RequiredAttribute), false);
		attr.Length.ShouldBe(1);
	}

	[Fact]
	public void HaveRangeAttributeOnDeadlineSeconds()
	{
		// Assert
		var property = typeof(GrpcTransportOptions).GetProperty(nameof(GrpcTransportOptions.DeadlineSeconds));
		property.ShouldNotBeNull();
		var rangeAttr = (RangeAttribute?)property.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault();
		rangeAttr.ShouldNotBeNull();
		rangeAttr.Minimum.ShouldBe(1);
		rangeAttr.Maximum.ShouldBe(3600);
	}

	[Fact]
	public void BeSealed()
	{
		typeof(GrpcTransportOptions).IsSealed.ShouldBeTrue();
	}
}
