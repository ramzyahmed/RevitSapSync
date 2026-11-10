# CLAUDE.md - RevitSapSync

> Self-contained copy for cloud sessions. The global rules below mirror https://github.com/ramzyahmed/claude-config (CLAUDE.md): keep them in sync. Project notes take precedence over the global rules.

## Project notes

Revit and SAP2000 synchronisation project (`src/`, `tests/`). No project-specific rules are recorded yet: read the code before assuming. Revit and SAP2000 are not available in the cloud.

Before touching any ETABS or SAP2000 API code, read `docs/CSI_API_NOTES.md` (live-tested crash hazards, units and API differences).

## Cloud session notes

- Cloud sessions run on Linux: they cannot build or run WPF/WinForms apps, ETABS, SAP2000, Revit or Office. Edit carefully, run only tests that build on Linux, and state clearly what was not verified.
- Push work to a `claude/...` branch (or the branch the user names), never straight to the default branch. The user pulls it in Visual Studio, builds and tests it there.
- Never commit proprietary files (CSI/Autodesk/McNeel DLLs, AISC/ACI PDFs), credentials or build output.

---

# CLAUDE.md

# Project Instructions

This document defines the coding standards, architecture principles, and AI behavior for this project.

These instructions apply to every task unless explicitly overridden by the user.

---

# Primary Technology Stack

Unless explicitly instructed otherwise, or a project's own CLAUDE.md states a justified override, assume the following stack for all projects:

- Language: C#
- Framework: .NET
- UI framework: WPF
- Architecture: MVVM via CommunityToolkit.Mvvm
- Testing: xUnit
- Serialization: System.Text.Json (unless a project has already established another library — do not introduce a second serialization approach into an existing project without clear justification)

Do not introduce additional languages, frameworks, libraries, or architectural patterns unless there is a clear technical justification and the existing stack cannot reasonably satisfy the requirement.

---

# Project Documentation Files (.md)

Each project should maintain Markdown documentation files that describe the project knowledge, architecture, and development rules.

These documentation files are the primary source of project understanding.

## Before Accessing Source Code

Always follow this order:

### Step 1 - Read Documentation Files First

Before opening source code, read the available documentation files:

- CLAUDE.md
- PROJECT_CONTEXT.md
- ARCHITECTURE.md
- PROJECT_STRUCTURE.md
- AI_MEMORY.md
- Any other relevant `.md` documentation files

Use these files to understand:

- Project purpose.
- Technologies and frameworks.
- Architecture.
- Folder structure.
- Main components.
- Existing design decisions.
- Coding standards.
- Previous decisions and lessons learned.

### Step 2 - Access Source Code Only When Required

Only inspect `.cs`, `.xaml`, configuration files, or other source files when:

- Documentation does not contain enough information.
- Implementation details are required.
- Existing behavior needs verification.
- The requested task requires code changes.

## Token Efficiency Rules

- Do not scan the entire project unnecessarily.
- Do not open unrelated files.
- Avoid loading large amounts of code when documentation already provides the required context.
- Start with documentation files to minimize context usage.
- Read only the minimum required files to complete the task.

## Documentation Maintenance

When significant architectural changes are made:

- Update the relevant Markdown documentation files.
- Keep documentation accurate and concise.
- Do not duplicate information across multiple files.
- Prefer updating existing documentation instead of creating unnecessary files.

## New Project Documentation

When starting a new project, create:

### CLAUDE.md

Contains:

- AI behavior rules.
- Coding standards.
- Development workflow.

### PROJECT_CONTEXT.md

Contains:

- Project purpose.
- Technologies.
- Frameworks.
- Main features.
- External dependencies.

### ARCHITECTURE.md

Contains:

- Application architecture.
- Layers.
- Data flow.
- Major components.
- Responsibilities.

### PROJECT_STRUCTURE.md

Contains:

- Folder organization.
- Important files.
- Responsibilities of major components.

