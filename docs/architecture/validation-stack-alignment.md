# UseValidation() vs UseValidationStack()

## Difference

| Method | Behavior |
|--------|----------|
| `UseValidation()` | Registers `ValidationMiddleware` only, with the default `NoOpValidatorResolver` (no-op resolver — call `WithDataAnnotationsValidation()` or the FluentValidation package's `WithFluentValidation()`/`WithAotFluentValidation()` to actually wire a resolver). |
| `UseValidationStack()` | Registers `ValidationMiddleware` (`TryAddSingleton<IValidatorResolver, NoOpValidatorResolver>()` fallback) + `ExceptionMappingMiddleware`. It does **not** register `ContextValidationMiddleware` or `InputSanitizationMiddleware` — those are separate, opt-in via `UseContextValidation()` and `UseInputSanitization()`. |

## Validation failures bypass ExceptionMappingMiddleware

`ValidationMiddleware` runs at stage `Validation` (200); `ExceptionMappingMiddleware` runs at stage
`PostProcessing` (700). The pipeline sorts middleware ascending by stage and builds the call chain so a
lower-stage middleware wraps everything after it — so `ValidationMiddleware` executes, and can throw,
*before* `ExceptionMappingMiddleware.InvokeAsync` is ever entered. Its `try`/`catch` never sees a
validation failure.

Consequence: neither `UseValidation()` nor `UseValidationStack()` turns a validation failure into an
`IMessageResult.Failed`. `Excalibur.Dispatch.Exceptions.ValidationException` always propagates out of
`DispatchAsync` to the caller. `ExceptionMappingMiddleware` still maps exceptions thrown by handlers and by
middleware staged at or after `Processing` (600) — validation simply sits earlier in the pipeline than the
stage it maps.

## When to Use

- **Simple validation**: `UseValidation()` plus an explicit resolver registration
  (`WithDataAnnotationsValidation()` or a FluentValidation call) — sufficient for most scenarios.
- **Full validation stack**: `UseValidationStack()` — adds problem-details mapping for exceptions thrown by
  handlers and downstream middleware. It composes with, but does not require, an explicit resolver.
- **Context validation / input sanitization**: call `UseContextValidation()` and/or `UseInputSanitization()`
  explicitly; neither is part of `UseValidationStack()`.
- **Custom combination**: register individual middleware via `Use<T>()` for fine-grained control.

## Recommendation

Use `UseValidation()` plus an explicit resolver as the default. Add `UseValidationStack()` when you also
want problem-details mapping for handler/downstream-middleware exceptions. Either way, catch
`Excalibur.Dispatch.Exceptions.ValidationException` around the dispatch call (or handle it at the host
boundary — `Excalibur.Hosting.Web`'s `GlobalExceptionHandler` does this for ASP.NET Core hosts) to observe
a validation failure; it is not visible via `IMessageResult.IsSuccess`.
