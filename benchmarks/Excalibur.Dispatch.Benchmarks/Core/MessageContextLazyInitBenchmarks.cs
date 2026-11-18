// -----------------------------------------------------------------------------
// MessageContext Lazy Initialization Benchmarks
// Sprint 205 - Core Performance Foundation
// Task cw6tf: Benchmark baseline before/after
//
// Purpose: Measure context creation and item-access costs. Historical Sprint 205
// targets below are not current measurements or proof of an improvement.
// -----------------------------------------------------------------------------

#pragma warning disable CA1707 // Remove underscores from member names (benchmark naming convention)

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// Benchmarks measuring MessageContext creation and item access with lazy dictionary initialization.
/// Sprint 205: Core Performance Foundation.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks retain the original context and item-access workloads. They do
/// not invoke the dispatcher. Key scenarios:
/// </para>
/// <list type="bullet">
/// <item><description>Context creation without Items usage (historical zero-dictionary-allocation target)</description></item>
/// <item><description>Context creation with Items read (historical zero-dictionary-allocation target)</description></item>
/// <item><description>Context creation with Items write (allocates on first write)</description></item>
/// </list>
/// <para>
/// Historical Performance Targets (Sprint 205; not current measurements):
/// - Context creation without Items: ~10-20ns (down from ~100ns)
/// - Memory without Items: ~200-400B (down from ~896B)
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class MessageContextLazyInitBenchmarks
{
	private IServiceProvider _serviceProvider = null!;

	[GlobalSetup]
	public void Setup()
	{
		// Create a minimal service provider for context initialization
		_serviceProvider = new MinimalServiceProvider();
	}

	// =========================================================================
	// SECTION 1: Context Creation - Zero Items Usage (Target: Zero Dict Alloc)
	// =========================================================================

	/// <summary>
	/// Baseline: Create MessageContext and set basic properties only.
	/// The workload also initializes identity/routing features; total allocations are measured.
	/// </summary>
	[Benchmark(Baseline = true, Description = "MessageContext: Create (no Items)")]
	public Messaging.MessageContext CreateContext_NoItemsUsage()
	{
		var context = new Messaging.MessageContext
		{
			MessageId = "msg-001",
			CorrelationId = "corr-001",
			CausationId = "cause-001",
			RequestServices = _serviceProvider,
		};
		context.GetOrCreateIdentityFeature().UserId = "user-001";
		context.GetOrCreateIdentityFeature().TenantId = "tenant-001";
		context.GetOrCreateIdentityFeature().TraceParent = "00-trace-parent";
		context.GetOrCreateRoutingFeature().Source = "BenchmarkSource";
		context.SetMessageType("BenchmarkMessage");
		return context;
	}

	// =========================================================================
	// SECTION 2: Read-Only Items Access (context allocation is included)
	// =========================================================================

	/// <summary>
	/// Check an absent key on a fresh context, including context creation.
	/// </summary>
	[Benchmark(Description = "ContainsItem on fresh context (absent key)")]
	public bool ContainsItem_FreshContext()
	{
		var context = new Messaging.MessageContext();
		return context.ContainsItem("SomeKey"); // Absent-key control; includes context creation.
	}

	/// <summary>
	/// Read an absent item on a fresh context, including context creation.
	/// </summary>
	[Benchmark(Description = "GetItem on fresh context (absent key)")]
	public string? GetItem_FreshContext()
	{
		var context = new Messaging.MessageContext();
		return context.GetItem<string>("SomeKey"); // Absent-key control; includes context creation.
	}

	/// <summary>
	/// Remove an absent item on a fresh context, including context creation.
	/// </summary>
	[Benchmark(Description = "RemoveItem on fresh context (absent key)")]
	public void RemoveItem_FreshContext()
	{
		var context = new Messaging.MessageContext();
		context.RemoveItem("SomeKey"); // Absent-key removal control; includes context creation.
	}

	// =========================================================================
	// SECTION 3: Write Operations (Allocates on First Write)
	// =========================================================================

	/// <summary>
	/// SetItem on fresh context (WILL allocate dictionary on first write).
	/// </summary>
	[Benchmark(Description = "SetItem on fresh context (allocs dict)")]
	public void SetItem_FreshContext()
	{
		var context = new Messaging.MessageContext();
		context.SetItem("Key", "Value"); // First item write; total allocations are measured.
	}

	/// <summary>
	/// Access Items property directly (WILL allocate dictionary).
	/// </summary>
	[Benchmark(Description = "Items.Count on fresh context (allocs dict)")]
	public int AccessItems_FreshContext()
	{
		var context = new Messaging.MessageContext();
		return context.Items.Count; // Materializes Items; total allocations are measured.
	}

	// =========================================================================
	// SECTION 4: Context-Only Dispatch Simulations (no dispatcher invocation)
	// =========================================================================

	/// <summary>
	/// Simulates simple dispatch: create context, access properties, no Items.
	/// This is a context-only simulation; it does not invoke the dispatcher.
	/// </summary>
	[Benchmark(Description = "Context simulation: no Items usage")]
	public string? SimpleDispatch_NoItems()
	{
		var context = new Messaging.MessageContext
		{
			MessageId = "msg-001",
			CorrelationId = "corr-001",
			RequestServices = _serviceProvider,
		};
		context.GetOrCreateIdentityFeature().UserId = "user-001";
		context.GetOrCreateIdentityFeature().TenantId = "tenant-001";

		// Simulate handler accessing context properties
		_ = context.MessageId;
		_ = context.CorrelationId;
		_ = context.GetUserId();
		_ = context.GetTenantId();

		// Check for a cached result; total method allocations include context/features.
		_ = context.ContainsItem("Dispatch:CachedResult");

		return context.CorrelationId;
	}

	/// <summary>
	/// Simulates dispatch with middleware writing to Items.
	/// Allocates dictionary when middleware writes.
	/// </summary>
	[Benchmark(Description = "Context simulation: middleware item writes")]
	public string? Dispatch_WithMiddlewareItems()
	{
		var context = new Messaging.MessageContext
		{
			MessageId = "msg-001",
			CorrelationId = "corr-001",
			RequestServices = _serviceProvider,
		};
		context.GetOrCreateIdentityFeature().UserId = "user-001";
		context.GetOrCreateIdentityFeature().TenantId = "tenant-001";

		// Simulate middleware pipeline writing to Items
		context.SetItem("Dispatch:Message", context.MessageId ?? "none");
		context.SetItem("Validation:Passed", true);
		context.SetItem("Timing:StartUtc", DateTimeOffset.UtcNow);

		return context.CorrelationId;
	}

	// =========================================================================
	// SECTION 5: Child Context Creation
	// =========================================================================

	/// <summary>
	/// Create child context from parent without Items usage.
	/// </summary>
	[Benchmark(Description = "CreateChildContext (no Items)")]
	public IMessageContext CreateChildContext_NoItems()
	{
		var parent = new Messaging.MessageContext
		{
			MessageId = "parent-001",
			CorrelationId = "corr-001",
			RequestServices = _serviceProvider,
		};
		parent.GetOrCreateIdentityFeature().TenantId = "tenant-001";

		return parent.CreateChildContext();
	}

	// =========================================================================
	// SECTION 6: Comparison - Multiple Operations
	// =========================================================================

	/// <summary>
	/// Multiple absent-key operations on a fresh context; includes context creation.
	/// </summary>
	[Benchmark(Description = "Multiple absent-key reads on fresh context")]
	public bool MultipleReads_NoAllocation()
	{
		var context = new Messaging.MessageContext();

		// Absent-key controls; context creation is part of the workload.
		var a = context.ContainsItem("Key1");
		var b = context.ContainsItem("Key2");
		var c = context.ContainsItem("Key3");
		_ = context.GetItem<string>("Key1");
		_ = context.GetItem<int>("Key2");
		context.RemoveItem("Key3");

		return a || b || c;
	}

	/// <summary>
	/// Five writes to a fresh context, including initial storage and any growth.
	/// </summary>
	[Benchmark(Description = "Five item writes on fresh context")]
	public void FirstWriteAllocates_SubsequentCheap()
	{
		var context = new Messaging.MessageContext();

		// First write allocates dictionary
		context.SetItem("Key1", "Value1");

		// Subsequent writes reuse item storage and may grow it.
		context.SetItem("Key2", "Value2");
		context.SetItem("Key3", "Value3");
		context.SetItem("Key4", "Value4");
		context.SetItem("Key5", "Value5");
	}

	// =========================================================================
	// Helper Types
	// =========================================================================

	/// <summary>
	/// Minimal service provider for benchmarks.
	/// </summary>
	private sealed class MinimalServiceProvider : IServiceProvider
	{
		public object? GetService(Type serviceType) => null;
	}
}
