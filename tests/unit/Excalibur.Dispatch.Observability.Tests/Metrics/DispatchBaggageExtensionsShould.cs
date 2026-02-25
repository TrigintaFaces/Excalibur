// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

using OpenTelemetry;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="DispatchBaggageExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class DispatchBaggageExtensionsShould
{
	[Fact]
	public void ApplyCustomBaggage_SetsItems()
	{
		// Arrange
		var items = new List<KeyValuePair<string, string?>>
		{
			new("custom.key1", "value1"),
			new("custom.key2", "value2"),
		};

		try
		{
			// Act
			DispatchBaggageExtensions.ApplyCustomBaggage(items);

			// Assert
			var baggage = DispatchBaggageExtensions.GetAllBaggage();
			baggage.ShouldContainKey("custom.key1");
			baggage.ShouldContainKey("custom.key2");
		}
		finally
		{
			DispatchBaggageExtensions.ClearBaggage();
		}
	}

	[Fact]
	public void ApplyCustomBaggage_SkipsNullValues()
	{
		// Arrange
		var items = new List<KeyValuePair<string, string?>>
		{
			new("key-with-null", null),
			new("key-with-empty", ""),
			new("key-with-value", "hello"),
		};

		try
		{
			// Act
			DispatchBaggageExtensions.ApplyCustomBaggage(items);

			// Assert — null/empty values should be skipped
			var baggage = DispatchBaggageExtensions.GetAllBaggage();
			baggage.ShouldContainKey("key-with-value");
		}
		finally
		{
			DispatchBaggageExtensions.ClearBaggage();
		}
	}

	[Fact]
	public void ApplyCustomBaggage_ThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			DispatchBaggageExtensions.ApplyCustomBaggage(null!));
	}

	[Fact]
	public void GetAllBaggage_ReturnsEmptyWhenNoBaggage()
	{
		// Arrange
		DispatchBaggageExtensions.ClearBaggage();

		// Act
		var baggage = DispatchBaggageExtensions.GetAllBaggage();

		// Assert
		baggage.ShouldNotBeNull();
	}

	[Fact]
	public void ClearBaggage_RemovesAllItems()
	{
		// Arrange
		_ = Baggage.SetBaggage("test.clear1", "value1");
		_ = Baggage.SetBaggage("test.clear2", "value2");

		// Act
		DispatchBaggageExtensions.ClearBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldNotContainKey("test.clear1");
		baggage.ShouldNotContainKey("test.clear2");
	}
}
