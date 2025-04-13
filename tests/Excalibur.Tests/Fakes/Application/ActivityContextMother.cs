using Excalibur.A3;
using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Domain;

using FakeItEasy;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Fakes.Application;

public static class ActivityContextMother
{
	public static IActivityContext WithCorrelationAndTenant(Guid? correlationId = null, string? tenantId = null)
	{
		var context = A.Fake<IActivityContext>();

		var cid = A.Fake<ICorrelationId>();
		_ = A.CallTo(() => cid.Value).Returns(correlationId ?? Guid.NewGuid());
		_ = A.CallTo(() => context.Get(nameof(CorrelationId), A<ICorrelationId>._)).Returns(cid);

		var tenant = A.Fake<ITenantId>();
		_ = A.CallTo(() => tenant.Value).Returns(tenantId ?? "test-tenant");
		_ = A.CallTo(() => context.Get(nameof(TenantId), A<ITenantId>._)).Returns(tenant);

		return context;
	}

	public static IActivityContext WithAccessToken(IAccessToken? accessToken = null)
	{
		var context = A.Fake<IActivityContext>();

		_ = A.CallTo(() => context.Get("AccessToken", (IAccessToken?)null)).Returns(accessToken);
		return context;
	}

	public static IActivityContext WithTenantId(string tenantId = "default-tenant-id")
	{
		var context = A.Fake<IActivityContext>();
		var tenant = A.Fake<ITenantId>();
		_ = A.CallTo(() => tenant.Value).Returns(tenantId);
		_ = A.CallTo(() => context.Get(nameof(TenantId), A<ITenantId>._)).Returns(tenant);
		return context;
	}

	public static IActivityContext WithCorrelationId(Guid correlationId)
	{
		var context = A.Fake<IActivityContext>();
		var cid = A.Fake<ICorrelationId>();
		_ = A.CallTo(() => cid.Value).Returns(correlationId);
		_ = A.CallTo(() => context.Get(nameof(CorrelationId), A<ICorrelationId>._)).Returns(cid);
		return context;
	}

	public static IActivityContext WithConfiguration(IConfiguration? configuration = null)
	{
		var context = A.Fake<IActivityContext>();
		var config = configuration ?? A.Fake<IConfiguration>();
		_ = A.CallTo(() => context.Get(nameof(IConfiguration), A<IConfiguration>._)).Returns(config);
		return context;
	}

	public static IActivityContext WithClientAddress(string address = "127.0.0.1")
	{
		var context = A.Fake<IActivityContext>();
		var client = A.Fake<IClientAddress>();
		_ = A.CallTo(() => client.Value).Returns(address);
		_ = A.CallTo(() => context.Get(nameof(ClientAddress), A<IClientAddress>._)).Returns(client);
		return context;
	}

	public static IActivityContext WithETag(string? incoming = null, string? outgoing = null)
	{
		var context = A.Fake<IActivityContext>();
		var etag = A.Fake<IETag>();
		_ = A.CallTo(() => etag.IncomingValue).Returns(incoming);
		_ = A.CallTo(() => etag.OutgoingValue).Returns(outgoing);
		_ = A.CallTo(() => context.Get(nameof(ETag), A<IETag>._)).Returns(etag);
		return context;
	}

	public static IActivityContext WithServiceProvider(IServiceProvider? provider = null)
	{
		var context = A.Fake<IActivityContext>();
		var serviceProvider = provider ?? A.Fake<IServiceProvider>();
		_ = A.CallTo(() => context.Get(nameof(IServiceProvider), A<IServiceProvider>._)).Returns(serviceProvider);
		return context;
	}
}
