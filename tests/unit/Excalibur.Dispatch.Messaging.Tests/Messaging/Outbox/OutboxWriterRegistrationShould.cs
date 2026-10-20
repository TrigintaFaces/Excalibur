// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class OutboxWriterRegistrationShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ResolveWriterFromLeanOutboxComposition(bool configured)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch(builder =>
		{
			if (configured) { builder.UseOutbox(options => options.Enabled = true); }
			else { builder.UseOutbox(); }
		});
		using var provider = services.BuildServiceProvider(validateScopes: true);
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IOutboxWriter>().ShouldNotBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void PreserveExplicitWriter(bool configured)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		var writer = A.Fake<IOutboxWriter>();
		services.AddSingleton(writer);
		services.AddDispatch(builder =>
		{
			if (configured) { builder.UseOutbox(options => options.Enabled = true); }
			else { builder.UseOutbox(); }
		});
		using var provider = services.BuildServiceProvider(validateScopes: true);
		using var scope = provider.CreateScope();
		scope.ServiceProvider.GetRequiredService<IOutboxWriter>().ShouldBeSameAs(writer);
	}
}
