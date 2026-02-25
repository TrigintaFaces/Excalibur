// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextAnomaly"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextAnomalyShould : UnitTestBase
{
	[Fact]
	public void CreateWithAllRequiredProperties()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.InsufficientContext,
			Severity = AnomalySeverity.Medium,
			Description = "Context was unexpectedly cleared",
			MessageId = "msg-123"
		};

		// Assert
		anomaly.Type.ShouldBe(AnomalyType.InsufficientContext);
		anomaly.Severity.ShouldBe(AnomalySeverity.Medium);
		anomaly.Description.ShouldBe("Context was unexpectedly cleared");
		anomaly.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void CreateWithOptionalProperties()
	{
		// Arrange
		var detectedAt = DateTimeOffset.UtcNow;

		// Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.OversizedItem,
			Severity = AnomalySeverity.High,
			Description = "Context size exceeded maximum",
			MessageId = "msg-456",
			DetectedAt = detectedAt,
			SuggestedAction = "Review context payload size"
		};

		// Assert
		anomaly.DetectedAt.ShouldBe(detectedAt);
		anomaly.SuggestedAction.ShouldBe("Review context payload size");
	}

	[Fact]
	public void AllowNullSuggestedAction()
	{
		// Arrange & Act
		var anomaly = new ContextAnomaly
		{
			Type = AnomalyType.MissingCorrelation,
			Severity = AnomalySeverity.High,
			Description = "Required field missing",
			MessageId = "msg-789",
			SuggestedAction = null
		};

		// Assert
		anomaly.SuggestedAction.ShouldBeNull();
	}
}
