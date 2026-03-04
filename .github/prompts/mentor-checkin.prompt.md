---
mode: 'agent'
description: 'Quick mentor check-in: assess current work state and suggest next steps based on git status, open PRs, and pending tasks.'
---

Analyze my current work state and tell me what to do next.

## What to Check

1. Run `git status` and `git log --oneline -5` to see where I am
2. Run `gh pr list --author @me --state open` to check my open PRs
3. For each open PR, check CI status with `gh pr checks <number>`
4. Check if there are any `api-needs-work` or `api-ready-for-review` issues assigned to me

## Then Recommend

Based on what you find, suggest the single most important next action:
- If CI is red → triage using the `ci-analysis` skill
- If PR has review feedback → address the feedback
- If PR is approved + CI green → merge it
- If no open PRs → ask what I'm working on and route to the right protocol
- If I have uncommitted changes → help me decide if they're ready to commit

Use the `bcl-mentor` skill for protocol routing and the resource index for links.

Keep the response concise — summary + one recommended action + one link.
