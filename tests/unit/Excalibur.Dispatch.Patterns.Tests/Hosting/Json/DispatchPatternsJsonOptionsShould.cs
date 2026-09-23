// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Shape locks for <see cref="DispatchPatternsJsonOptions" />.
/// </summary>
/// <remarks>
/// Behavioral coverage — that these members actually reach the serializer — lives in
/// <see cref="DispatchPatternsJsonOptionsReachTheSerializerShould" />. Asserting a value back off this
/// DTO proves only that a property setter works, which stays true even if the registration discards it.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DispatchPatternsJsonOptionsShould
{
	[Fact]
	public void DefaultBothMembersToNull()
	{
		var options = new DispatchPatternsJsonOptions();

		options.ConfigureSerializer.ShouldBeNull();
		options.SerializerContext.ShouldBeNull();
	}

	[Fact]
	public void RetainAConfigureSerializerDelegate()
	{
		Action<JsonSerializerOptions> configure = static json => json.WriteIndented = true;

		var options = new DispatchPatternsJsonOptions { ConfigureSerializer = configure };

		options.ConfigureSerializer.ShouldBeSameAs(configure);
	}
}
