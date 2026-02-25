// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DlqAnalysisResultShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var result = new DlqAnalysisResult();

		// Assert
		result.ShouldMoveToDeadLetter.ShouldBeFalse();
		result.Reason.ShouldBeNull();
		result.IsRecoverable.ShouldBeFalse();
		result.RecommendedAction.ShouldBe(DlqAction.None);
		result.SuggestedRetryDelay.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var result = new DlqAnalysisResult
		{
			ShouldMoveToDeadLetter = true,
			Reason = "Max retries exceeded",
			IsRecoverable = true,
			RecommendedAction = DlqAction.Redriven,
			SuggestedRetryDelay = TimeSpan.FromSeconds(30),
		};

		// Assert
		result.ShouldMoveToDeadLetter.ShouldBeTrue();
		result.Reason.ShouldBe("Max retries exceeded");
		result.IsRecoverable.ShouldBeTrue();
		result.RecommendedAction.ShouldBe(DlqAction.Redriven);
		result.SuggestedRetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void SupportArchivedAction()
	{
		// Arrange & Act
		var result = new DlqAnalysisResult
		{
			ShouldMoveToDeadLetter = true,
			Reason = "Poison message detected",
			IsRecoverable = false,
			RecommendedAction = DlqAction.Archived,
		};

		// Assert
		result.RecommendedAction.ShouldBe(DlqAction.Archived);
		result.IsRecoverable.ShouldBeFalse();
	}
}
