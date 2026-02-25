# Consolidated CloudNative Examples

This directory contains consolidated examples that previously existed in multiple locations throughout the codebase. The consolidation eliminates duplication while preserving all important patterns and functionality.

## Consolidated From

This consolidation replaced the following duplicate example directories:

### Removed Directories
- `examples/CircuitBreakerCache/` - Basic circuit breaker cache examples
- `examples/CacheCircuitBreaker/` - Alternative circuit breaker cache implementations
- `examples/Caching/CircuitBreaker/` - Advanced circuit breaker caching patterns
- `examples/Excalibur.Dispatch.CloudNative.Patterns/` - CloudNative-specific patterns
- `examples/Excalibur.Dispatch.Transport.Patterns/` - CloudProviders-specific patterns

### Consolidated Examples

#### 1. UnifiedCacheCircuitBreakerExample.cs
**Consolidates patterns from:**
- `CircuitBreakerCache/CacheCircuitBreakerExample.cs`
- `CircuitBreakerCache/AdvancedCircuitBreakerExample.cs`
- `Caching/CircuitBreaker/AdvancedCircuitBreakerExample.cs`
- `Excalibur.Dispatch.Transport.Patterns/CacheCircuitBreaker/AdvancedCacheCircuitBreakerExample.cs`

**Features:**
- Basic circuit breaker operations
- Multi-region circuit breaker isolation
- Cascading failure prevention
- Adaptive TTL based on circuit state
- Performance benchmarking
- Comprehensive metrics and monitoring

#### 2. AdvancedAdaptiveTtlExample.cs
**Consolidates patterns from:**
- `Excalibur.Dispatch.CloudNative.Patterns/Caching/AdaptiveTtl/AdvancedAdaptiveTtlExample.cs`
- Various TTL adaptation strategies from other locations

**Features:**
- Hit rate-based TTL adaptation
- Frequency-based TTL strategies
- Memory pressure-based adaptation
- Time pattern-based strategies
- Composite strategy combinations
- Real-world e-commerce scenarios
- Performance benchmarking vs fixed TTL

## Benefits of Consolidation

### 1. **Eliminated Duplication**
- Removed ~5 duplicate circuit breaker implementations
- Consolidated similar caching patterns
- Unified CloudNative vs CloudProviders approaches

### 2. **Improved Maintainability**
- Single source of truth for each pattern
- Easier to update and enhance
- Consistent API and behavior

### 3. **Better Documentation**
- Comprehensive examples in one location
- Clear demonstration of all features
- Reduced cognitive overhead for developers

### 4. **Enhanced Functionality**
- Combined best features from all implementations
- More comprehensive test scenarios
- Better error handling and metrics

## Architecture Changes

### Shared Infrastructure
The consolidated examples now use shared infrastructure from:
- `Excalibur.Dispatch.Common.ChannelMessagePump` - Unified message pump implementation
- Common circuit breaker interfaces and base classes
- Shared caching abstractions

### Namespace Standardization
- All consolidated examples use `Excalibur.Dispatch.Examples.Consolidated` namespace
- Removed confusion between `Excalibur.Dispatch.CloudNative.*` vs `Excalibur.Dispatch.Transport.*`
- Consistent naming conventions throughout

## Migration Guide

### For Developers Using Old Examples

If you were referencing any of the removed example directories:

1. **Update References:**
   ```csharp
   // OLD: Multiple scattered examples
   using Excalibur.Dispatch.CloudNative.Patterns.CacheCircuitBreaker;
   using Excalibur.Dispatch.Transport.Patterns.CacheCircuitBreaker;

   // NEW: Single consolidated namespace
   using Excalibur.Dispatch.Examples.Consolidated;
   ```

2. **Use Unified APIs:**
   - `UnifiedCacheCircuitBreakerExample` contains all circuit breaker patterns
   - `AdvancedAdaptiveTtlExample` contains all TTL adaptation strategies
   - Both examples include comprehensive documentation and benchmarks

3. **Leverage Enhanced Features:**
   - Better metrics and monitoring
   - More comprehensive test scenarios
   - Improved error handling
   - Performance optimizations

### Key API Changes

Most APIs remain the same, but some consolidation was made:

```csharp
// Circuit Breaker - Now unified interface
ICircuitBreaker circuitBreaker = circuitBreakerFactory.GetOrCreate("MyService");

// Adaptive TTL - Consolidated strategy interface
IAdaptiveTtlStrategy strategy = new CompositeStrategy()
    .AddStrategy(hitRateStrategy, 0.6)
    .AddStrategy(frequencyStrategy, 0.4);
```

## Running the Examples

```bash
# Run the unified circuit breaker example
dotnet run --project examples/Consolidated/UnifiedCacheCircuitBreakerExample.cs

# Run the adaptive TTL example
dotnet run --project examples/Consolidated/AdvancedAdaptiveTtlExample.cs
```

## Next Steps

With this consolidation complete:
1. All circuit breaker and caching patterns are in one location
2. Reduced maintenance burden across the codebase
3. Developers have clear, comprehensive examples to reference
4. Foundation is set for future pattern additions

The consolidation maintains backward compatibility while providing a much cleaner and more maintainable structure for CloudNative patterns in the Excalibur Dispatch project.