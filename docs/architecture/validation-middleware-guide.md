# Validation Middleware Decision Tree

## 4 Validation Middleware Types

| Middleware | Use When | Pattern |
|-----------|----------|---------|
| `ValidationMiddleware` | Standard validation via DataAnnotations/FluentValidation | Pipeline stage 200 |
| `InputSanitizationMiddleware` | XSS/injection prevention on untrusted input | Pipeline stage ~190 |
| `ZeroAllocationValidationMiddleware` | Hot-path validation without heap allocation | Pipeline stage 200, uses Span-based checks |
| `ContextValidationMiddleware` | Validating dispatch context properties (MessageId, CorrelationId) | Pipeline stage 200 |

## Decision Tree

1. **Is this hot-path (>10K msg/sec)?** -> Use `ZeroAllocationValidationMiddleware`
2. **Is input from external/untrusted source?** -> Use `InputSanitizationMiddleware`
3. **Are you validating context properties?** -> Use `ContextValidationMiddleware`
4. **Standard message validation?** -> Use `ValidationMiddleware`
