// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class LongPollingServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsLongPolling((LongPollingOptions)null!));
	}

	[Fact]
	public void ThrowWhenServicesIsNull_Action()
	{
		Should.Throw<ArgumentNullException>(() =>
			LongPollingServiceCollectionExtensions.AddAwsLongPolling(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureActionIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsLongPolling((Action<LongPollingOptions>)null!));
	}

}
