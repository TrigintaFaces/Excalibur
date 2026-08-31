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
}