### AI_MEMORY.md

Contains:

- Important decisions.
- Lessons learned.
- Reusable patterns.
- Known limitations.
- Future improvements.

---

# General Principles

- Prioritize readability over clever implementations.
- Preserve existing functionality unless explicitly requested otherwise.
- Prefer refactoring over rewriting.
- Keep the codebase simple, maintainable, and easy to understand.
- Avoid unnecessary abstractions and overengineering.
- Reuse existing code before creating new implementations.
- Remove dead, redundant, or obsolete code whenever encountered.
- Follow SOLID principles only when they clearly improve maintainability.
- Always choose the simplest solution that satisfies the requirements.

---

# Naming Conventions

## General

- Use meaningful and descriptive names.
- Names should clearly describe their responsibility or purpose.
- Avoid abbreviations unless they are industry standard (UI, API, PDF, JSON, etc.).
- Avoid generic names such as:
  - temp
  - data
  - obj
  - item
  - list
  - value

## Classes

- Use PascalCase.
- Class names should clearly describe what they represent or do.

Good

- BeamReactionService
- LoadCombinationGenerator
- ExcelExporter

Bad

- Service1
- Manager
- Helper
- Utils

## Methods

- Use PascalCase.
- Method names should begin with a verb.
- Method names should clearly describe the action performed.

Good

- CalculateBeamReactions()
- GenerateLoadCombinations()
- ReadExcelFile()

Bad

- Process()
- Execute()
- Method1()

## Variables

- Use camelCase.
- Use descriptive names.

Good

- selectedLoadCombinations
- beamReactions
- loadCombinationName

Bad

- x
- obj
- temp
- list

## Boolean Variables

Always begin with:

- Is
- Has
- Can
- Should

Examples

- isValid
- hasReaction
- canGenerate
- shouldUpdate

---

# Class Organization

Each class should have one clear responsibility.

Recommended member order:

1. Constants
2. Private fields
3. Constructor
4. Public properties
5. Public commands
6. Public methods
7. Protected methods
8. Private helper methods

Rules

- Keep classes focused.
- Split classes when responsibilities grow.
- Avoid excessively large classes.
- Group related methods together.
- Keep the public API near the top.

---

# Method Style

Methods should perform one task only.

Rules

- Keep methods short and readable.
- Target approximately 20–40 lines whenever practical.
- Prefer early returns.
- Avoid deep nesting.
- Extract complex logic into private helper methods.
- Keep helper methods private unless intended for reuse.

## Method Calls and Fluent APIs

Prefer clarity. LINQ chains and fluent-style calls are idiomatic modern C# — use them when they improve readability.

Acceptable (fluent/LINQ style):

```csharp
var beams = frames
    .Where(f => f.MemberType == MemberType.Beam)
    .OrderBy(f => f.StartPoint.Z)
    .ThenBy(f => f.StartPoint.Y)
    .ToList();

var displayText = frame.Position!.Value.ToDisplayText();
```

Avoid (deeply nested, hard to follow):

```csharp
SaveReactions(CalculateReactions(GetBeam(LoadModel(GetFilePath()))));
```

Use intermediate variables when a sequence of non-fluent method calls would be hard to follow:

```csharp
var beam = GetBeam();
var reactions = CalculateReactions(beam);
SaveReactions(reactions);
```

---

## Delegates

Unless explicitly requested, do not use:

- Action
- Func
- Predicate

Do not pass methods as parameters for callback patterns.

