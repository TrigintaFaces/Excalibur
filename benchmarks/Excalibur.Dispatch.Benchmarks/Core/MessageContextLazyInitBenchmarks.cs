// -----------------------------------------------------------------------------
// MessageContext Lazy Initialization Benchmarks
// Sprint 205 - Core Performance Foundation
// Task cw6tf: Benchmark baseline before/after
//
// Purpose: Measure the performance improvement from lazy-initializing the
// ConcurrentDictionary<string, object> Items in MessageContext.
// -----------------------------------------------------------------------------

#pragma warning disable CA1707 // Remove underscores from member names (benchmark naming convention)

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// Benchmarks comparing MessageContext performance with lazy-init ConcurrentDictionary.
/// Sprint 205: Core Performance Foundation.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks measure the impact of Option B: Lazy-init ConcurrentDictionary
/// in MessageContext. Key scenarios:
/// </para>
/// <list type="bullet">
/// <item><description>Context creation without Items usage (zero allocation target)</description></item>
/// <item><description>Context creation with Items read (zero allocation target)</description></item>
/// <item><description>Context creation with Items write (allocates on first write)</description></item>
/// </list>
/// <para>
/// Performance Targets (Sprint 205):
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
	/// With lazy-init, ConcurrentDictionary should NOT be allocated.
	/// </summary>
	[Benchmark(Baseline = true, Description = "MessageContext: Create (no Items)")]
	public Messaging.MessageContext CreateContext_NoItemsUsage()
	{
		var context = new Messaging.MessageContext
		{
			MessageId = "msg-001",
			CorrelationId = "corr-001",
			CausationId = "cause-001",
			UserId = "user-001",
			TenantId = "tenant-001",
			TraceParent = "00-trace-parent",
			Source = "BenchmarkSource",
			MessageType = "BenchmarkMessage",
			RequestServices = _serviceProvider,
		};
		return context;
	}

	// =========================================================================
	// SECTION 2: Read-Only Items Access (Target: Zero Allocation)
	// =========================================================================

	/// <summary>
	/// Check ContainsItem on fresh context (should NOT allocate dictionary).
	/// </summary>
	[Benchmark(Description = "ContainsItem on fresh context (no alloc)")]
	public bool ContainsItem_FreshContext()
	{
		var context = new Messaging.MessageContext();
		return context.ContainsItem("SomeKey"); // Should return false, no allocation
	}

	/// <summary>
	/// Get item from fresh context (should NOT allocate dictionary).
	/// </summary>
	[Benchmark(Description = "GetItem on fresh context (no alloc)")]
	public string? GetItem_FreshContext()
	{
		var context = new Messaging.MessageContext();
		return context.GetItem<string>("SomeKey"); // Should return null, no allocation
	}

	/// <summary>
	/// Remove item from fresh context (should NOT allocate dictionary).
	/// </summary>
	[Benchmark(Description = "RemoveItem on fresh context (no alloc)")]
	public void RemoveItem_FreshContext()
	{
		var context = new Messaging.MessageContext();
		context.RemoveItem("SomeKey"); // Should no-op, no allocation
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
		context.SetItem("Key", "Value"); // Allocates ConcurrentDictionary on first write
	}

	/// <summary>
	/// Access Items property directly (WILL allocate dictionary).
	/// </summary>
	[Benchmark(Description = "Items.Count on fresh context (allocs dict)")]
	public int AccessItems_FreshContext()
	{
		var context = new Messaging.MessageContext();
		return context.Items.Count; // Allocates ConcurrentDictionary
	}

	// =========================================================================
	// SECTION 4: Realistic Dispatch Scenarios
	// =========================================================================

	/// <summary>
	/// Simulates simple dispatch: create context, access properties, no Items.
	/// This is the optimized hot path with lazy-init.
	/// </summary>
	[Benchmark(Description = "Simple dispatch: no Items usage")]
	public string? SimpleDispatch_NoItems()
	{
		var context = new Messaging.MessageContext
		{
			MessageId = "msg-001",
			CorrelationId = "corr-001",
			UserId = "user-001",
			TenantId = "tenant-001",
			RequestServices = _serviceProvider,
		};

		// Simulate handler accessing context properties
		_ = context.MessageId;
		_ = context.CorrelationId;
		_ = context.UserId;
		_ = context.TenantId;

		// Check for cached result (read-only, no allocation)
		_ = context.ContainsItem("Dispatch:CachedResult");

		return context.CorrelationId;
	}

	/// <summary>
	/// Simulates dispatch with middleware writing to Items.
	/// Allocates dictionary when middleware writes.
	/// </summary>
	[Benchmark(Description = "Dispatch: middleware writes Items")]
	public string? Dispatch_WithMiddlewareItems()
	{
		var context = new Messaging.MessageContext
		{
			MessageId = "msg-001",
			CorrelationId = "corr-001",
			UserId = "user-001",
			TenantId = "tenant-001",
			RequestServices = _serviceProvider,
		};

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
	public Abstractions.IMessageContext CreateChildContext_NoItems()
	{
		var parent = new Messaging.MessageContext
		{
			MessageId = "parent-001",
			CorrelationId = "corr-001",
			TenantId = "tenant-001",
			RequestServices = _serviceProvider,
		};

		return parent.CreateChildContext();
	}

	// =========================================================================
	// SECTION 6: Comparison - Multiple Operations
	// =========================================================================

	/// <summary>
	/// Multiple read operations without triggering allocation.
	/// </summary>
	[Benchmark(Description = "Multiple reads (no alloc)")]
	public bool MultipleReads_NoAllocation()
	{
		var context = new Messaging.MessageContext();

		// All these should NOT allocate
		var a = context.ContainsItem("Key1");
		var b = context.ContainsItem("Key2");
		var c = context.ContainsItem("Key3");
		_ = context.GetItem<string>("Key1");
		_ = context.GetItem<int>("Key2");
		context.RemoveItem("Key3");

		return a || b || c;
	}

	/// <summary>
	/// Single write triggers allocation, subsequent writes are cheap.
	/// </summary>
	[Benchmark(Description = "First write allocs, subsequent cheap")]
	public void FirstWriteAllocates_SubsequentCheap()
	{
		var context = new Messaging.MessageContext();

		// First write allocates dictionary
		context.SetItem("Key1", "Value1");

		// Subsequent writes use existing dictionary (no new allocation)
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
