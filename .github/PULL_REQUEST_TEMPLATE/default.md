# Pull Request Checklist

Thank you for your contribution! Please complete all sections.

## Summary
- What change does this PR introduce?
- Why is this change needed?

## Requirement IDs (from `management/Specs/Dispatch.Requirements.*.md` / `management/Specs/requirements-index.md`)
- List all impacted MUST/SHALL IDs (e.g., R0.2, R1.5, R14.1)
- For each ID, link to evidence (code/tests/docs):
  - R__: path/to/file:line — short note
  - R__: path/to/test:line — short note

## Traceability
- RTM link: `management/reports/requirements-traceability.md`
- Related task IDs: (e.g., TASK-20251011-0007)

## Testing
- [ ] Unit tests updated/added
- [ ] Integration/Functional tests considered
- [ ] Performance impact assessed (if hot path)

## CI & Gates
- [ ] CI passes (build, tests, requirements, quality)
- [ ] API compat report reviewed
- [ ] Transitive bloat report reviewed

## Security & Observability
- [ ] No secrets committed; input validated
- [ ] Serilog structured logging present where applicable
- [ ] OpenTelemetry integration considered (opt-in exporters)

## Notes
- Additional context, migration notes, or deprecations

