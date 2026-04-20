# Repository Guidelines

## Project Structure & Module Organization
- `src/Dispatch/` contains the core Dispatch packages, including transport adapters in `src/Dispatch/Excalibur.Dispatch.Transport.*`.
- `tests/` holds unit/integration/conformance suites; `benchmarks/` covers perf runs; `samples/` includes P0/P1 sample apps.
- `docs/` is contributor-facing documentation; `docs-site/` hosts end-user docs.
- Shared build configuration lives in `Directory.Build.props` and dependency pins in `Directory.Packages.props`.

## Build, Test, and Development Commands
- `dotnet build Excalibur.sln` for fast local builds (ProjectReference).
- `dotnet build eng/ci/shards/ShippingOnly.slnf` for shipping-only packages.
- `dotnet build -c Release` for a release configuration build.
- `dotnet test Excalibur.sln` to run the full suite.
- `dotnet test --filter "Category=Unit"` or `Category=Integration` for focused runs.
- `pwsh eng/validate-package-composition.ps1` to mirror CI package composition validation.

## Coding Style & Naming Conventions
- Follow ADR-075 (.NET best practices) and the repo `.editorconfig`; use 4-space indentation.
- Namespace conventions are enforced by the namespace validator (avoid blocked patterns such as `.Core.`).
- Treat warnings as errors; keep public APIs and docs in sync for shipping packages.

## Testing Guidelines
- Tests are xUnit with required traits (`Category`, `Component`, `Pattern`) for CI filtering.
- Add/update tests in `tests/` and validate related samples in `samples/`.
- See `docs/testing/` for trait definitions and organization rules.

## Commit & Pull Request Guidelines
- Use conventional commits: `type(scope): summary` (e.g., `fix(kafka): handle retries`).
- PRs should include a clear description, tests run, and linked issues when applicable.
- If you modify generated reports/indices, update and commit the corresponding artifacts.

## Agent Workflow (Repo-Specific)
- Reserve files via OPCOM/Beads before editing and coordinate parallel changes.

## Mission Workflow (MANDATORY for all workers)

> **MANDATORY** — All workers must follow this workflow. The mission state machine orchestrates sprint phases automatically.

Sprints are driven by a **mission** — a state machine that progresses through phases: PLAN → GUIDE → IMPLEMENT → TEST → SAMPLES → REVIEW_CODE → REVIEW_ARCH → DOCS → CLOSE. Each phase creates tasks and dispatches them to assigned workers.

### When You Receive a Task

1. **Accept the task immediately:**
   ```
   opcom_task_accept({ projectKey: "/excalibur/dispatch", agentName: "<your-mailbox>", taskId: <id> })
   ```

2. **Read the task description** — it tells you which skill(s) to run. **You MUST run the listed skills using the Skill tool. This is not optional.** Do not substitute your own approach.
   - If it says "Run these skills in order: /ready-for-testing, then /full-ci-run" → call `Skill({ skill: "ready-for-testing" })` then `Skill({ skill: "full-ci-run" })`
   - If it says "Mission phase IMPLEMENT" → call `Skill({ skill: "run-current-tasks" })`

3. **Run every listed skill to completion** — do not skip skills, do not substitute your own verification, do not mark the task complete until every listed skill has been executed via the Skill tool.

4. **Only after ALL skills have run**, mark the task complete:
   ```
   opcom_task_update({ projectKey: "/excalibur/dispatch", agentName: "<your-mailbox>", taskId: <id>, status: "completed" })
   ```
   **Do NOT mark complete if you skipped any required skill.**

5. **The mission auto-advances** — when all tasks in the current phase are complete, the mission automatically moves to the next phase and dispatches tasks to the next worker(s).

### What Happens Automatically

- **Phase transitions**: Handled by the mission system — you don't need to call `opcom_mission_advance`
- **Next worker notification**: The mission dispatches a task + sends a high-importance message to the next phase's worker
- **Dependency unblocking**: When your task completes and another task depended on it, that worker is automatically notified

### What You Must Do

- Accept tasks when dispatched to you
- Complete your work and call `opcom_task_update(status: "completed")`
- Log progress to the capsule: `./.claude/hooks/log-discovery.sh achievement "..."`
- If blocked, send a direct message to the specific agent who can unblock you

### What You Must NOT Do

- **DO NOT work ahead of your phase.** If the mission is on IMPLEMENT and you are a reviewer, do NOT start reviewing. Wait for your phase to become active and a task to be dispatched to you.
- **DO NOT start tasks from the sprint plan directly.** Only work on tasks dispatched to you via OPCOM (`opcom_task_accept`). Reading the sprint plan for context is fine, but do not self-assign or start work from it.
- **DO NOT build the solution while another agent is editing files.** Use `opcom_acquire_build_slot` before running `dotnet build` and release it after. If the slot is held, wait.
- **DO NOT edit files reserved by another agent.** Use `opcom_check_reservations` before editing. If reserved, wait or coordinate.

## Communication Policy (Mission-Driven)

> **MANDATORY** — This policy overrides any per-agent messaging instructions that predate it.

The mission state machine handles phase transitions and task dispatch automatically. Workers do NOT need to broadcast progress to the team. Follow these rules:

### Progress Tracking: Use Capsule, Not OPCOM Messages

- **DO:** Log progress to the shared capsule: `./.claude/hooks/log-discovery.sh achievement "Task X complete: <summary>"`
- **DO:** Call `opcom_task_update(status: "completed")` when your task is done — the mission system auto-advances the sprint pipeline.
- **DO NOT:** Send OPCOM messages to 5+ recipients announcing routine progress, task starts, or completions.
- **DO NOT:** CC ProjectManager, SoftwareArchitect, BackendDeveloper, TestsDeveloper, etc. on every status update.

