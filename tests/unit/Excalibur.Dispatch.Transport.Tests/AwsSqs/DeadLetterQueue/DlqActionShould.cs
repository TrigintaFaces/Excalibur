// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DlqActionShould
{
	[Fact]
	public void HaveExpectedValues()
	{
		DlqAction.None.ShouldBe((DlqAction)0);
		DlqAction.Redriven.ShouldBe((DlqAction)1);
		DlqAction.RetryFailed.ShouldBe((DlqAction)2);
		DlqAction.Archived.ShouldBe((DlqAction)3);
		DlqAction.Deleted.ShouldBe((DlqAction)4);
		DlqAction.Skipped.ShouldBe((DlqAction)5);
	}

	[Fact]
	public void DefaultToNone()
	{
		default(DlqAction).ShouldBe(DlqAction.None);
	}

	[Fact]
	public void HaveAllExpectedValues()
	{
		var values = Enum.GetValues<DlqAction>();
		values.Length.ShouldBe(6);
	}
}
