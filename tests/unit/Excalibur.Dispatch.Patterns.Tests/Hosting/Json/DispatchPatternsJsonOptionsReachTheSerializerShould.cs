// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Locks that configuration passed to <c>AddJsonSerialization</c> reaches the resolved serializer.
/// </summary>
/// <remarks>
/// These assert on serializer OUTPUT, not on the options object. A test that reads the value back off
/// the DTO passes whether or not the serializer ever sees it, so it cannot detect a registration that
/// discards the configuration.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DispatchPatternsJsonOptionsReachTheSerializerShould
{
	private sealed record Payload(string Name, int Count);

	[Fact]
	public void ApplyConfigureSerializer_ToSerializedOutput()
	{
		var services = new ServiceCollection();
		_ = services.AddJsonSerialization(o => o.ConfigureSerializer = json => json.WriteIndented = true);

		var serializer = services.BuildServiceProvider().GetRequiredService<DispatchJsonSerializer>();
		var json = serializer.Serialize(new Payload("excalibur", 7));

		json.ShouldContain("\n", Case.Sensitive);
	}

	[Fact]
	public void LeaveOutputCompact_WhenNothingIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddJsonSerialization();

		var serializer = services.BuildServiceProvider().GetRequiredService<DispatchJsonSerializer>();
		var json = serializer.Serialize(new Payload("excalibur", 7));

		json.ShouldNotContain("\n", Case.Sensitive);
	}

	[Fact]
	public void ApplyConfigureSerializer_OverTheSerializerDefaults()
	{
		var services = new ServiceCollection();
		// camelCase is a serializer default; the delegate runs last and must be able to override it.
		_ = services.AddJsonSerialization(
			o => o.ConfigureSerializer = json => json.PropertyNamingPolicy = null);

		var serializer = services.BuildServiceProvider().GetRequiredService<DispatchJsonSerializer>();
		var json = serializer.Serialize(new Payload("excalibur", 7));

		json.ShouldContain("\"Name\"", Case.Sensitive);
	}
}
