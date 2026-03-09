---
name: ai-updates-digest
description: Generates a structured weekly digest of AI updates, tips, and tricks from the "AI Workgroup" and ".NET team work chat" Teams channels. Use when asked for "AI updates", "AI digest", "what's new in AI this week", "AI workgroup summary", or at the start/end of the week for AI awareness.
---

# AI Updates Digest

Queries WorkIQ for recent discussions in the **AI Workgroup** and **.NET team work chat** Teams channels, then produces a structured digest focused on AI landscape awareness.

## Workflow

1. **Gather data** — Make the WorkIQ queries below (all of them, in parallel if possible)
2. **Synthesize** — Deduplicate and categorize findings into the output template
3. **Present** — Render the final digest to the user

## Step 1: WorkIQ Queries

Run ALL of these queries using the `workiq-ask_work_iq` MCP tool:

| # | Question |
|---|----------|
| 1 | "What was discussed in the AI Workgroup Teams chat in the last 7 days?" |
| 2 | "What AI-related updates, announcements, or news were shared in the AI Workgroup Teams chat recently?" |
| 3 | "What AI tips, tricks, tools, or techniques were shared in the AI Workgroup Teams chat recently?" |
| 4 | "What AI-related topics were discussed in the .NET team work chat in the last 7 days?" |
| 5 | "Are there any action items or requests related to AI in the AI Workgroup or .NET team work chat?" |

## Step 2: Output Template

ALWAYS use this structure for the digest. Omit a section only if genuinely empty after querying.

```markdown
# 🤖 AI Updates Digest — Week of [DATE]

## 📢 Key Updates
> Major announcements, product launches, org-wide AI initiatives, policy changes.

- [Update with source attribution]

## 💡 Tips & Tricks
> Practical AI tools, prompts, workflows, or techniques shared by colleagues.

- [Tip/trick with who shared it and context]

## ⚡ Actions Required
> Items that need your direct response or participation.

- **[Action]** — [Context, deadline if any, who's asking]

## 🌱 Optional Actions
> Not required, but worthwhile for staying ahead or growing your AI skills.

- **[Action]** — [Why it's optional] · [Why you'd want to do it: relevance to your work or growth as SWE 59]

## 📎 Resources & Links
> Documents, repos, articles, or recordings mentioned.

- [Resource with brief description]
```

## Notes

- If WorkIQ returns sparse results, note it in the digest rather than fabricating content.
- Attribution matters: always note who shared something when available.
- For "self-run" weekly use, invoke this skill every Monday morning or Friday afternoon.