### When to Send OPCOM Messages

Only send direct OPCOM messages when:

| Scenario | Who to message | Why |
|----------|---------------|-----|
| **Blocked** and need help | The specific agent who can unblock you | Targeted request, not broadcast |
| **Found a bug/issue** that affects another agent's work | That agent directly | They need to know now |
| **Review findings** that require action | Sprint channel + the agent who owns the code | Actionable feedback |
| **Unblocking a dependent task** | The agent whose task depends on yours | They need to start working |
| **Architecture/requirement questions** | SoftwareArchitect or ProductManager | Decision needed |

### Task Completion Flow

```
1. Finish your work
2. Log to capsule: log-discovery.sh achievement "Task complete: <summary>"
3. Mark task done: opcom_task_update(status: "completed")
4. If your task unblocks another worker → send them a direct message
5. Mission auto-advances when all phase tasks complete
```

### What NOT to Do

- Do not send "Task X started" messages to the team
- Do not send "Task X complete" messages to 5+ recipients (the capsule + task_update handles this)
- Do not acknowledge routine messages with "Idle." — if there's nothing to do, do nothing
- Do not send progress updates to ProjectManager unless you're blocked or need a decision

## File Reservations (Beads + OPCOM)
- Before editing, add a beads comment on the relevant issue with the file list and ETA.
- If another agent is active, send an OPCOM message to avoid conflicts.
- Close the loop with a follow-up comment when the reservation ends.

Examples:
```bash
bd comments add Excalibur.Dispatch-123 "Reserving: src/Dispatch/Foo.cs (ETA 2h)"
bd message send other-agent "Reserving src/Dispatch/Foo.cs for Excalibur.Dispatch-123"
```

## Issue Tracking with bd (beads)
**IMPORTANT**: This project uses **bd (beads)** for ALL issue tracking. Do NOT use markdown TODOs, task lists, or other tracking methods.

### Why bd?
- Dependency-aware: Track blockers and relationships between issues
- Git-friendly: Auto-syncs to JSONL for version control
- Agent-optimized: JSON output, ready work detection, discovered-from links
- Prevents duplicate tracking systems and confusion

### Quick Start

**Check for ready work:**
```bash
bd ready --json
```

**Create new issues:**
```bash
bd create "Issue title" -t bug|feature|task -p 0-4 --json
bd create "Issue title" -p 1 --deps discovered-from:bd-123 --json
```

**Claim and update:**
```bash
bd update bd-42 --status in_progress --json
bd update bd-42 --priority 1 --json
```

**Complete work:**
```bash
bd close bd-42 --reason "Completed" --json
```

### Issue Types

- `bug` - Something broken
- `feature` - New functionality
- `task` - Work item (tests, docs, refactoring)
- `epic` - Large feature with subtasks
- `chore` - Maintenance (dependencies, tooling)

### Priorities

- `0` - Critical (security, data loss, broken builds)
- `1` - High (major features, important bugs)
- `2` - Medium (default, nice-to-have)
- `3` - Low (polish, optimization)
- `4` - Backlog (future ideas)

### Workflow for AI Agents

1. **Check ready work**: `bd ready` shows unblocked issues
2. **Claim your task**: `bd update <id> --status in_progress`
3. **Work on it**: Implement, test, document
4. **Discover new work?** Create linked issue:
   - `bd create "Found bug" -p 1 --deps discovered-from:<parent-id>`
5. **Complete**: `bd close <id> --reason "Done"`
6. **Commit together**: Always commit the `.beads/issues.jsonl` file together with the code changes so issue state stays in sync with code state

### Auto-Sync

bd automatically syncs with git:
- Exports to `.beads/issues.jsonl` after changes (5s debounce)
- Imports from JSONL when newer (e.g., after `git pull`)
- No manual export/import needed!

### MCP Server (Recommended)

If using Claude or MCP-compatible clients, install the beads MCP server:

```bash
pip install beads-mcp
```

Add to MCP config (e.g., `~/.config/claude/config.json`):
```json
{
  "beads": {
    "command": "beads-mcp",
    "args": []
  }
}
```

Then use `mcp__beads__*` functions instead of CLI commands.

### Managing AI-Generated Planning Documents

AI assistants often create planning and design documents during development:
- PLAN.md, IMPLEMENTATION.md, ARCHITECTURE.md
- DESIGN.md, CODEBASE_SUMMARY.md, INTEGRATION_PLAN.md
- TESTING_GUIDE.md, TECHNICAL_DESIGN.md, and similar files

**Best Practice: Use a dedicated directory for these ephemeral files**

**Recommended approach:**
- Create a `history/` directory in the project root
- Store ALL AI-generated planning/design docs in `history/`
- Keep the repository root clean and focused on permanent project files
- Only access `history/` when explicitly asked to review past planning

**Example .gitignore entry (optional):**
```
# AI planning documents (ephemeral)
history/
```

**Benefits:**
- ✅ Clean repository root
- ✅ Clear separation between ephemeral and permanent documentation
- ✅ Easy to exclude from version control if desired
- ✅ Preserves planning history for archeological research
- ✅ Reduces noise when browsing the project

### Important Rules

- ✅ Use bd for ALL task tracking
- ✅ Always use `--json` flag for programmatic use
- ✅ Link discovered work with `discovered-from` dependencies
- ✅ Check `bd ready` before asking "what should I work on?"
- ✅ Store AI planning docs in `history/` directory
- ❌ Do NOT create markdown TODO lists
- ❌ Do NOT use external issue trackers
- ❌ Do NOT duplicate tracking systems
- ❌ Do NOT clutter repo root with planning documents

For more details, see README.md and QUICKSTART.md.
