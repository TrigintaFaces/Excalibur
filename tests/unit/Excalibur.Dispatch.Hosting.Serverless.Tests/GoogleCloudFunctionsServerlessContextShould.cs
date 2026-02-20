// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GoogleCloudFunctionsServerlessContextShould : UnitTestBase
{
	[Fact]
	public void ResolveIdentityMetadataFromEnvironment()
	{
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "orders-handler");
		Environment.SetEnvironmentVariable("K_REVISION", "rev-4");
		Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "proj-123");
		Environment.SetEnvironmentVariable("GOOGLE_CLOUD_REGION", "europe-west1");

		using var sut = new GoogleCloudFunctionsServerlessContext(new object(), NullLogger.Instance);

		sut.FunctionName.ShouldBe("orders-handler");
		sut.FunctionVersion.ShouldBe("rev-4");
		sut.AccountId.ShouldBe("proj-123");
		sut.Region.ShouldBe("europe-west1");
		sut.CloudProvider.ShouldBe("Google Cloud");
		sut.Platform.ShouldBe(ServerlessPlatform.GoogleCloudFunctions);
		sut.InvokedFunctionArn.ShouldBe("projects/proj-123/locations/europe-west1/functions/orders-handler");
	}

	[Fact]
	public void FallBackWhenProjectOrRegionAreMissing()
	{
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "orders-handler");
		Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", null);
		Environment.SetEnvironmentVariable("GOOGLE_CLOUD_REGION", null);
		Environment.SetEnvironmentVariable("FUNCTION_REGION", "us-central2");

		using var sut = new GoogleCloudFunctionsServerlessContext(new object(), NullLogger.Instance);

		sut.Region.ShouldBe("us-central2");
		sut.AccountId.ShouldBe("unknown");
		sut.InvokedFunctionArn.ShouldBe("projects/unknown/locations/us-central2/functions/orders-handler");
	}

	[Theory]
	[InlineData("1024", 1024)]
	[InlineData(null, 256)]
	public void ResolveMemoryLimitFromEnvironment(string? memory, int expected)
	{
		Environment.SetEnvironmentVariable("FUNCTION_MEMORY_MB", memory);
		using var sut = new GoogleCloudFunctionsServerlessContext(new object(), NullLogger.Instance);

		sut.MemoryLimitInMB.ShouldBe(expected);
	}

	[Fact]
	public void RequestId_UseEnvironmentExecutionIdWhenPresent()
	{
		Environment.SetEnvironmentVariable("FUNCTION_EXECUTION_ID", "exec-abc");
		using var sut = new GoogleCloudFunctionsServerlessContext(new object(), NullLogger.Instance);

		sut.RequestId.ShouldBe("exec-abc");
	}

	[Fact]
	public void ExecutionDeadline_ReflectTimeoutConfiguration()
	{
		Environment.SetEnvironmentVariable("FUNCTION_TIMEOUT_SEC", "120");
		using var configured = new GoogleCloudFunctionsServerlessContext(new object(), NullLogger.Instance);
		var configuredDelta = configured.ExecutionDeadline - DateTimeOffset.UtcNow;
		configuredDelta.ShouldBeGreaterThan(TimeSpan.FromSeconds(110));

		Environment.SetEnvironmentVariable("FUNCTION_TIMEOUT_SEC", null);
		using var fallback = new GoogleCloudFunctionsServerlessContext(new object(), NullLogger.Instance);
		var fallbackDelta = fallback.ExecutionDeadline - DateTimeOffset.UtcNow;
		fallbackDelta.ShouldBeGreaterThan(TimeSpan.FromSeconds(55));
	}

	[Fact]
	public void BuildLogGroupAndStreamValues()
	{
		Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "proj-123");
		Environment.SetEnvironmentVariable("FUNCTION_NAME", "orders-handler");

		using var sut = new GoogleCloudFunctionsServerlessContext(new object(), NullLogger.Instance);

		sut.LogGroupName.ShouldContain("projects/proj-123/logs/cloudfunctions");
		sut.LogStreamName.ShouldContain("orders-handler-");
	}
}
