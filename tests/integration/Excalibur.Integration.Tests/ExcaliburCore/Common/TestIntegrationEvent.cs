// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Integration.Tests.ExcaliburCore.Common;

public sealed record TestIntegrationEvent(string DestinationEndpoint = null) : IIntegrationEvent
{
	public string MessageId { get; init; } = Guid.NewGuid().ToString();

	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();

	public object Body { get; init; } = "test-event-body";

	public string MessageType { get; init; } = "TestIntegrationEvent";

	public Microsoft.AspNetCore.Http.Features.IFeatureCollection Features { get; init; } =
		new Microsoft.AspNetCore.Http.Features.FeatureCollection();
}
