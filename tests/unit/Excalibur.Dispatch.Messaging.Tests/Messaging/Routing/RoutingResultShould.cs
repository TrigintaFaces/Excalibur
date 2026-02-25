// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
/// Unit tests for <see cref="RoutingResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Routing")]
[Trait("Priority", "0")]
public sealed class RoutingResultShould
{
	#region Default Value Tests

	[Fact]
	public void Default_BusName_IsNull()
	{
		// Arrange & Act
		var result = new RoutingResult();

		// Assert
		result.BusName.ShouldBeNull();
	}

	[Fact]
	public void Default_Metadata_IsNull()
	{
		// Arrange & Act
		var result = new RoutingResult();

		// Assert
		result.Metadata.ShouldBeNull();
	}

	#endregion

	#region BusName Tests

	[Fact]
	public void BusName_CanBeSet()
	{
		// Arrange
		var result = new RoutingResult();

		// Act
		result.BusName = "kafka-primary";

		// Assert
		result.BusName.ShouldBe("kafka-primary");
	}

	[Fact]
	public void BusName_CanBeSetToEmpty()
	{
		// Arrange
		var result = new RoutingResult();

		// Act
		result.BusName = string.Empty;

		// Assert
		result.BusName.ShouldBe(string.Empty);
	}

	[Fact]
	public void BusName_CanBeSetToNull()
	{
		// Arrange
		var result = new RoutingResult { BusName = "some-bus" };

		// Act
		result.BusName = null;

		// Assert
		result.BusName.ShouldBeNull();
	}

	#endregion

	#region Metadata Tests

	[Fact]
	public void Metadata_CanBeSetViaInit()
	{
		// Act
		var result = new RoutingResult
		{
			Metadata = new Dictionary<string, object>
			{
				["key1"] = "value1",
				["key2"] = 42,
			},
		};

		// Assert
		_ = result.Metadata.ShouldNotBeNull();
		result.Metadata["key1"].ShouldBe("value1");
		result.Metadata["key2"].ShouldBe(42);
	}

	[Fact]
	public void Metadata_CanContainMultipleEntries()
	{
		// Act
		var result = new RoutingResult
		{
			Metadata = new Dictionary<string, object>
			{
				["region"] = "us-east-1",
				["priority"] = 100,
				["tags"] = new List<string> { "important", "production" },
			},
		};

		// Assert
		result.Metadata.Count.ShouldBe(3);
	}

	[Fact]
	public void Metadata_CanBeEmptyDictionary()
	{
		// Act
		var result = new RoutingResult
		{
			Metadata = new Dictionary<string, object>(),
		};

		// Assert
		_ = result.Metadata.ShouldNotBeNull();
		result.Metadata.ShouldBeEmpty();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var result = new RoutingResult
		{
			BusName = "rabbitmq-cluster",
			Metadata = new Dictionary<string, object>
			{
				["routed-at"] = DateTimeOffset.UtcNow,
			},
		};

		// Assert
		result.BusName.ShouldBe("rabbitmq-cluster");
		_ = result.Metadata.ShouldNotBeNull();
		result.Metadata.ContainsKey("routed-at").ShouldBeTrue();
	}

	#endregion
}
