# Validation Middleware Decision Tree

## 3 Validation Middleware Types

| Middleware | Use When | Pattern |
|-----------|----------|---------|
| `ValidationMiddleware` | Standard validation via DataAnnotations/FluentValidation | Pipeline stage 200 |
| `InputSanitizationMiddleware` | XSS/injection prevention on untrusted input | Pipeline stage ~190 |
| `ContextValidationMiddleware` | Validating dispatch context properties (MessageId, CorrelationId) | Pipeline stage 200 |

## Decision Tree

1. **Is input from external/untrusted source?** -> Use `InputSanitizationMiddleware`
2. **Are you validating context properties?** -> Use `ContextValidationMiddleware`
3. **Standard message validation?** -> Use `ValidationMiddleware`
