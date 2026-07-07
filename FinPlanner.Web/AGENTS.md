# AGENTS.md

## Purpose
This repository is a Blazor WebAssembly application. Make minimal, safe, reviewable changes that fit the existing code style and structure.

## Required Git workflow
- Never commit directly to main or master.
- Always create or use a feature branch.
- Branch naming: feature/<short-kebab-description>
- Prefer opening a pull request when finished.

## Blazor conventions
- Check both `.razor` and matching `.razor.cs` / partial class files before concluding how a page works.
- Reuse existing services, models, and shared components before introducing new ones.
- Keep page responsibilities focused.
- If logic is shared across pages, extract it into an appropriate service or shared component rather than duplicating it.
- Preserve existing naming and folder patterns unless the task specifically calls for restructuring.

## Safety rules
- Do not remove working functionality unless the task explicitly requires it.
- Do not rename unrelated files.
- Do not perform broad refactors outside the requested scope.
- Remove dead code only when it is clearly made obsolete by the requested change.

## Validation
Before finishing:
1. restore dependencies if needed
2. build the project
3. run relevant tests if present
4. summarize modified files and why

## Likely commands
- dotnet restore
- dotnet build
- dotnet test

## Output format
At the end, provide:
- Summary
- Files changed
- Validation results
- Risks / follow-up items
