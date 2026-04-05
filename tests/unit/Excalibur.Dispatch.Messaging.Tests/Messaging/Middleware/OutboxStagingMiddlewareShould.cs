// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
///     Tests for the <see cref="OutboxStagingMiddleware" /> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class OutboxStagingMiddlewareShould
{
	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new OutboxStagingMiddleware(null!, null, NullLogger<OutboxStagingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new OutboxStagingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions()),
				null, null!));

	[Fact]
	public void ThrowWhenEnabledWithNoOutboxServices()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });

		Should.Throw<InvalidOperationException>(() =>
			new OutboxStagingMiddleware(options, null, NullLogger<OutboxStagingMiddleware>.Instance));
	}

	[Fact]
	public void CreateSuccessfullyWhenDisabled()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = false });

		var middleware = new OutboxStagingMiddleware(options, null, NullLogger<OutboxStagingMiddleware>.Instance);
		middleware.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSuccessfullyWithOutboxStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });
		var store = A.Fake<IOutboxStore>();

		var middleware = new OutboxStagingMiddleware(options, store, NullLogger<OutboxStagingMiddleware>.Instance);
		middleware.ShouldNotBeNull();
	}

	// Sprint 683 T.17: IOutboxService deleted -- CreateSuccessfullyWithOutboxService test removed
}
