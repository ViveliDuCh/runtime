---
name: dotnet-ai-standup-feedback
description: Generates a career-focused feedback loop from ".NET AI Standup" meetings, surfacing team updates, contribution gaps, and growth opportunities for SWE 59. Use when asked for "AI standup feedback", ".NET AI standup updates", "where can I contribute to AI", "AI contribution gaps", or for weekly career-growth check-ins. Connects to dotnet/extensions triaging and work-finder workflows.
---

# .NET AI Standup Feedback Loop

Queries WorkIQ for recent **.NET AI Standup** discussions, then produces a feedback loop focused on:
- What the team is shipping and prioritizing
- Where gaps exist that match SWE 59-level contribution scope
- Actionable growth opportunities

For career-level context and what "SWE 59 impact" means, see [CAREER-CONTEXT.md](CAREER-CONTEXT.md).

## Workflow

1. **Gather data** — Run all WorkIQ queries below
2. **Analyze** — Cross-reference updates with career context to identify gaps
3. **Present** — Render the feedback loop digest

## Step 1: WorkIQ Queries

Run ALL queries using `workiq-ask_work_iq`:

| # | Question |
|---|----------|
| 1 | "What was discussed in the .NET AI Standup meeting this week?" |
| 2 | "What are the current priorities and focus areas mentioned in the .NET AI Standup?" |
| 3 | "What challenges, blockers, or gaps were mentioned in the .NET AI Standup recently?" |
| 4 | "What new features, APIs, or work items were mentioned in the .NET AI Standup?" |
| 5 | "Who is working on what in the .NET AI Standup? What areas need help?" |
| 6 | "What dotnet/extensions issues or PRs were discussed in the .NET AI Standup?" |
| 7 | "What upcoming deadlines or milestones were mentioned in the .NET AI Standup?" |

## Step 2: Output Template

```markdown
# 🎯 .NET AI Standup Feedback — Week of [DATE]

## 📋 Team Updates
> What the .NET AI team shipped, discussed, or decided this week.

- [Update with owner attribution]

## 🏁 Current Priorities & Milestones
> What the team is focused on and upcoming deadlines.

- **[Priority]** — [Owner, timeline, status]

## 🔍 Contribution Gaps (SWE 59 Scope)
> Areas where you could contribute meaningfully at your level.
> See CAREER-CONTEXT.md for what qualifies as SWE 59-level impact.

- **[Gap area]** — [What's missing] · [Why it's SWE 59-appropriate: scope, complexity, visibility]

## ⚡ Actions Required
> Items that directly affect you or need your response.

- **[Action]** — [Context, deadline, who's asking]

## 🌱 Optional Actions (Growth-Oriented)
> Not assigned to you, but doing them builds skills or visibility for level progression.

- **[Action]** — [Why it's optional: not assigned/not urgent] · [Why do it: builds skill X / demonstrates impact Y / creates visibility with Z]

## 🚀 Growth Opportunities
> Concrete ways to generate impact toward SWE 59→60 progression.

- **[Opportunity]** — [How to get started] · [Expected impact: feature ownership / cross-team collab / mentoring]

## 🔗 dotnet/extensions Connection
> Issues, PRs, or areas in dotnet/extensions related to this week's standup topics.
> Use this section to feed into issue triaging or the work-finder skill.

- [Issue/area link or description]
```

## Notes

- The "Contribution Gaps" section is the core value of this skill — analyze what's NOT being covered, not just what IS.
- When identifying gaps, filter for SWE 59 scope: feature-level ownership, not team-level strategy.
- If standup content is thin, surface that honestly and suggest probing questions for next standup.
