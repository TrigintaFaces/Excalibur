using System.Collections.ObjectModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Tests.Unit.Jobs.Quartz.Cdc;

/// <summary>
/// A test implementation of IHealthChecksBuilder for unit tests
/// </summary>
public class TestHealthChecksBuilder : IHealthChecksBuilder
{
	public Collection<HealthCheckRegistration> Registrations { get; } = new();

	/// <summary>
	/// Mock implementation of Add method that registers a health check
	/// </summary>
	public IHealthChecksBuilder Add(HealthCheckRegistration registration)
	{
		Registrations.Add(registration);
		return this;
	}

	/// <summary>
	/// Returns the underlying service collection
	/// </summary>
	public IServiceCollection Services { get; } = new ServiceCollection();
}
