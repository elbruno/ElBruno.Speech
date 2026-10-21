# Copilot Coding Agent — Squad Instructions

You are working on a project that uses **Squad**, an AI team framework. When picking up issues autonomously, follow these guidelines.

## Project: ElBruno.Speech

A reusable, local-first .NET 8/10 speech runtime. It orchestrates a **VAD → STT → LLM → TTS** pipeline using `Microsoft.Extensions.AI` provider boundaries. Full spec: `docs/PRD.md`. Implementation plan and current status: `docs/PLAN.md`.

## NuGet release review gate

Every NuGet release requires a review before publishing. The reviewer must confirm the version, release notes, package metadata, and the complete package set (including `ElBruno.Speech.BlazorComponents`), and verify that the release workflow's build, test, pack, and package-presence checks pass. Use `.github/workflows/publish.yml` for publishing; do not bypass the review gate or publish from an unreviewed change.

**When the user says "continue with the plan":**
1. Read `docs/PLAN.md` to find the first phase marked as `⬅️ NEXT`
2. Read `.squad/identity/now.md` for the current focus and next task
3. Read `.squad/team.md` to identify the right agent to lead that phase
4. Proceed to implement it following the PRD and plan

**Current state:** Phase 0 complete. **Phase 1 (Audio Primitives) is next.** See `docs/PLAN.md`.

## Copilot and Squad session scope

Normal Copilot sessions do not load the custom Squad coordinator and do not need it to work on this repository. Do not run a coordinator canary check, emit a missing-coordinator warning, or adopt the coordinator role in a normal Copilot session.

Squad is opt-in: select the **Squad** custom agent in your client, or start the CLI with `copilot --agent squad`. Only that agent loads `.github/agents/squad.agent.md` and runs its coordinator integrity check. If the coordinator cannot be loaded completely, stop Squad orchestration; do not manufacture a canary or bypass its safety rules.

The Squad-specific instructions below apply only when working on an explicitly Squad-managed task (for example, an issue labeled `squad:*`). Otherwise, use the normal Copilot workflow. Project guidance and the NuGet release review gate above apply in both modes.

## Team Context

Before starting work on any issue:

1. Read `.squad/team.md` for the team roster, member roles, and your capability profile.
2. Read `.squad/routing.md` for work routing rules.
3. If the issue has a `squad:{member}` label, read that member's charter at `.squad/agents/{member}/charter.md` to understand their domain expertise and coding style — work in their voice.

## Capability Self-Check

Before starting work, check your capability profile in `.squad/team.md` under the **Coding Agent → Capabilities** section.

- **🟢 Good fit** — proceed autonomously.
- **🟡 Needs review** — proceed, but note in the PR description that a squad member should review.
- **🔴 Not suitable** — do NOT start work. Instead, comment on the issue:
  ```
  🤖 This issue doesn't match my capability profile (reason: {why}). Suggesting reassignment to a squad member.
  ```

## Branch Naming

Use the squad branch convention:
```
squad/{issue-number}-{kebab-case-slug}
```
Example: `squad/42-fix-login-validation`

## PR Guidelines

When opening a PR:
- Reference the issue: `Closes #{issue-number}`
- If the issue had a `squad:{member}` label, mention the member: `Working as {member} ({role})`
- If this is a 🟡 needs-review task, add to the PR description: `⚠️ This task was flagged as "needs review" — please have a squad member review before merging.`
- Follow any project conventions in `.squad/decisions.md`

## Decisions

If you make a decision that affects other team members, write it to:
```
.squad/decisions/inbox/copilot-{brief-slug}.md
```
The Scribe will merge it into the shared decisions file.
