// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Inbox.Diagnostics;

namespace Excalibur.Data.SqlServer.Tests.Diagnostics;

/// <summary>
///     Verifies the lease-based claim overload of <see cref="TelemetryInboxStoreDecorator" /> emits its
///     operation telemetry. This overload is the path <c>InboxMiddleware</c> full-mode uses, and it
///     previously forwarded to the inner store without recording any measurement.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class TelemetryInboxDecoratorLeaseClaimTelemetryShould
{
	private const string OperationTagKey = "operation";
	private const string LeaseClaimOperation = "try_claim_lease";

	[Fact]
	public async Task EmitTryClaimLeaseTelemetry_OnTheLeaseClaimOverload()
	{
		var inner = A.Fake<IInboxStore>(b => b.Implements<IClaimableInboxStore>());
		A.CallTo(() => ((IClaimableInboxStore)inner).TryClaimAsync(
				"msg-1", "TestHandler", A<TimeSpan>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		var recordedOperations = new List<string>();

		using var listener = new MeterListener
		{
			InstrumentPublished = (instrument, l) =>
			{
				if (instrument.Meter.Name == TelemetryInboxStoreDecorator.MeterName)
				{
					l.EnableMeasurementEvents(instrument);
				}
			},
		};
		listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
		{
			foreach (var tag in tags)
			{
				if (tag.Key == OperationTagKey && tag.Value is string operation)
				{
					lock (recordedOperations)
					{
						recordedOperations.Add(operation);
					}
				}
			}
		});
		listener.Start();

		var decorator = (IClaimableInboxStore)new TelemetryInboxStoreDecorator(inner);

		_ = await decorator.TryClaimAsync("msg-1", "TestHandler", TimeSpan.FromMinutes(5), CancellationToken.None);

		recordedOperations.ShouldContain(
			LeaseClaimOperation,
			"the lease-claim overload must record its 'try_claim_lease' operation telemetry");
	}
}
