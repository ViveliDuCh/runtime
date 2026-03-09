---
name: team-updates-digest
description: Generates a structured weekly digest from ".NET Libraries Team Meeting" and "Team Apollo" Teams channels/meetings. Surfaces team updates, cross-team collaboration opportunities, and growth areas for SWE 59-60. Use when asked for "team updates", "Team Apollo digest", ".NET Libraries meeting summary", "what's happening in the team", or for weekly team awareness check-ins.
---

# .NET Team Updates Digest

Queries WorkIQ for recent discussions in **.NET Libraries Team Meeting** and **Team Apollo**, then produces a digest focused on team awareness and cross-team collaboration opportunities.

For career-level context, see [../dotnet-ai-standup-feedback/CAREER-CONTEXT.md](../dotnet-ai-standup-feedback/CAREER-CONTEXT.md).

## Workflow

1. **Gather data** — Run all WorkIQ queries below
2. **Synthesize** — Organize by source, then cross-reference for opportunities
3. **Present** — Render the digest

## Step 1: WorkIQ Queries

Run ALL queries using `workiq-ask_work_iq`:

| # | Question |
|---|----------|
| 1 | "What was discussed in the .NET Libraries Team Meeting this week?" |
| 2 | "What decisions or action items came out of the .NET Libraries Team Meeting recently?" |
| 3 | "What was discussed in Team Apollo meetings or chat this week?" |
| 4 | "What are the current priorities for Team Apollo?" |
| 5 | "What cross-team projects or collaborations were mentioned in the .NET Libraries Team Meeting or Team Apollo?" |
| 6 | "What issues, PRs, or work items were highlighted in the .NET Libraries Team Meeting?" |
| 7 | "Are there any areas in the .NET Libraries Team Meeting or Team Apollo that need additional help or contributors?" |

## Step 2: Output Template

```markdown
# 📊 .NET Team Updates — Week of [DATE]

## 📋 Updates

### .NET Libraries Team Meeting
- [Update with attribution]

### Team Apollo
- [Update with attribution]

## 🏁 Priorities & Decisions
> Key decisions made, priorities set, or direction changes.

- **[Decision/Priority]** — [Context, who decided, implications]

## ⚡ Actions Required
> Items that need your direct response, review, or participation.

- **[Action]** — [Context, deadline, requestor]

## 🌱 Optional Actions (Growth-Oriented)
> Not assigned to you, but aligned with career growth.

- **[Action]** — [Why it's optional: not assigned / low urgency]
  · [Why you'd want to do it: skill development / visibility / demonstrates cross-team ability]

## 🤝 Cross-Team Collaboration Opportunities (SWE 59-60)
> Projects or initiatives that span teams — ideal for demonstrating L59→60 growth.

- **[Project/Initiative]** — [Teams involved] · [How to get involved] · [Expected impact]

## 📈 Growth Opportunities
> Smaller issues, mentoring chances, or areas to build expertise beyond your assigned work.

- **[Opportunity]** — [Effort estimate: small/medium/large] · [Growth signal: ownership / breadth / leadership]

## 🔭 Radar Items
> Not actionable now, but worth tracking for future relevance.

- [Item with brief context on why to watch it]
```

## Notes

- Prioritize "Cross-Team Collaboration Opportunities" — this is the highest-value section for L59-60 growth.
- "Radar Items" captures things mentioned in passing that may become relevant later.
- If a source meeting didn't happen this week, note that rather than leaving the section blank.
