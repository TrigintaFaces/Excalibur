using System.Data;

using Excalibur.Application;
using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.DataAccess;
using Excalibur.Domain;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Stubs.Application;

public class ActivityContextBuilder
{
	private ITenantId _tenantId = new DefaultTenantId("tenant-123");
	private ICorrelationId _correlationId = new DefaultCorrelationId(Guid.NewGuid());
	private IETag _etag = new DefaultETag("in-etag");
	private IDomainDb? _domainDb;
	private IConfiguration _configuration = new ConfigurationBuilder().Build();
	private IClientAddress _clientAddress = new DefaultClientAddress("127.0.0.1");
	private IServiceProvider _serviceProvider = new DummyServiceProvider();

	public ActivityContextBuilder WithConnection(IDbConnection connection)
	{
		_domainDb = new DomainDb(connection);
		return this;
	}

	public ActivityContextBuilder WithTenantId(string tenantId)
	{
		_tenantId = new DefaultTenantId(tenantId);
		return this;
	}

	public ActivityContextBuilder WithCorrelationId(Guid id)
	{
		_correlationId = new DefaultCorrelationId(id);
		return this;
	}

	public ActivityContextBuilder WithETag(string incoming, string? outgoing = null)
	{
		_etag = new DefaultETag(incoming) { OutgoingValue = outgoing };
		return this;
	}

	public ActivityContextBuilder WithConfiguration(IConfiguration configuration)
	{
		_configuration = configuration;
		return this;
	}

	public ActivityContextBuilder WithClientAddress(string address)
	{
		_clientAddress = new DefaultClientAddress(address);
		return this;
	}

	public ActivityContextBuilder WithServiceProvider(IServiceProvider provider)
	{
		_serviceProvider = provider;
		return this;
	}

	public IActivityContext Build()
	{
		return new ActivityContext(_tenantId, _correlationId, _etag, _domainDb, _configuration, _clientAddress, _serviceProvider);
	}
}

public class DefaultTenantId : ITenantId
{
	public DefaultTenantId(string value) => Value = value;

	public string Value { get; set; }
}

public class DefaultCorrelationId : ICorrelationId
{
	public DefaultCorrelationId(Guid value) => Value = value;

	public Guid Value { get; set; }
}

public class DefaultETag : IETag
{
	public DefaultETag(string incoming) => IncomingValue = incoming;

	public string? IncomingValue { get; set; }
	public string? OutgoingValue { get; set; }
}

public class DefaultClientAddress : IClientAddress
{
	public DefaultClientAddress(string value) => Value = value;

	public string Value { get; set; }
}

public class DummyDomainDb(IDbConnection connection) : Db(connection), IDomainDb;

public class DummyServiceProvider : IServiceProvider
{
	public object? GetService(Type serviceType) => null;
}
