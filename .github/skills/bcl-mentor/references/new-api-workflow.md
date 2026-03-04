# Protocol: New API Design Workflow

Step-by-step guide for designing, prototyping, and getting a new API approved in dotnet/runtime.

## Overview

```
  Idea / Issue
       │
       ▼
  ┌─────────────┐    ┌──────────────────────────────────────────────┐
  │ Phase 0:    │───►│ Check for prior proposals on same topic      │
  │ Gather &    │    │ Check existing workarounds                   │
  │ Assess      │    │ Read: issue-guide.md for area labels         │
  └──────┬──────┘    └──────────────────────────────────────────────┘
         │
         ▼
  ┌─────────────┐    ┌──────────────────────────────────────────────┐
  │ Phase 1:    │───►│ Read FDG digest                              │
  │ Research    │    │ Read existing APIs in target namespace        │
  │             │    │ Search grep.app + apisof.net for usage        │
  └──────┬──────┘    └──────────────────────────────────────────────┘
         │
         ▼
  ┌─────────────┐    ┌──────────────────────────────────────────────┐
  │ Phase 2:    │───►│ Create branch: api-proposal/<short-name>     │
  │ Prototype   │    │ Implement + tests + ref assembly generation  │
  │             │    │ Validate: build, test, TFM compat            │
  └──────┬──────┘    └──────────────────────────────────────────────┘
         │
         ▼
  ┌─────────────┐    ┌──────────────────────────────────────────────┐
  │ Phase 3:    │───►│ Invoke code-review skill                     │
  │ Review      │    │ Fix all errors/warnings                      │
  │ (BLOCKING)  │    │ Consider performance-benchmark skill          │
  └──────┬──────┘    └──────────────────────────────────────────────┘
         │
         ▼
  ┌─────────────┐    ┌──────────────────────────────────────────────┐
  │ Phase 4:    │───►│ Write proposal using template                │
  │ Draft       │    │ Extract API surface from ref source           │
  │ Proposal    │    │ Include: motivation, usage, design decisions  │
  └──────┬──────┘    └──────────────────────────────────────────────┘
         │
         ▼
  ┌─────────────┐    ┌──────────────────────────────────────────────┐
  │ Phase 5:    │───►│ File issue with api-suggestion label         │
  │ Publish     │    │ Or post as PR with prototype                 │
  │             │    │ Link to prototype commit                     │
  └──────┬──────┘    └──────────────────────────────────────────────┘
         │
         ▼
  ┌─────────────┐
  │ Phase 6:    │    Owner assigns → Discussion → api-ready-for-review
  │ Iterate     │    → FXDC review → api-approved / api-needs-work
  └─────────────┘
```

## Key Documentation Links

| Document | URL | When to Read |
|---|---|---|
| **API Review Process** | https://github.com/dotnet/runtime/blob/main/docs/project/api-review-process.md | Before starting any API work |
| **Framework Design Guidelines Digest** | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/framework-design-guidelines-digest.md | During naming and design |
| **API Proposal Template** | https://github.com/dotnet/runtime/issues/new?template=02_api_proposal.yml | When filing the issue |
| **Updating Ref Source** | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/updating-ref-source.md | After prototype, before proposal |
| **Breaking Change Rules** | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md | When modifying existing APIs |
| **Issue Guide (Area Labels)** | https://github.com/dotnet/runtime/blob/main/docs/project/issue-guide.md | To find the area owner |

## Checklist: Before Filing an API Proposal

- [ ] Searched for existing proposals on the same topic (closed + open issues)
- [ ] Identified existing workarounds and why they're insufficient
- [ ] Read the Framework Design Guidelines digest
- [ ] Built a working prototype with tests
- [ ] Generated reference assembly source (`dotnet msbuild /t:GenerateReferenceAssemblySource`)
- [ ] Ran code-review skill against the prototype
- [ ] Checked for breaking changes (source + binary)
- [ ] Checked TFM compatibility if library ships netstandard/netfx

## The API Review Meeting

- **Schedule**: Tuesdays 10am-12pm Pacific — https://apireview.net/schedule
- **Backlog**: https://aka.ms/ready-for-api-review
- **Live stream**: https://www.youtube.com/@NETFoundation/streams
- **Notes published**: https://github.com/dotnet/apireviews

## Tips for Newcomers

1. **Don't submit a PR before API approval** — the API must be `api-approved` first
2. **Terse proposals win** — reviewers read dozens, keep it focused
3. **Prototype first, propose second** — the ref source IS the proposal
4. **One proposal per issue** — don't bundle multiple API additions
5. **Fast track**: Add both `api-ready-for-review` AND `blocking` labels
