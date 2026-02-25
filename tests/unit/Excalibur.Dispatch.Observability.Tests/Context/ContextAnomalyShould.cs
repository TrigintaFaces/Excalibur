// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextAnomaly"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextAnomalyShould
{
	#region Required Property Tests

	[Fact]
	public void RequireType()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.MissingCorrelation,
			Severity = AnomalySeverity.High,
			Description = "Missing correlation ID",
			MessageId = "msg-123",
		};

		// Assert
		anomaly.Type.ShouldBe(AnomalyType.MissingCorrelation);
	}

	[Fact]
	public void RequireSeverity()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.PotentialPII,
			Severity = AnomalySeverity.Medium,
			Description = "Potential PII detected",
			MessageId = "msg-456",
		};

		// Assert
		anomaly.Severity.ShouldBe(AnomalySeverity.Medium);
	}

	[Fact]
	public void RequireDescription()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.ExcessiveContext,
			Severity = AnomalySeverity.Low,
			Description = "Too many context fields",
			MessageId = "msg-789",
		};

		// Assert
		anomaly.Description.ShouldBe("Too many context fields");
	}

	[Fact]
	public void RequireMessageId()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.OversizedItem,
			Severity = AnomalySeverity.High,
			Description = "Context item too large",
			MessageId = "msg-abc",
		};

		// Assert
		anomaly.MessageId.ShouldBe("msg-abc");
	}

	#endregion

	#region Optional Property Tests

	[Fact]
	public void HaveDefaultDetectedAtValue()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.CircularCausation,
			Severity = AnomalySeverity.High,
			Description = "Circular causation detected",
			MessageId = "msg-def",
		};

		// Assert
		anomaly.DetectedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void AllowSettingDetectedAt()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.InsufficientContext,
			Severity = AnomalySeverity.Medium,
			Description = "Insufficient context",
			MessageId = "msg-ghi",
			DetectedAt = timestamp,
		};

		// Assert
		anomaly.DetectedAt.ShouldBe(timestamp);
	}

	[Fact]
	public void HaveNullSuggestedActionByDefault()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.MissingCorrelation,
			Severity = AnomalySeverity.High,
			Description = "Missing correlation",
			MessageId = "msg-jkl",
		};

		// Assert
		anomaly.SuggestedAction.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingSuggestedAction()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.OversizedItem,
			Severity = AnomalySeverity.Medium,
			Description = "Context item too large",
			MessageId = "msg-mno",
			SuggestedAction = "Consider using claim check pattern for large payloads",
		};

		// Assert
		anomaly.SuggestedAction.ShouldBe("Consider using claim check pattern for large payloads");
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var detectedAt = DateTimeOffset.UtcNow;

		// Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.PotentialPII,
			Severity = AnomalySeverity.High,
			Description = "Email address detected in context",
			MessageId = "msg-pqr",
			DetectedAt = detectedAt,
			SuggestedAction = "Mask or remove PII before adding to context",
		};

		// Assert
		anomaly.Type.ShouldBe(AnomalyType.PotentialPII);
		anomaly.Severity.ShouldBe(AnomalySeverity.High);
		anomaly.Description.ShouldBe("Email address detected in context");
		anomaly.MessageId.ShouldBe("msg-pqr");
		anomaly.DetectedAt.ShouldBe(detectedAt);
		anomaly.SuggestedAction.ShouldBe("Mask or remove PII before adding to context");
	}

	[Theory]
	[InlineData(AnomalyType.MissingCorrelation, AnomalySeverity.High)]
	[InlineData(AnomalyType.InsufficientContext, AnomalySeverity.Medium)]
	[InlineData(AnomalyType.ExcessiveContext, AnomalySeverity.Low)]
	[InlineData(AnomalyType.CircularCausation, AnomalySeverity.High)]
	[InlineData(AnomalyType.PotentialPII, AnomalySeverity.High)]
	[InlineData(AnomalyType.OversizedItem, AnomalySeverity.Medium)]
	public void SupportAllTypeAndSeverityCombinations(AnomalyType type, AnomalySeverity severity)
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = type,
			Severity = severity,
			Description = $"Test anomaly: {type}",
			MessageId = "msg-test",
		};

		// Assert
		anomaly.Type.ShouldBe(type);
		anomaly.Severity.ShouldBe(severity);
	}

	#endregion
}
