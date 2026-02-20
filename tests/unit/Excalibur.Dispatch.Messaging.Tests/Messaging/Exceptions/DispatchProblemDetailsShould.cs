// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
///     Tests for the <see cref="DispatchProblemDetails" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchProblemDetailsShould
{
	[Fact]
	public void HaveDefaultTypeOfAboutBlank()
	{
		var details = new DispatchProblemDetails();
		details.Type.ShouldBe("about:blank");
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var timestamp = DateTimeOffset.UtcNow;
		var details = new DispatchProblemDetails
		{
			Type = "urn:dispatch:error:validation",
			Title = "Validation Failed",
			Status = 400,
			Detail = "The input was invalid",
			Instance = "urn:excalibur:error:abc123",
			ErrorCode = "VAL001",
			Category = "Validation",
			Severity = "Warning",
			CorrelationId = "corr-123",
			TraceId = "trace-456",
			SpanId = "span-789",
			Timestamp = timestamp,
			SuggestedAction = "Fix input",
		};

		details.Type.ShouldBe("urn:dispatch:error:validation");
		details.Title.ShouldBe("Validation Failed");
		details.Status.ShouldBe(400);
		details.Detail.ShouldBe("The input was invalid");
		details.Instance.ShouldBe("urn:excalibur:error:abc123");
		details.ErrorCode.ShouldBe("VAL001");
		details.Category.ShouldBe("Validation");
		details.Severity.ShouldBe("Warning");
		details.CorrelationId.ShouldBe("corr-123");
		details.TraceId.ShouldBe("trace-456");
		details.SpanId.ShouldBe("span-789");
		details.Timestamp.ShouldBe(timestamp);
		details.SuggestedAction.ShouldBe("Fix input");
	}
}
