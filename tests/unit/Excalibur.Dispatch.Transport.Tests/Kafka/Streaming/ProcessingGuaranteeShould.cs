// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Streaming;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ProcessingGuaranteeShould
{
	[Theory]
	[InlineData(ProcessingGuarantee.AtLeastOnce, 0)]
	[InlineData(ProcessingGuarantee.ExactlyOnce, 1)]
	public void HaveCorrectValues(ProcessingGuarantee guarantee, int expected)
	{
		((int)guarantee).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllMembers()
	{
		Enum.GetValues<ProcessingGuarantee>().Length.ShouldBe(2);
	}
}
