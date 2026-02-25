// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AzureFunctions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureFunctionsServerlessContextShould : UnitTestBase
{
	[Fact]
	public void ThrowWhenPlatformContextIsNotFunctionContext()
	{
		var ex = Should.Throw<ArgumentException>(() => new AzureFunctionsServerlessContext(new object(), NullLogger.Instance));
		ex.Message.ShouldContain("FunctionContext");
	}

	[Fact]
	public void ExposeRequestIdAndPlatformMetadata()
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "orders-eastus");
		Environment.SetEnvironmentVariable("WEBSITE_OWNER_NAME", "subscription+tenant");
		Environment.SetEnvironmentVariable("WEBSITE_RESOURCE_GROUP", "orders-rg");

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.RequestId.ShouldBe("func-id");
		sut.FunctionName.ShouldBe("orders-eastus");
		sut.CloudProvider.ShouldBe("Azure");
		sut.Platform.ShouldBe(ServerlessPlatform.AzureFunctions);
		sut.AccountId.ShouldBe("subscription");
		sut.Region.ShouldBe("eastus");
		sut.InvokedFunctionArn.ShouldContain("/subscriptions/subscription/resourceGroups/orders-rg/providers/Microsoft.Web/sites/orders-eastus/functions/orders-eastus");
	}

	[Theory]
	[InlineData("Dynamic", 1536)]
	[InlineData("ElasticPremium", 3584)]
	[InlineData("Other", 1536)]
	public void ResolveMemoryLimitByPlan(string sku, int expectedMb)
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_SKU", sku);

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.MemoryLimitInMB.ShouldBe(expectedMb);
	}

	[Fact]
	public void FunctionVersion_DefaultAndEnvironmentOverride()
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", null);
		using (var defaultVersion = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance))
		{
			defaultVersion.FunctionVersion.ShouldBe("Production");
		}

		Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "Staging");
		using (var stagingVersion = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance))
		{
			stagingVersion.FunctionVersion.ShouldBe("Staging");
		}
	}

	[Fact]
	public void ExecutionDeadline_ReflectSkuTimeoutPolicy()
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_SKU", "Dynamic");
		using (var dynamicContext = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance))
		{
			var delta = dynamicContext.ExecutionDeadline - DateTimeOffset.UtcNow;
			delta.ShouldBeGreaterThan(TimeSpan.FromMinutes(4));
			delta.ShouldBeLessThan(TimeSpan.FromMinutes(6));
		}

		Environment.SetEnvironmentVariable("WEBSITE_SKU", "ElasticPremium");
		using (var premiumContext = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance))
		{
			var delta = premiumContext.ExecutionDeadline - DateTimeOffset.UtcNow;
			delta.ShouldBeGreaterThan(TimeSpan.FromMinutes(29));
		}
	}

	[Fact]
	public void ExposeFunctionContextService()
	{
		var functionContext = CreateFunctionContext("func-id");
		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.GetService(typeof(AzureFunctionsServerlessContext)).ShouldBeSameAs(sut);
		sut.GetService(typeof(FunctionContext)).ShouldBeNull();
	}

	[Fact]
	public void InvokedFunctionArn_FallbackToUnknownSegments_WhenEnvironmentIsIncomplete()
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", null);
		Environment.SetEnvironmentVariable("WEBSITE_OWNER_NAME", null);
		Environment.SetEnvironmentVariable("WEBSITE_RESOURCE_GROUP", null);

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.InvokedFunctionArn.ShouldContain("/subscriptions/unknown/resourceGroups/unknown/providers/Microsoft.Web/sites/unknown/functions/unknown");
	}

	[Fact]
	public void ExposeLogGroupAndStreamMetadata()
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "orders-eastus");
		Environment.SetEnvironmentVariable("WEBSITE_OWNER_NAME", null);
		Environment.SetEnvironmentVariable("REGION_NAME", null);

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.LogGroupName.ShouldContain("/subscriptions/unknown/resourceGroups/eastus/providers/Microsoft.Web/sites/orders-eastus/functions/orders-eastus");
		sut.LogStreamName.ShouldStartWith("orders-eastus-");
	}

	[Fact]
	public void AccountId_ReturnUnknown_WhenOwnerNameMissing()
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_OWNER_NAME", null);

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.AccountId.ShouldBe("unknown");
	}

	[Fact]
	public void ExecutionDeadline_DefaultToConsumptionTimeout_WhenSkuUnknown()
	{
		var functionContext = CreateFunctionContext("func-id");
		Environment.SetEnvironmentVariable("WEBSITE_SKU", "UnknownPlan");

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		var delta = sut.ExecutionDeadline - DateTimeOffset.UtcNow;
		delta.ShouldBeGreaterThan(TimeSpan.FromMinutes(4));
		delta.ShouldBeLessThan(TimeSpan.FromMinutes(6));
	}

	[Fact]
	public void ExposeUnderlyingFunctionContext()
	{
		var functionContext = CreateFunctionContext("func-id");

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.FunctionContext.ShouldBeSameAs(functionContext);
	}

	[Fact]
	public void TraceContext_ReturnNull_WhenTraceParentIsMissing()
	{
		var traceContext = A.Fake<Microsoft.Azure.Functions.Worker.TraceContext>();
		A.CallTo(() => traceContext.TraceParent).Returns(string.Empty);

		var functionContext = CreateFunctionContext("func-id");
		A.CallTo(() => functionContext.TraceContext).Returns(traceContext);

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.TraceContext.ShouldBeNull();
	}

	[Fact]
	public void TraceContext_ReturnNull_WhenTraceParentIsInvalid()
	{
		var traceContext = A.Fake<Microsoft.Azure.Functions.Worker.TraceContext>();
		A.CallTo(() => traceContext.TraceParent).Returns("invalid-trace-parent");

		var functionContext = CreateFunctionContext("func-id");
		A.CallTo(() => functionContext.TraceContext).Returns(traceContext);

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.TraceContext.ShouldBeNull();
	}

	[Fact]
	public void TraceContext_ReturnParsedW3CValues_WhenTraceParentIsValid()
	{
		const string traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-a2fb4a1d1a96d312-01";
		var traceContext = A.Fake<Microsoft.Azure.Functions.Worker.TraceContext>();
		A.CallTo(() => traceContext.TraceParent).Returns(traceParent);
		A.CallTo(() => traceContext.TraceState).Returns("tenant=orders");

		var functionContext = CreateFunctionContext("func-id");
		A.CallTo(() => functionContext.TraceContext).Returns(traceContext);

		using var sut = new AzureFunctionsServerlessContext(functionContext, NullLogger.Instance);

		sut.TraceContext.ShouldNotBeNull();
		sut.TraceContext!.TraceId.ShouldBe("4bf92f3577b34da6a3ce929d0e0e4736");
		sut.TraceContext.SpanId.ShouldBe("a2fb4a1d1a96d312");
		sut.TraceContext.TraceFlags.ShouldBe("01");
		sut.TraceContext.TraceState.ShouldBe("tenant=orders");
	}

	private static FunctionContext CreateFunctionContext(string functionId)
	{
		var functionContext = A.Fake<FunctionContext>();
		A.CallTo(() => functionContext.FunctionId).Returns(functionId);
		A.CallTo(() => functionContext.FunctionDefinition).Returns(null!);
		return functionContext;
	}
}
