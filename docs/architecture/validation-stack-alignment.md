# UseValidation() vs UseValidationStack()

## Difference

| Method | Behavior |
|--------|----------|
| `UseValidation()` | Registers `ValidationMiddleware` only (DataAnnotations + FluentValidation) |
| `UseValidationStack()` | Registers `ValidationMiddleware` + `ContextValidationMiddleware` + `InputSanitizationMiddleware` |

## When to Use

- **Simple validation**: `UseValidation()` -- sufficient for most scenarios
- **Full validation stack**: `UseValidationStack()` -- when you need context validation, input sanitization, AND message validation
- **Custom combination**: Register individual middleware via `Use<T>()` for fine-grained control

## Recommendation

Use `UseValidation()` as default. Add `UseValidationStack()` only when processing untrusted external input that needs sanitization.
