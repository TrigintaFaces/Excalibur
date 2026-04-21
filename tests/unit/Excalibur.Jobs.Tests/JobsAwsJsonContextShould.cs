// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Jobs.Aws;

namespace Excalibur.Jobs.Tests;

/// <summary>
/// Round-trip serialization tests for <see cref="JobSchedulePayload"/> via
/// <see cref="JobsAwsJsonContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Serialization")]
public sealed class JobsAwsJsonContextShould
{
    [Fact]
    public void InheritFromJsonSerializerContext()
    {
        var context = JobsAwsJsonContext.Default;
        context.ShouldBeAssignableTo<JsonSerializerContext>();
    }

    [Fact]
    public void ProvideDefaultInstance()
    {
        JobsAwsJsonContext.Default.ShouldNotBeNull();
    }

    [Fact]
    public void HaveJobSchedulePayloadTypeInfo()
    {
        JobsAwsJsonContext.Default
            .GetTypeInfo(typeof(JobSchedulePayload))
            .ShouldNotBeNull();
    }

    [Fact]
    public void RoundTripFullyPopulatedPayload()
    {
        var original = new JobSchedulePayload
        {
            JobType = "MyApp.Jobs.SendEmailJob, MyApp",
            JobName = "daily-email-digest",
        };

        var json = JsonSerializer.Serialize(original, JobsAwsJsonContext.Default.JobSchedulePayload);
        var deserialized = JsonSerializer.Deserialize(json, JobsAwsJsonContext.Default.JobSchedulePayload);

        deserialized.ShouldNotBeNull();
        deserialized.JobType.ShouldBe("MyApp.Jobs.SendEmailJob, MyApp");
        deserialized.JobName.ShouldBe("daily-email-digest");
    }

    [Fact]
    public void ProducePascalCasePropertyNames()
    {
        // JobSchedulePayload uses explicit [JsonPropertyName("JobType")] and [JsonPropertyName("JobName")]
        var payload = new JobSchedulePayload
        {
            JobType = "SomeType",
            JobName = "SomeName",
        };

        var json = JsonSerializer.Serialize(payload, JobsAwsJsonContext.Default.JobSchedulePayload);

        json.ShouldContain("\"JobType\"");
        json.ShouldContain("\"JobName\"");
    }

    [Fact]
    public void PreserveAssemblyQualifiedTypeName()
    {
        var aqn = "MyApp.Jobs.CleanupJob, MyApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        var payload = new JobSchedulePayload
        {
            JobType = aqn,
            JobName = "cleanup",
        };

        var json = JsonSerializer.Serialize(payload, JobsAwsJsonContext.Default.JobSchedulePayload);
        var deserialized = JsonSerializer.Deserialize(json, JobsAwsJsonContext.Default.JobSchedulePayload);

        deserialized.ShouldNotBeNull();
        deserialized.JobType.ShouldBe(aqn);
    }

    [Fact]
    public void NotWriteIndented()
    {
        var options = JobsAwsJsonContext.Default.Options;
        options.WriteIndented.ShouldBeFalse();
    }

    [Fact]
    public void RoundTripWithEmptyStrings()
    {
        var payload = new JobSchedulePayload
        {
            JobType = string.Empty,
            JobName = string.Empty,
        };

        var json = JsonSerializer.Serialize(payload, JobsAwsJsonContext.Default.JobSchedulePayload);
        var deserialized = JsonSerializer.Deserialize(json, JobsAwsJsonContext.Default.JobSchedulePayload);

        deserialized.ShouldNotBeNull();
        deserialized.JobType.ShouldBe(string.Empty);
        deserialized.JobName.ShouldBe(string.Empty);
    }

    [Fact]
    public void ProduceBackwardCompatibleJsonShape()
    {
        // The old code used: new { JobType = ..., JobName = ... }
        // which produced PascalCase keys. Verify the new DTO matches.
        var payload = new JobSchedulePayload
        {
            JobType = "MyApp.TestJob, MyApp",
            JobName = "test-job",
        };

        var json = JsonSerializer.Serialize(payload, JobsAwsJsonContext.Default.JobSchedulePayload);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("JobType", out _).ShouldBeTrue();
        root.TryGetProperty("JobName", out _).ShouldBeTrue();

        // Should NOT have camelCase variants
        root.TryGetProperty("jobType", out _).ShouldBeFalse();
        root.TryGetProperty("jobName", out _).ShouldBeFalse();
    }
}
