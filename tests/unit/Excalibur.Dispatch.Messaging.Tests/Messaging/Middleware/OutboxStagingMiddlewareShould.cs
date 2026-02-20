// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
///     Tests for the <see cref="OutboxStagingMiddleware" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxStagingMiddlewareShould
{
	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new OutboxStagingMiddleware(null!, null, null, NullLogger<OutboxStagingMiddleware>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new OutboxStagingMiddleware(
				Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions()),
				null, null, null!));

	[Fact]
	public void ThrowWhenEnabledWithNoOutboxServices()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });

		Should.Throw<InvalidOperationException>(() =>
			new OutboxStagingMiddleware(options, null, null, NullLogger<OutboxStagingMiddleware>.Instance));
	}

	[Fact]
	public void CreateSuccessfullyWhenDisabled()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = false });

		var middleware = new OutboxStagingMiddleware(options, null, null, NullLogger<OutboxStagingMiddleware>.Instance);
		middleware.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSuccessfullyWithOutboxStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });
		var store = A.Fake<IOutboxStore>();

		var middleware = new OutboxStagingMiddleware(options, store, null, NullLogger<OutboxStagingMiddleware>.Instance);
		middleware.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSuccessfullyWithOutboxService()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxStagingOptions { Enabled = true });
		var service = A.Fake<IOutboxService>();

		var middleware = new OutboxStagingMiddleware(options, null, service, NullLogger<OutboxStagingMiddleware>.Instance);
		middleware.ShouldNotBeNull();
	}
}
