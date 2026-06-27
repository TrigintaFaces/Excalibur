// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Jobs.Aws;
using Excalibur.Jobs.Redis.Coordination;

using Shouldly;

using Xunit;

namespace Excalibur.Jobs.Tests.Serialization;

/// <summary>
/// Verifies that all AOT-safe DTO types round-trip through their source-generated
/// JsonSerializerContext implementations without data loss.
/// Sprint 754 task i7nsac.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AotSerializationRoundTripShould
{
	// -- AWS Jobs: JobSchedulePayload --

	[Fact]
	public void RoundTripJobSchedulePayload()
	{
		// Arrange
		var original = new JobSchedulePayload
		{
			JobType = "MyApp.Jobs.CleanupJob, MyApp",
			JobName = "daily-cleanup"
		};

		// Act
		var json = JsonSerializer.Serialize(original, JobsAwsJsonContext.Default.JobSchedulePayload);
		var deserialized = JsonSerializer.Deserialize(json, JobsAwsJsonContext.Default.JobSchedulePayload);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.JobType.ShouldBe(original.JobType);
		deserialized.JobName.ShouldBe(original.JobName);
	}

	[Fact]
	public void SerializeJobSchedulePayloadWithCorrectPropertyNames()
	{
		// Arrange
		var payload = new JobSchedulePayload
		{
			JobType = "SomeType",
			JobName = "some-job"
		};

		// Act
		var json = JsonSerializer.Serialize(payload, JobsAwsJsonContext.Default.JobSchedulePayload);

		// Assert -- explicit [JsonPropertyName] attributes are honored
		json.ShouldContain("\"JobType\"");
		json.ShouldContain("\"JobName\"");
	}

	// -- Redis Jobs: RedisJobMessage --

	[Fact]
	public void RoundTripRedisJobMessage()
	{
		// Arrange
		var dataElement = JsonSerializer.SerializeToElement(new { foo = "bar" });
		var original = new RedisJobMessage("job-key-1", dataElement);

		// Act
		var json = JsonSerializer.Serialize(original, RedisJobCoordinatorSerializerContext.Default.RedisJobMessage);
		var deserialized = JsonSerializer.Deserialize(json, RedisJobCoordinatorSerializerContext.Default.RedisJobMessage);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.JobKey.ShouldBe("job-key-1");
		deserialized.Data.GetProperty("foo").GetString().ShouldBe("bar");
	}

	// -- Redis Jobs: RedisCompletionData --

	[Fact]
	public void RoundTripRedisCompletionData()
	{
		// Arrange
		var completed = DateTimeOffset.UtcNow;
		var result = JsonSerializer.SerializeToElement(42);
		var original = new RedisCompletionData("job-1", "inst-1", true, result, completed);

		// Act
		var json = JsonSerializer.Serialize(original, RedisJobCoordinatorSerializerContext.Default.RedisCompletionData);
		var deserialized = JsonSerializer.Deserialize(json, RedisJobCoordinatorSerializerContext.Default.RedisCompletionData);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.JobKey.ShouldBe("job-1");
		deserialized.InstanceId.ShouldBe("inst-1");
		deserialized.Success.ShouldBeTrue();
		deserialized.Result.ShouldNotBeNull();
		deserialized.CompletedAt.ShouldBe(completed);
	}

	[Fact]
	public void RoundTripRedisCompletionDataWithNullResult()
	{
		// Arrange -- Result is nullable
		var completed = DateTimeOffset.UtcNow;
		var original = new RedisCompletionData("job-2", "inst-2", false, null, completed);

		// Act
		var json = JsonSerializer.Serialize(original, RedisJobCoordinatorSerializerContext.Default.RedisCompletionData);
		var deserialized = JsonSerializer.Deserialize(json, RedisJobCoordinatorSerializerContext.Default.RedisCompletionData);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.Success.ShouldBeFalse();
		deserialized.Result.ShouldBeNull();
	}

	// -- Redis Jobs: camelCase naming policy --

	[Fact]
	public void SerializeRedisTypesWithCamelCasePropertyNames()
	{
		// Arrange -- RedisJobCoordinatorSerializerContext uses CamelCase policy
		var data = new RedisCompletionData("job-1", "inst", true, null, DateTimeOffset.UtcNow);

		// Act
		var json = JsonSerializer.Serialize(data, RedisJobCoordinatorSerializerContext.Default.RedisCompletionData);

		// Assert
		json.ShouldContain("\"jobKey\"");
		json.ShouldContain("\"instanceId\"");
		json.ShouldContain("\"completedAt\"");
	}
}
