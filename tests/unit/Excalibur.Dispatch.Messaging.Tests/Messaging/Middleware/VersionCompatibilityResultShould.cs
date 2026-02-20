// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="VersionCompatibilityResult"/>.
/// </summary>
/// <remarks>
/// Tests the version compatibility result factory methods and properties.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class VersionCompatibilityResultShould
{
	#region Compatible Factory Tests

	[Fact]
	public void Compatible_ReturnsCompatibleStatus()
	{
		// Act
		var result = VersionCompatibilityResult.Compatible();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(VersionCompatibilityStatus.Compatible);
	}

	[Fact]
	public void Compatible_ReturnsNullReason()
	{
		// Act
		var result = VersionCompatibilityResult.Compatible();

		// Assert
		result.Reason.ShouldBeNull();
	}

	[Fact]
	public void Compatible_ReturnsSameStatusOnMultipleCalls()
	{
		// Act
		var result1 = VersionCompatibilityResult.Compatible();
		var result2 = VersionCompatibilityResult.Compatible();

		// Assert
		result1.Status.ShouldBe(result2.Status);
	}

	#endregion

	#region Deprecated Factory Tests

	[Fact]
	public void Deprecated_ReturnsDeprecatedStatus()
	{
		// Act
		var result = VersionCompatibilityResult.Deprecated("This version is deprecated");

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(VersionCompatibilityStatus.Deprecated);
	}

	[Fact]
	public void Deprecated_ReturnsCorrectReason()
	{
		// Arrange
		const string reason = "Use version 2.0 instead";

		// Act
		var result = VersionCompatibilityResult.Deprecated(reason);

		// Assert
		result.Reason.ShouldBe(reason);
	}

	[Fact]
	public void Deprecated_WithEmptyReason_Works()
	{
		// Act
		var result = VersionCompatibilityResult.Deprecated(string.Empty);

		// Assert
		result.Status.ShouldBe(VersionCompatibilityStatus.Deprecated);
		result.Reason.ShouldBe(string.Empty);
	}

	[Fact]
	public void Deprecated_PreservesReasonWithSpecialCharacters()
	{
		// Arrange
		const string reason = "Version 1.x is deprecated! Please upgrade to v2.0+";

		// Act
		var result = VersionCompatibilityResult.Deprecated(reason);

		// Assert
		result.Reason.ShouldBe(reason);
	}

	#endregion

	#region Incompatible Factory Tests

	[Fact]
	public void Incompatible_ReturnsIncompatibleStatus()
	{
		// Act
		var result = VersionCompatibilityResult.Incompatible("Version is not supported");

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(VersionCompatibilityStatus.Incompatible);
	}

	[Fact]
	public void Incompatible_ReturnsCorrectReason()
	{
		// Arrange
		const string reason = "Version 0.x is no longer supported";

		// Act
		var result = VersionCompatibilityResult.Incompatible(reason);

		// Assert
		result.Reason.ShouldBe(reason);
	}

	[Fact]
	public void Incompatible_WithEmptyReason_Works()
	{
		// Act
		var result = VersionCompatibilityResult.Incompatible(string.Empty);

		// Assert
		result.Status.ShouldBe(VersionCompatibilityStatus.Incompatible);
		result.Reason.ShouldBe(string.Empty);
	}

	#endregion

	#region Unknown Factory Tests

	[Fact]
	public void Unknown_ReturnsUnknownStatus()
	{
		// Act
		var result = VersionCompatibilityResult.Unknown("Version could not be determined");

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe(VersionCompatibilityStatus.Unknown);
	}

	[Fact]
	public void Unknown_ReturnsCorrectReason()
	{
		// Arrange
		const string reason = "Version header not present";

		// Act
		var result = VersionCompatibilityResult.Unknown(reason);

		// Assert
		result.Reason.ShouldBe(reason);
	}

	[Fact]
	public void Unknown_WithEmptyReason_Works()
	{
		// Act
		var result = VersionCompatibilityResult.Unknown(string.Empty);

		// Assert
		result.Status.ShouldBe(VersionCompatibilityStatus.Unknown);
		result.Reason.ShouldBe(string.Empty);
	}

	#endregion

	#region Status Property Tests

	[Fact]
	public void Status_ReturnsCorrectEnumValue()
	{
		// Arrange
		var compatible = VersionCompatibilityResult.Compatible();
		var deprecated = VersionCompatibilityResult.Deprecated("reason");
		var incompatible = VersionCompatibilityResult.Incompatible("reason");
		var unknown = VersionCompatibilityResult.Unknown("reason");

		// Assert
		compatible.Status.ShouldBe(VersionCompatibilityStatus.Compatible);
		deprecated.Status.ShouldBe(VersionCompatibilityStatus.Deprecated);
		incompatible.Status.ShouldBe(VersionCompatibilityStatus.Incompatible);
		unknown.Status.ShouldBe(VersionCompatibilityStatus.Unknown);
	}

	#endregion

	#region Reason Property Tests

	[Fact]
	public void Reason_ForCompatible_IsNull()
	{
		// Act
		var result = VersionCompatibilityResult.Compatible();

		// Assert
		result.Reason.ShouldBeNull();
	}

	[Fact]
	public void Reason_ForNonCompatible_IsNotNull()
	{
		// Act
		var deprecated = VersionCompatibilityResult.Deprecated("reason");
		var incompatible = VersionCompatibilityResult.Incompatible("reason");
		var unknown = VersionCompatibilityResult.Unknown("reason");

		// Assert
		_ = deprecated.Reason.ShouldNotBeNull();
		_ = incompatible.Reason.ShouldNotBeNull();
		_ = unknown.Reason.ShouldNotBeNull();
	}

	[Fact]
	public void Reason_WithLongMessage_Works()
	{
		// Arrange
		var longReason = new string('x', 5000);

		// Act
		var result = VersionCompatibilityResult.Incompatible(longReason);

		// Assert
		result.Reason.ShouldBe(longReason);
		result.Reason.Length.ShouldBe(5000);
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Status_IsReadOnly()
	{
		// Arrange
		var result = VersionCompatibilityResult.Compatible();

		// Assert - Properties are get-only (verified by compilation)
		result.Status.ShouldBe(VersionCompatibilityStatus.Compatible);
	}

	[Fact]
	public void Reason_IsReadOnly()
	{
		// Arrange
		var result = VersionCompatibilityResult.Deprecated("old");

		// Assert - Properties are get-only (verified by compilation)
		result.Reason.ShouldBe("old");
	}

	#endregion
}
