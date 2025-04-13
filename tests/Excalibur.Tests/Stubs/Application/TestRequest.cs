using Excalibur.Application.Requests;

using MediatR;

namespace Excalibur.Tests.Stubs.Application;

// Test request classes

public sealed class TestRequest(string value) : IRequest<string>
{
	public string Value { get; } = value;
}

public sealed class CorrelatableRequest : IRequest<string>, IAmCorrelatable
{
	public Guid CorrelationId { get; set; }
}

public sealed class MultiTenantRequest : IRequest<string>, IAmMultiTenant
{
	public string TenantId { get; set; } = string.Empty;
}

public sealed class CorrelatableAndMultiTenantRequest : IRequest<string>, IAmCorrelatable, IAmMultiTenant
{
	public Guid CorrelationId { get; set; }
	public string TenantId { get; set; } = string.Empty;
}
