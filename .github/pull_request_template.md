# Pull Request

## Description
<!-- What does this PR do? Why is this change needed? -->


## Type of Change
<!-- Check all that apply -->
- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to change)
- [ ] Documentation update
- [ ] Performance improvement
- [ ] Refactoring (no functional changes)
- [ ] Build/CI changes

## Related Issues
<!-- Link to related issues using GitHub keywords: Fixes #123, Closes #456 -->


## Affected Packages
<!-- Check all affected packages -->
- [ ] Dispatch
- [ ] Excalibur.Dispatch.Abstractions
- [ ] Excalibur.Domain
- [ ] Excalibur.Data.Abstractions
- [ ] Excalibur.EventSourcing
- [ ] Excalibur.EventSourcing.SqlServer
- [ ] Excalibur.Saga
- [ ] Excalibur.LeaderElection
- [ ] Other:

## Testing
<!-- Describe how you tested this change -->

- [ ] Unit tests added/updated
- [ ] Integration tests added/updated (if applicable)
- [ ] Manual testing performed
- [ ] No tests required (documentation only, etc.)

## Checklist
<!-- All items must be checked before merging -->

### Code Quality
- [ ] Code follows the project's coding standards
- [ ] Self-review of code completed
- [ ] Comments added for complex logic
- [ ] No unnecessary changes included

### Build & CI
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] No new warnings introduced

### Documentation
- [ ] XML documentation updated (if public API changed)
- [ ] README updated (if needed)
- [ ] Migration notes added (if breaking change)

### Architecture & Governance
- [ ] Dispatch/Excalibur ownership matrix updated (if capability ownership changed)
- [ ] docs-site parity confirmed for any contributor-doc changes
- [ ] sample catalog update completed (if sample set changed)
- [ ] `pwsh eng/ci/validate-framework-governance.ps1 -Mode Governance -Enforce:$true` passes locally or in CI

### Security
- [ ] No secrets or credentials committed
- [ ] Input validation added where appropriate
- [ ] No security vulnerabilities introduced

## Additional Notes
<!-- Any additional context, screenshots, or information -->
