// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.A3.Audit;

using Shouldly;

using Xunit;

namespace Excalibur.A3.Tests.Serialization;

/// <summary>
/// Verifies A3 audit types round-trip through the source-generated AuditJsonContext.
/// Sprint 754 task i7nsac.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditJsonContextShould
{
	[Fact]
	public void RoundTripRaisedBy()
	{
		// Arrange
		var original = new RaisedBy("John Doe", "jdoe")
		{
			FirstName = "John",
			LastName = "Doe",
			UserId = "user-123"
		};

		// Act
		var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.RaisedBy);
		var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.RaisedBy);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.FullName.ShouldBe("John Doe");
		deserialized.Login.ShouldBe("jdoe");
		deserialized.FirstName.ShouldBe("John");
		deserialized.LastName.ShouldBe("Doe");
		deserialized.UserId.ShouldBe("user-123");
	}

	[Fact]
	public void RoundTripRaisedByWithNullFields()
	{
		// Arrange -- minimal RaisedBy with null optional fields
		var original = new RaisedBy();

		// Act
		var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.RaisedBy);
		var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.RaisedBy);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.FullName.ShouldBeNull();
		deserialized.Login.ShouldBeNull();
	}

	[Fact]
	public void RoundTripDictionaryStringString()
	{
		// Arrange -- AuditJsonContext declares Dictionary<string, string>
		var original = new Dictionary<string, string>
		{
			["key1"] = "value1",
			["key2"] = "value2"
		};

		// Act
		var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.DictionaryStringString);
		var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.DictionaryStringString);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Count.ShouldBe(2);
		deserialized["key1"].ShouldBe("value1");
		deserialized["key2"].ShouldBe("value2");
	}

	[Fact]
	public void RoundTripEmptyDictionary()
	{
		// Arrange
		var original = new Dictionary<string, string>();

		// Act
		var json = JsonSerializer.Serialize(original, AuditJsonContext.Default.DictionaryStringString);
		var deserialized = JsonSerializer.Deserialize(json, AuditJsonContext.Default.DictionaryStringString);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldBeEmpty();
	}
}
