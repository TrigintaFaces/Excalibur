// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Categories;

using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class GooglePubSubDisposalShould
{
	[Fact]
	[UnitTest]
	[Trait(TraitNames.Component, TestComponents.Transport)]
	[Trait("Pattern", "TRANSPORT")]
	public void DisposeMetricsWithoutThrowing()
	{
		var metrics = new GooglePubSubMetrics();
		metrics.Dispose();
	}
}
