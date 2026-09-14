// Host composition extracted from Program.cs. The sample's top-level statements otherwise exceed the
// class-coupling analyzer limit, and the limit is doing its job: a reader following the workflow should
// not have to step over the host's prerequisite wiring to find it.

using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;

namespace ProvisioningWorkflow;

internal static class SampleHostComposition
{
	/// <summary>
	/// Registers everything <c>AddExcaliburA3</c> requires of the host but does not supply itself.
	/// </summary>
	/// <remarks>
	/// TWO registrations, not one. The scoped wrapper is keyed and wraps whichever unkeyed cache the
	/// container holds, so a host that registers only the wrapper has nothing for it to wrap. Grant cache
	/// keys identify a user but not an application, so without the partition two applications sharing one
	/// cache server address the same entry for the same user and one serves the other's grants. The scope
	/// is what keeps them apart, which is why every sample here uses a different one.
	/// <para>
	/// A real host points the unkeyed registration at Redis or SQL Server; the in-memory one here is what
	/// makes this sample self-contained, and it is not a production choice.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSampleAuthorizationPrerequisites(this IServiceCollection services)
	{
		// This sample keeps the in-memory grant stores, so it must say so out loud. AddExcaliburA3 installs
		// a startup gate that FAILS CLOSED when grants would live in a volatile store: grants lost on
		// restart make a user whose grants vanished indistinguishable from one who never had any, so
		// authorization silently denies everyone. A production host registers a durable grant store instead
		// and leaves this option alone.
		_ = services.Configure<GrantDurabilityOptions>(static o => o.AllowVolatileGrantStore = true);

		_ = services.AddDistributedMemoryCache();
		_ = services.AddApplicationScopedDistributedCache(static o => o.Scope = "provisioning-workflow-sample");
		return services;
	}
}
