// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Bus;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

/// <summary>
///     Runs the shared <see cref="IMessageBusAdapter" /> conformance suite against <see cref="InMemoryMessageBusAdapter" />.
/// </summary>
public sealed class InMemoryMessageBusAdapterConformanceShould : MessageBusAdapterConformanceTests
{
	protected override string ExpectedAdapterName => "InMemory";

	protected override IMessageBusAdapter CreateAdapter() =>
		new InMemoryMessageBusAdapter(NullLogger<InMemoryMessageBusAdapter>.Instance);

	protected override MessageBusOptions CreateTestOptions() => A.Fake<MessageBusOptions>();

	protected override IDispatchMessage CreateTestMessage() => A.Fake<IDispatchMessage>();

	protected override IMessageContext CreateTestMessageContext() => A.Fake<IMessageContext>();

	[Fact]
	public async Task RefuseOptionsItCannotHonourRatherThanDiscardingThem()
	{
		// InitializeAsync used to validate non-null and then read none of the eight properties it was
		// handed, so a consumer configuring the bus got their configuration silently ignored and a call
		// that succeeded told them nothing.
		var adapter = new InMemoryMessageBusAdapter(NullLogger<InMemoryMessageBusAdapter>.Instance);

		var retries = new UnsupportedProbeOptions { EnableRetries = true };
		var addressed = new UnsupportedProbeOptions { TargetUri = new Uri("amqp://localhost") };

		_ = await Should.ThrowAsync<ArgumentException>(
			() => adapter.InitializeAsync(retries, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => adapter.InitializeAsync(addressed, CancellationToken.None));
	}

	[Fact]
	public async Task AcceptOptionsItCanHonour()
	{
		// LIVENESS. The refusal must be specific to what this adapter cannot do; a blanket throw would
		// satisfy the arm above and make the adapter unusable.
		var adapter = new InMemoryMessageBusAdapter(NullLogger<InMemoryMessageBusAdapter>.Instance);

		await adapter.InitializeAsync(
			new UnsupportedProbeOptions { Name = "in-process", EnableTelemetry = true },
			CancellationToken.None);
	}

	private sealed class UnsupportedProbeOptions : MessageBusOptions;
}
