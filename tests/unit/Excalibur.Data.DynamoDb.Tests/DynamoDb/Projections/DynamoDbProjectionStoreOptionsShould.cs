// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Data.DynamoDb.Projections;

namespace Excalibur.Data.Tests.DynamoDb.Projections;

/// <summary>
/// Tests for <see cref="DynamoDbProjectionStoreOptions"/> — AOT-safe serialization (bd-yd29oo).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DynamoDb")]
public sealed class DynamoDbProjectionStoreOptionsShould
{
	[Fact]
	public void JsonSerializerOptions_DefaultsToNull()
	{
		// Arrange & Act
		var options = new DynamoDbProjectionStoreOptions();

		// Assert — null means store uses internal camelCase options
		options.JsonSerializerOptions.ShouldBeNull();
	}

	[Fact]
	public void JsonSerializerOptions_AcceptsCustomValue()
	{
		// Arrange
		var options = new DynamoDbProjectionStoreOptions();
		var custom = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		};

		// Act
		options.JsonSerializerOptions = custom;

		// Assert
		options.JsonSerializerOptions.ShouldBeSameAs(custom);
	}

	[Fact]
	public void JsonSerializerOptions_CanBeResetToNull()
	{
		// Arrange
		var options = new DynamoDbProjectionStoreOptions();
		options.JsonSerializerOptions = new JsonSerializerOptions();

		// Act — consumer resets to null (back to default behavior)
		options.JsonSerializerOptions = null;

		// Assert
		options.JsonSerializerOptions.ShouldBeNull();
	}
}