Prefer explicit method calls. (Exception: LINQ's `Where(predicate)`, `Select(projection)`, and other standard query operators are idiomatic and acceptable.)

---

# MVVM

Use CommunityToolkit.Mvvm.

Use

- ObservableObject
- ObservableProperty
- RelayCommand
- AsyncRelayCommand

Rules

- Never implement INotifyPropertyChanged manually.
- Keep ViewModels responsible only for UI coordination.
- Business logic belongs in Services.
- Views should contain presentation only.
- Avoid code-behind except for UI-specific behavior.

---

# Dependency Injection

- Use constructor injection.
- Avoid service locators.
- Avoid static services.
- Use interfaces only when they provide a clear benefit.
- Do not create interfaces for every class unnecessarily.

---

# Error Handling

- Validate public inputs.
- Throw meaningful exceptions.
- Never swallow exceptions.
- Avoid empty catch blocks.
- Log errors when appropriate.
- Return meaningful error information.

---

# Collections

Prefer explicit collection types.

Good

```csharp
List<BeamModel>

List<LoadCombination>

Dictionary<string, BeamModel>
```

Avoid excessive use of generic abstractions such as

- IEnumerable<T>
- ICollection<T>
- IList<T>

unless they provide a clear benefit.

Never return null collections.

Return empty collections instead.

---

# Async Programming

- Use async/await.
- Avoid .Result.
- Avoid Wait().
- Avoid async void except UI events.
- Use CancellationToken when appropriate.

---

# XAML

- Keep XAML clean.
- Move logic into ViewModels.
- Use bindings instead of code-behind.
- Avoid duplicated styles.
- Use DataTemplates where appropriate.
- Use converters only when necessary.

---

# Documentation

Every public class should include an XML summary describing its purpose.

Every public method should include XML documentation describing:

- Purpose
- Parameters
- Return value (if applicable)

Complex private methods should also include a short summary when it improves readability.

Comments should explain **why**, not **what**.

Avoid comments that simply repeat the code.

Bad

```csharp
// Increment index
index++;
```

Good

```csharp
// Required because ETABS may return duplicate load combinations.
```

---

# Code Generation Rules

Before generating code

- Search the project for existing implementations.
- Reuse existing classes, methods, and utilities whenever possible.
- Never duplicate functionality.
- Do not invent APIs that do not exist.
- Preserve public APIs unless explicitly requested otherwise.
- Return only modified files unless requested otherwise.
- Ask for missing files before making assumptions.

## Refactoring

When existing code is difficult to understand or maintain

- Refactor while preserving all functionality.
- Simplify the implementation.
- Improve readability.
- Improve naming where appropriate.
- Remove duplicate code.
- Remove obsolete code.
- Reduce unnecessary complexity.
- Keep the implementation easy to understand.
- Prefer incremental refactoring over complete rewrites.
- Do not introduce unnecessary design patterns.

---

# Performance

- Avoid unnecessary allocations.
- Avoid repeated LINQ enumeration.
- Cache repeated lookups when appropriate.
- Avoid creating unnecessary objects inside loops.
- Prefer simple loops over complex LINQ when readability improves.

---

# Engineering Rules

Engineering correctness always has higher priority than optimization.

- Never modify engineering calculations unless explicitly requested.
- Separate calculations from UI.
- Separate API integrations from business logic.
- Keep external software interactions inside dedicated services.
- Avoid hardcoded engineering values.
- Use constants or configuration when appropriate.
- Keep calculations deterministic and easy to verify.

---

# Architecture Philosophy

- Favor simple solutions.
- Avoid abstractions that do not solve a real problem.
- Avoid unnecessary factories, base classes, interfaces, extension methods, or design patterns.
- Prefer composition over inheritance.
- Make the code understandable within a few minutes by another developer.
- Optimize for maintainability instead of cleverness.

---

# AI Behavior

These instructions apply to every coding task.

## Before Writing Code

- Read and understand the relevant files before making modifications.
- Understand the existing architecture.
- Search the project for existing implementations before creating new ones.
- Reuse existing code whenever possible.
- Never duplicate functionality.
- Never assume missing classes, methods, or APIs exist.
- Ask for clarification if requirements are unclear.

---

## While Implementing

- Keep changes as small as possible.
- Preserve existing behavior.
- Follow every rule in this document.
- Keep implementations simple.
- Maintain consistency with the project.
- Avoid unnecessary abstractions.
- Avoid introducing unnecessary dependencies.
- Reuse existing logic instead of rewriting it.

---

## Existing Behavior

Before modifying existing code

- Understand why the current implementation exists.
- Preserve the existing workflow unless instructed otherwise.
- Do not replace a working implementation with a completely different design unless there is a clear technical advantage.
- Prefer incremental improvements over complete rewrites.

---

## Refactoring

If existing code is poorly structured

- Improve readability.
- Improve maintainability.
- Preserve functionality.
- Remove duplication.
- Improve naming.
- Split large classes when appropriate.
- Split large methods into smaller focused methods.
- Refactor only the code related to the requested task unless additional refactoring clearly improves maintainability.

---

## Communication

Before making significant architectural changes

- Briefly explain the proposed approach.
- Recommend the simplest and most maintainable solution.
- Mention potential side effects before making breaking changes.
- Explain why a different design is better before implementing it.

---

## Code Generation

Generate production-ready code.

- Ensure generated code compiles whenever possible.
- Do not leave placeholder implementations unless requested.
- Do not generate unused files, classes, or methods.
- Remove unused using statements.
- Keep formatting clean and consistent.

---

## Final Review

Before completing any task, verify that

- The requested functionality has been implemented.
- Existing functionality has not been broken.
- The code follows every rule in this document.
- There is no duplicated logic.
- Naming is meaningful and consistent.
- Methods remain small and focused.
- Classes have a single responsibility.
- Unused code has been removed.
- The implementation is the simplest maintainable solution.

---

# Rule Precedence & Subagent Architecture

This project's engineering knowledge is organized in layers so that rules are defined exactly once and every consumer — the main session, any subagent, any skill — stays current automatically without duplication.

## Layers

1. **Global CLAUDE.md** (this file, `~/.claude/CLAUDE.md`) — the single source of truth for coding standards, architecture principles, naming conventions, and the technology stack. Applies to every project unless a project's own CLAUDE.md explicitly overrides it.
2. **Project-specific CLAUDE.md** — lives in a project's own folder (e.g. `EngineeringDocumentControl/CLAUDE.md`). Contains only stated deltas on top of this file — never a full restatement. Absence of a project-specific rule on a topic means the global rule remains in effect; never assume the global rule has been removed just because a project file is silent on it. Any override must state explicitly what it overrides and why.
3. **SubAgents** (`~/.claude/agents/*.md`) — domain-specific expertise only (e.g. structural calculation, CSI/Revit platform integration, desktop UI, infrastructure). A subagent must never redefine or restate coding conventions, architecture rules, or project philosophy — it inherits them by reading the same global + project CLAUDE.md hierarchy for whatever directory it's invoked in. If a domain recommendation would conflict with an established rule, the subagent must say so explicitly rather than silently overriding it.
4. **Skills** (`~/.claude/skills/*/SKILL.md`) — packaged procedures. They don't read CLAUDE.md themselves; they run inside whichever session invoked them and inherit that session's already-loaded rules for free.

## Rule precedence order

1. Explicit user instruction in the current conversation
2. Project-specific CLAUDE.md (a stated delta/override)
3. This global CLAUDE.md
4. Subagent/domain-specific expertise (fills gaps the rules above don't address)
5. General model knowledge (last resort only)

A project-specific rule overriding a global one must say so explicitly, state what it overrides, and give the reason — never silently diverge. This precedence order governs rule *authority*; it is distinct from the Decision Priority below, which governs trade-offs between competing code-quality goals once the applicable rules are already known.

---

# Decision Priority

When making implementation decisions, follow this priority order:

1. Correctness
2. Preserve existing functionality
3. Simplicity
4. Readability
5. Maintainability
6. Consistency with the existing project
7. Performance
8. Extensibility

Never sacrifice a higher-priority item to improve a lower-priority one unless explicitly instructed.
