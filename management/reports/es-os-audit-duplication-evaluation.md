# ES/OS Audit Sink + Exporter Duplication Evaluation

**Date:** 2026-04-02
**Sprint:** 738 (L.4)
**Evaluator:** DocumentationWriter
**Beads:** `sol792`

## Summary

The Elasticsearch and OpenSearch audit logging packages are **96-99% identical** after normalizing type names. The only differences are comment wording (3 lines) and one missing file in OpenSearch.

## Quantitative Analysis

### File-by-File Comparison (name + whitespace normalized)

| ES File | OS File | Lines | Diff Lines | Identity |
|---------|---------|-------|-----------|----------|
| `ElasticsearchAuditSink.cs` | `OpenSearchAuditSink.cs` | 207 | 3 | **98%** |
| `ElasticsearchAuditExporter.cs` | `OpenSearchAuditExporter.cs` | 481 | 1 | **99%** |
| `ElasticsearchAuditSinkOptions.cs` | `OpenSearchAuditSinkOptions.cs` | 124 | 3 | **97%** |
| `ElasticsearchExporterOptions.cs` | `OpenSearchExporterOptions.cs` | 91 | 3 | **96%** |
| `ElasticsearchServiceCollectionExtensions.cs` | `OpenSearchServiceCollectionExtensions.cs` | 144 | ~4 | **97%** |
| `ElasticsearchAuditJsonContext.cs` | `OpenSearchAuditJsonContext.cs` | 18 | 0 | **100%** |
| `ElasticsearchAuditSinkOptionsValidator.cs` | `OpenSearchAuditSinkOptionsValidator.cs` | 74 | 0 | **100%** |
| `ElasticsearchExporterOptionsValidator.cs` | *MISSING* | 74 | - | N/A |

**Weighted average: ~98% identical across 1,139 lines.**

### What Actually Differs

After replacing `Elasticsearch` with `OpenSearch` in the ES code, the remaining differences are:

1. **Comment wording** (3 lines in AuditSink): `"serves as a search and analytics sink -- for compliance-grade..."` vs `"serves as a search/analytics sink, not a compliance-grade..."` -- purely stylistic
2. **Missing file**: `OpenSearchExporterOptionsValidator.cs` does not exist -- likely a copy-paste omission
3. **Indentation style**: ES uses tabs, OS uses spaces -- no semantic difference

### What Is Identical

- HTTP Bulk API interaction pattern
- Round-robin node selection logic
- Index naming and lifecycle management
- Error handling and retry patterns
- Options validation
- DI registration
- JSON serialization context
- Logger message definitions

## Root Cause

Both packages communicate with the **same REST API** -- OpenSearch is a fork of Elasticsearch and maintains API compatibility for the Bulk API, index management, and search. The audit sink/exporter only uses these basic REST operations, so there is zero protocol-level difference.

## Recommendation: Shared Base Class

### Approach: `SearchEngineAuditSinkBase<TOptions>` in a shared package

```
Excalibur.Dispatch.AuditLogging.SearchEngine (new, internal)
  ├── SearchEngineAuditSinkBase<TOptions> : IAuditSink
  ├── SearchEngineAuditExporterBase<TOptions> : IAuditExporter
  ├── SearchEngineAuditSinkOptionsBase
  └── SearchEngineExporterOptionsBase

Excalibur.Dispatch.AuditLogging.Elasticsearch (existing, thin wrapper)
  ├── ElasticsearchAuditSink : SearchEngineAuditSinkBase<ElasticsearchAuditSinkOptions>
  └── ElasticsearchServiceCollectionExtensions

Excalibur.Dispatch.AuditLogging.OpenSearch (existing, thin wrapper)
  ├── OpenSearchAuditSink : SearchEngineAuditSinkBase<OpenSearchAuditSinkOptions>
  └── OpenSearchServiceCollectionExtensions
```

### Benefits

- **~1,000 lines eliminated** from maintenance burden
- **Bug fixes apply once** -- currently a fix in ES must be manually copied to OS
- **Missing validator bug fixed** -- OS would inherit the base validator

### Risks

- New internal package adds a transitive dependency
- Consumers who depend on internal types (via `InternalsVisibleTo`) may need updates
- Minor version churn for a non-functional change

### Alternative: Do Nothing

The duplication is stable and unlikely to diverge. Both packages are thin HTTP wrappers with no planned feature additions. The cost of consolidation (new package, test migration, CI updates) may exceed the maintenance benefit for a greenfield project.

### Recommendation

**Short term: Do nothing.** The duplication is harmless for a pre-release framework. Fix the missing `OpenSearchExporterOptionsValidator.cs` by copying from ES.

**Long term (post-v1): Consolidate** if either package gains features or if a third search engine (e.g., Amazon OpenSearch Serverless) is added. At that point, the base class pattern is clearly justified.

## Action Items

1. **Fix now:** Create `OpenSearchExporterOptionsValidator.cs` (copy from ES, rename) -- tracked as P2 bug
2. **Defer:** Base class consolidation to post-v1 or when a third search engine backend is needed
