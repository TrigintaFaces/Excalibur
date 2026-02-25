// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

using OpenTelemetry;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="DispatchBaggageExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class DispatchBaggageExtensionsShould : UnitTestBase
{
	public DispatchBaggageExtensionsShould()
	{
		// Clear baggage before each test
		DispatchBaggageExtensions.ClearBaggage();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			// Clear baggage after each test
			DispatchBaggageExtensions.ClearBaggage();
		}

		base.Dispose(disposing);
	}

	private static HttpContext CreateFakeHttpContext()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ITelemetrySanitizer>(NullTelemetrySanitizer.Instance);
		var serviceProvider = services.BuildServiceProvider();

		var httpContext = A.Fake<HttpContext>();
		A.CallTo(() => httpContext.RequestServices).Returns(serviceProvider);
		return httpContext;
	}

	#region ApplyBaggage Tests

	[Fact]
	public void ApplyBaggage_ThrowOnNullHttpContext()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchBaggageExtensions.ApplyBaggage(null!));
	}

	[Fact]
	public void ApplyBaggage_SetTraceIdentifier()
	{
		// Arrange
		var httpContext = CreateFakeHttpContext();
		A.CallTo(() => httpContext.TraceIdentifier).Returns("trace-id-123");
		A.CallTo(() => httpContext.Request.Headers).Returns(new HeaderDictionary());

		// Act
		httpContext.ApplyBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("trace.id");
		baggage["trace.id"].ShouldBe("trace-id-123");
	}

	[Fact]
	public void ApplyBaggage_SetTenantIdFromHeader()
	{
		// Arrange
		var headers = new HeaderDictionary
		{
			["X-Tenant-ID"] = new StringValues("tenant-123")
		};
		var httpContext = CreateFakeHttpContext();
		A.CallTo(() => httpContext.Request.Headers).Returns(headers);

		// Act
		httpContext.ApplyBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("tenant.id");
		baggage["tenant.id"].ShouldBe("tenant-123");
	}

	[Fact]
	public void ApplyBaggage_SetCorrelationIdFromHeader()
	{
		// Arrange
		var headers = new HeaderDictionary
		{
			["X-Correlation-ID"] = new StringValues("corr-456")
		};
		var httpContext = CreateFakeHttpContext();
		A.CallTo(() => httpContext.Request.Headers).Returns(headers);

		// Act
		httpContext.ApplyBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("correlation.id");
		baggage["correlation.id"].ShouldBe("corr-456");
	}

	[Fact]
	public void ApplyBaggage_SetRequestIdFromHeader()
	{
		// Arrange
		var headers = new HeaderDictionary
		{
			["X-Request-ID"] = new StringValues("req-789")
		};
		var httpContext = CreateFakeHttpContext();
		A.CallTo(() => httpContext.Request.Headers).Returns(headers);

		// Act
		httpContext.ApplyBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("request.id");
		baggage["request.id"].ShouldBe("req-789");
	}

	[Fact]
	public void ApplyBaggage_SetUserIdFromClaims()
	{
		// Arrange
		var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-123") };
		var identity = new ClaimsIdentity(claims, "test");
		var principal = new ClaimsPrincipal(identity);

		var httpContext = CreateFakeHttpContext();
		A.CallTo(() => httpContext.User).Returns(principal);
		A.CallTo(() => httpContext.Request.Headers).Returns(new HeaderDictionary());

		// Act
		httpContext.ApplyBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("user.id");
		baggage["user.id"].ShouldBe("user-123");
	}

	[Fact]
	public void ApplyBaggage_SetUserNameFromClaims()
	{
		// Arrange
		var claims = new[] { new Claim(ClaimTypes.Name, "John Doe") };
		var identity = new ClaimsIdentity(claims, "test");
		var principal = new ClaimsPrincipal(identity);

		var httpContext = CreateFakeHttpContext();
		A.CallTo(() => httpContext.User).Returns(principal);
		A.CallTo(() => httpContext.Request.Headers).Returns(new HeaderDictionary());

		// Act
		httpContext.ApplyBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("user.name");
		baggage["user.name"].ShouldBe("John Doe");
	}

	[Fact]
	public void ApplyBaggage_IgnoreEmptyValues()
	{
		// Arrange
		var headers = new HeaderDictionary
		{
			["X-Tenant-ID"] = new StringValues("")
		};
		var httpContext = CreateFakeHttpContext();
		A.CallTo(() => httpContext.TraceIdentifier).Returns("");
		A.CallTo(() => httpContext.Request.Headers).Returns(headers);

		// Act
		httpContext.ApplyBaggage();

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldNotContainKey("trace.id");
		baggage.ShouldNotContainKey("tenant.id");
	}

	#endregion

	#region ApplyCustomBaggage Tests

	[Fact]
	public void ApplyCustomBaggage_ThrowOnNullBaggageItems()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchBaggageExtensions.ApplyCustomBaggage(null!));
	}

	[Fact]
	public void ApplyCustomBaggage_SetCustomItems()
	{
		// Arrange
		var items = new[]
		{
			new KeyValuePair<string, string?>("custom.key1", "value1"),
			new KeyValuePair<string, string?>("custom.key2", "value2")
		};

		// Act
		DispatchBaggageExtensions.ApplyCustomBaggage(items);

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("custom.key1");
		baggage["custom.key1"].ShouldBe("value1");
		baggage.ShouldContainKey("custom.key2");
		baggage["custom.key2"].ShouldBe("value2");
	}

	[Fact]
	public void ApplyCustomBaggage_IgnoreNullValues()
	{
		// Arrange
		var items = new[]
		{
			new KeyValuePair<string, string?>("key1", "value1"),
			new KeyValuePair<string, string?>("key2", null)
		};

		// Act
		DispatchBaggageExtensions.ApplyCustomBaggage(items);

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("key1");
		baggage.ShouldNotContainKey("key2");
	}

	[Fact]
	public void ApplyCustomBaggage_IgnoreEmptyValues()
	{
		// Arrange
		var items = new[]
		{
			new KeyValuePair<string, string?>("key1", "value1"),
			new KeyValuePair<string, string?>("key2", "")
		};

		// Act
		DispatchBaggageExtensions.ApplyCustomBaggage(items);

		// Assert
		var baggage = DispatchBaggageExtensions.GetAllBaggage();
		baggage.ShouldContainKey("key1");
		baggage.ShouldNotContainKey("key2");
	}

	[Fact]
	public void ApplyCustomBaggage_HandleEmptyCollection()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() =>
			DispatchBaggageExtensions.ApplyCustomBaggage(Array.Empty<KeyValuePair<string, string?>>()));
	}

	#endregion

	#region GetAllBaggage Tests

	[Fact]
	public void GetAllBaggage_ReturnEmptyWhenNoBaggage()
	{
		// Act
		var baggage = DispatchBaggageExtensions.GetAllBaggage();

		// Assert
		baggage.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllBaggage_ReturnAllSetItems()
	{
		// Arrange
		_ = Baggage.SetBaggage("key1", "value1");
		_ = Baggage.SetBaggage("key2", "value2");

		// Act
		var baggage = DispatchBaggageExtensions.GetAllBaggage();

		// Assert
		baggage.Count.ShouldBe(2);
		baggage.ShouldContainKey("key1");
		baggage.ShouldContainKey("key2");
	}

	#endregion

	#region ClearBaggage Tests

	[Fact]
	public void ClearBaggage_RemoveAllItems()
	{
		// Arrange
		_ = Baggage.SetBaggage("key1", "value1");
		_ = Baggage.SetBaggage("key2", "value2");
		DispatchBaggageExtensions.GetAllBaggage().Count.ShouldBe(2);

		// Act
		DispatchBaggageExtensions.ClearBaggage();

		// Assert
		DispatchBaggageExtensions.GetAllBaggage().ShouldBeEmpty();
	}

	[Fact]
	public void ClearBaggage_HandleEmptyBaggage()
	{
		// Act & Assert - Should not throw
		Should.NotThrow(() => DispatchBaggageExtensions.ClearBaggage());
	}

	#endregion
}
