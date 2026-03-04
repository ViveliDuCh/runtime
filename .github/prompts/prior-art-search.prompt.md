---
mode: 'agent'
description: 'Search for prior art, existing patterns, and precedent in dotnet/runtime for a given topic before starting new work.'
---

Before I start implementing, help me find prior art and existing patterns for: $ARGUMENTS

## What to Search

1. **Existing implementations in the repo**: Search `src/libraries/` for similar patterns, naming, or approaches
2. **Closed issues and PRs**: Use `gh search issues "<topic>" --repo dotnet/runtime --state closed --limit 10`
3. **API review notes**: Check https://github.com/dotnet/apireviews for past decisions on similar APIs
4. **Existing skills**: Check if any `.github/skills/` already covers this area
5. **Documentation**: Search `docs/` for relevant guidelines

## What to Report

- **Existing patterns**: How does the codebase already handle similar cases?
- **Past decisions**: Were similar proposals accepted, rejected, or sent back?
- **Conventions**: What naming, structure, and implementation patterns should I follow?
- **Potential conflicts**: Would my work overlap with or contradict existing code?

Be specific — cite file paths, issue numbers, and commit hashes.
