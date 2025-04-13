using System.Net;

using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Hosting.Web;

using FakeItEasy;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Tests.Unit.Http;

public class ApplicationBuilderExtensionsShould
{
	[Fact]
	public async Task PopulateTenantIdFromHttpContext()
	{
		var services = new ServiceCollection();
		var tenantId = A.Fake<ITenantId>();
		_ = services.AddSingleton(tenantId);

		var serviceProvider = services.BuildServiceProvider();

		var app = new ApplicationBuilder(serviceProvider);

		var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
		httpContext.Features.Set<IRoutingFeature>(new RoutingFeature
		{
			RouteData = new RouteData(new RouteValueDictionary { { "tenantId", "test-tenant" } })
		});

		var middleware = new RequestDelegate(context =>
		{
			context.RequestServices.GetRequiredService<ITenantId>().Value = context.TenantId();
			return Task.CompletedTask;
		});

		await middleware(httpContext).ConfigureAwait(true);
		_ = A.CallToSet(() => tenantId.Value).To("test-tenant").MustHaveHappened();
	}

	[Fact]
	public async Task PopulateCorrelationIdFromHttpContext()
	{
		var services = new ServiceCollection();
		var correlationId = A.Fake<ICorrelationId>();
		_ = services.AddSingleton(correlationId);

		var serviceProvider = services.BuildServiceProvider();

		var expectedGuid = Guid.NewGuid();
		var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
		httpContext.Request.Headers[ExcaliburHeaderNames.CorrelationId] = expectedGuid.ToString();

		var middleware = new RequestDelegate(context =>
		{
			context.RequestServices.GetRequiredService<ICorrelationId>().Value = context.CorrelationId();
			return Task.CompletedTask;
		});

		await middleware(httpContext).ConfigureAwait(true);
		_ = A.CallToSet(() => correlationId.Value).To(expectedGuid).MustHaveHappened();
	}

	[Fact]
	public async Task PopulateETagFromHttpContext()
	{
		var services = new ServiceCollection();
		var eTag = A.Fake<IETag>();
		_ = services.AddSingleton(eTag);

		var serviceProvider = services.BuildServiceProvider();

		var expectedETag = "\"test-etag\"";
		var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
		httpContext.Request.Headers["If-Match"] = expectedETag;

		var middleware = new RequestDelegate(async context =>
		{
			context.RequestServices.GetRequiredService<IETag>().IncomingValue = context.ETag();
			context.Response.OnStarting(() =>
			{
				var etagValue = context.RequestServices.GetRequiredService<IETag>().OutgoingValue;
				if (!string.IsNullOrEmpty(etagValue))
				{
					context.Response.Headers.Append("ETag", etagValue.Split(','));
				}

				return Task.CompletedTask;
			});
			await Task.CompletedTask.ConfigureAwait(true);
		});

		await middleware(httpContext).ConfigureAwait(true);
		_ = A.CallToSet(() => eTag.IncomingValue).To(expectedETag).MustHaveHappened();
	}

	[Fact]
	public async Task PopulateClientAddressFromHttpContext()
	{
		var services = new ServiceCollection();
		var clientAddress = A.Fake<IClientAddress>();
		_ = services.AddSingleton(clientAddress);

		var serviceProvider = services.BuildServiceProvider();

		var expectedIp = "192.168.1.1";
		var httpContext = new DefaultHttpContext
		{
			RequestServices = serviceProvider,
			Connection = { RemoteIpAddress = IPAddress.Parse(expectedIp) }
		};

		var middleware = new RequestDelegate(context =>
		{
			context.RequestServices.GetRequiredService<IClientAddress>().Value = context.RemoteIpAddress();
			return Task.CompletedTask;
		});

		await middleware(httpContext).ConfigureAwait(true);
		_ = A.CallToSet(() => clientAddress.Value).To(expectedIp).MustHaveHappened();
	}
}
