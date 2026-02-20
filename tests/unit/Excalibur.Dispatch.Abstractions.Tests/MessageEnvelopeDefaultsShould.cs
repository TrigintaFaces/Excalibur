// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Tests for MessageEnvelope default nested types (DefaultValidationResult, DefaultAuthorizationResult,
/// DefaultRoutingResult, DefaultMessageVersionMetadata) exercised through public API.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageEnvelopeDefaultsShould : UnitTestBase
{
	#region DefaultValidationResult via MessageEnvelope

	[Fact]
	public void DefaultValidationResult_HasEmptyErrors()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.ValidationResult.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultValidationResult_IsValid_ReturnsTrue()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.ValidationResult.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ValidationResult_SetToNull_ResetsToDefault()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		var custom = SerializableValidationResult.Failed("test error");
		envelope.ValidationResult = custom;
		envelope.ValidationResult.IsValid.ShouldBeFalse();

		// Act
		envelope.ValidationResult = null!;

		// Assert - should reset to default valid result
		(envelope.ValidationResult is not null).ShouldBeTrue();
		envelope.ValidationResult.IsValid.ShouldBeTrue();
		envelope.ValidationResult.Errors.Count.ShouldBe(0);
	}

	#endregion

	#region DefaultAuthorizationResult via MessageEnvelope

	[Fact]
	public void DefaultAuthorizationResult_IsAuthorized_ReturnsTrue()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.AuthorizationResult.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void DefaultAuthorizationResult_FailureMessage_IsNull()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.AuthorizationResult.FailureMessage.ShouldBeNull();
	}

	[Fact]
	public void AuthorizationResult_SetToNull_ResetsToDefault()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		var custom = A.Fake<IAuthorizationResult>();
		envelope.AuthorizationResult = custom;

		// Act
		envelope.AuthorizationResult = null!;

		// Assert - should reset to default
		(envelope.AuthorizationResult is not null).ShouldBeTrue();
		envelope.AuthorizationResult.IsAuthorized.ShouldBeTrue();
	}

	#endregion

	#region DefaultRoutingResult via MessageEnvelope

	[Fact]
	public void DefaultRoutingResult_IsSuccess_ReturnsTrue()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.RoutingDecision.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void DefaultRoutingResult_FailureReason_IsNull()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.RoutingDecision.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void DefaultRoutingDecision_IsSuccess_ReturnsTrue()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		_ = envelope.RoutingDecision.ShouldNotBeNull();
		envelope.RoutingDecision.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void DefaultRoutingDecision_Transport_IsLocal()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		_ = envelope.RoutingDecision.ShouldNotBeNull();
		envelope.RoutingDecision.Transport.ShouldBe("local");
	}

	[Fact]
	public void DefaultRoutingDecision_Endpoints_IsEmpty()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		_ = envelope.RoutingDecision.ShouldNotBeNull();
		envelope.RoutingDecision.Endpoints.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultRoutingDecision_MatchedRules_IsEmpty()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		_ = envelope.RoutingDecision.ShouldNotBeNull();
		envelope.RoutingDecision.MatchedRules.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultRoutingDecision_FailureReason_IsNull()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		_ = envelope.RoutingDecision.ShouldNotBeNull();
		envelope.RoutingDecision.FailureReason.ShouldBeNull();
	}

	#endregion

	#region DefaultMessageVersionMetadata via MessageEnvelope

	[Fact]
	public void DefaultVersionMetadata_Version_IsOne()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.VersionMetadata.Version.ShouldBe(1);
	}

	[Fact]
	public void DefaultVersionMetadata_SchemaVersion_IsOne()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.VersionMetadata.SchemaVersion.ShouldBe(1);
	}

	[Fact]
	public void DefaultVersionMetadata_SerializerVersion_IsOne()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.VersionMetadata.SerializerVersion.ShouldBe(1);
	}

	[Fact]
	public void DefaultVersionMetadata_Version_CanBeSet()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.VersionMetadata.Version = 2;

		// Assert
		envelope.VersionMetadata.Version.ShouldBe(2);
	}

	[Fact]
	public void DefaultVersionMetadata_SchemaVersion_CanBeSet()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.VersionMetadata.SchemaVersion = 3;

		// Assert
		envelope.VersionMetadata.SchemaVersion.ShouldBe(3);
	}

	[Fact]
	public void DefaultVersionMetadata_SerializerVersion_CanBeSet()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.VersionMetadata.SerializerVersion = 5;

		// Assert
		envelope.VersionMetadata.SerializerVersion.ShouldBe(5);
	}

	#endregion

	#region Reset and Clone exercising defaults

	[Fact]
	public void Reset_RestoresDefaultValidationResult()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.ValidationResult = SerializableValidationResult.Failed("err");

		// Act
		envelope.Reset();

		// Assert
		envelope.ValidationResult.IsValid.ShouldBeTrue();
		envelope.ValidationResult.Errors.Count.ShouldBe(0);
	}

	[Fact]
	public void Reset_RestoresDefaultAuthorizationResult()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		var custom = A.Fake<IAuthorizationResult>();
		A.CallTo(() => custom.IsAuthorized).Returns(false);
		envelope.AuthorizationResult = custom;

		// Act
		envelope.Reset();

		// Assert
		envelope.AuthorizationResult.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void Reset_RestoresDefaultRoutingDecision()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.Reset();

		// Assert
		envelope.RoutingDecision.IsSuccess.ShouldBeTrue();
		envelope.RoutingDecision.Transport.ShouldBe("local");
		envelope.RoutingDecision.Endpoints.ShouldBeEmpty();
	}

	[Fact]
	public void Reset_RestoresDefaultVersionMetadata()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.VersionMetadata.Version = 99;

		// Act
		envelope.Reset();

		// Assert
		envelope.VersionMetadata.Version.ShouldBe(1);
		envelope.VersionMetadata.SchemaVersion.ShouldBe(1);
		envelope.VersionMetadata.SerializerVersion.ShouldBe(1);
	}

	[Fact]
	public void Reset_ClearsItems()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.SetItem("key", "value");

		// Act
		envelope.Reset();

		// Assert
		envelope.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void Reset_ClearsHeaders()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.Headers["X-Custom"] = "val";

		// Act
		envelope.Reset();

		// Assert
		envelope.Headers.ShouldBeEmpty();
	}

	[Fact]
	public void Reset_ClearsCorrelationId()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.CorrelationId = "corr-123";

		// Act
		envelope.Reset();

		// Assert
		envelope.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void Clone_PreservesValidationResult()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		using var clone = envelope.Clone();

		// Assert
		clone.ValidationResult.IsValid.ShouldBeTrue();
		clone.ValidationResult.Errors.Count.ShouldBe(0);
	}

	[Fact]
	public void Clone_PreservesRoutingDecision()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		using var clone = envelope.Clone();

		// Assert
		clone.RoutingDecision.IsSuccess.ShouldBeTrue();
		clone.RoutingDecision.Transport.ShouldBe("local");
		clone.RoutingDecision.Endpoints.ShouldBeEmpty();
		clone.RoutingDecision.MatchedRules.ShouldBeEmpty();
		clone.RoutingDecision.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void Clone_PreservesVersionMetadata()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.VersionMetadata.Version = 7;

		// Act
		using var clone = envelope.Clone();

		// Assert
		clone.VersionMetadata.Version.ShouldBe(7);
	}

	[Fact]
	public void Clone_PreservesCorrelationId()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.CorrelationId = "clone-test";

		// Act
		using var clone = envelope.Clone();

		// Assert
		clone.CorrelationId.ShouldBe("clone-test");
	}

	[Fact]
	public void Clone_PreservesHeaders()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.Headers["X-Test"] = "header-value";

		// Act
		using var clone = envelope.Clone();

		// Assert
		clone.Headers.ShouldContainKey("X-Test");
		clone.Headers["X-Test"].ShouldBe("header-value");
	}

	[Fact]
	public void Success_Property_ReflectsDefaultResults()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert - Success depends on ValidationResult.IsValid and AuthorizationResult.IsAuthorized
		envelope.Success.ShouldBeTrue();
	}

	#endregion
}
