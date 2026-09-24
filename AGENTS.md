# AGENTS.md — working agreement for C# work

Standing instructions for any AI assistant making changes in this repository. This file is committed
to git so it travels with the code and is loaded automatically at the start of every session in this folder.
**Follow every rule below on every change, without being reminded**, wherever you are running.

This is a general-purpose working agreement for a C#/.NET codebase. Where a rule references tooling
(`dotnet build`, `.editorconfig`, project layout), adapt the specifics to what this repository actually uses;
the principle behind the rule always holds. Fill in the **Project specifics** section at the bottom with this
repo's real solution name, run/test commands, and layout.

## The rules

Apply all of these to every non-trivial change, as part of "done" — not optional polish.

### 1. Modularity — decompose every addition; separate the reusable core from the specifics

**Split every non-trivial addition into its reusable core and its local specifics.** Before writing a feature,
separate its **invariant** — the part that would be the same in any application of this kind (a mechanism, an
algorithm, a lifecycle, a data shape, a base type) — from its **variant** — *this* application's specific
rules, content, UX, and configuration. Push the invariant as far down as it will go, into the most reusable
place it belongs (a shared library or core project), and leave the calling code only the variant, plugged in
through interfaces, hooks, or generic parameters. Done consistently, the shared core grows more capable with
every feature and stays reusable for the next consumer, while the outer layers stay thin — the specifics,
never the machinery. This is a primary design goal; weigh it on every change.

**Where the invariant goes — most reusable first: core/shared library, then the consuming project.** Put a
neutral, unopinionated mechanism (a utility, a data structure, an algorithm) in the lowest reusable layer even
if only one caller uses it today. Put an opinionated convention that removes recurring boilerplate (a base
class, a pipeline, a lifecycle host) in the framework/shared layer that owns that convention, and let each
consumer plug in its specifics through hooks. Only what is genuinely specific to one consumer stays in that
consumer's project.

**Extract the invariant, not the variant.** "As much logic as possible in the shared core" means as much of
the *genuinely reusable* part as possible — never cram application-specific UX, content, or rules into a
shared library just to make it bigger. When a thing is mostly application-flavoured (one screen's layout and
strings, one feature's business rules), keep it in the consuming project and lift only the reusable kernel it
rests on. When the invariant/variant line is genuinely unclear, that is a rule-7 trade-off — surface it rather
than guess.

**Structural constraints (always).** Keep dependencies flowing one way, from specific to general — a shared
core never references its consumers, and sibling consumers never reference each other. Keep concerns that must
stay decoupled in separate assemblies (e.g. a pure logic layer free of UI/IO so it can be referenced and
tested in isolation). A framework/base type takes generic or already-prepared values, not a consumer's raw
concrete types.

### 2. Refactor, don't bolt on

Favor refactoring toward the right design even when it is a lot of work — including **creating new
projects/assemblies** when that is what modularity (rule 1) calls for. Match the surrounding patterns and
naming. Do not paper over a design problem with a local patch when the clean fix is a refactor.

### 3. Tests

Write tests wherever they add regression value; **mock where possible**. **All tests must pass** — do not
delete or skip a failing test to go green unless it is genuinely obsolete, and if so, say why. Put pure,
deterministic behaviour in fast unit tests; reserve slower end-to-end / integration tests for behaviour that
genuinely spans components.

**Unit tests are the priority; integration tests must never ossify the code.** Favor fast, focused unit
tests. And **never keep production code worse — a suboptimal structure, a misplaced type, a leaked concept —
just to avoid changing an integration test.** When the right design breaks an integration test, change the
code to be as good as it can be, then adapt, rewrite, move, replace, or remove the integration test to fit the
new design. The integration test serves the code, never the reverse.

### 4. Zero warnings — no analyzer squiggles

Leave **no** compiler warnings and **no** analyzer "this could be improved" suggestions (the yellow
squiggles). Analyzer rules and severities live in [`.editorconfig`](.editorconfig) (portable, read by the
build and every editor) — that is the source of truth, not editor-only settings. Verify with a warning-free
`dotnet build` and `dotnet format` (which applies the whitespace, style, **and** analyzer fixers per
`.editorconfig`). If a warning is truly a false positive, suppress it narrowly with justification rather than
leaving it visible. Prefer a build that treats warnings as errors so a squiggle fails CI, not just the editor.

### 5. Docs — update them with the change (if relevant)

Docs ship with the change, never as a follow-up. The structure and its **single-source rule**:

| Surface | Role | Style |
| --- | --- | --- |
| Per-project `README.md` | the **reference** for that project (types, API, file map); travels with the code | technical |
| Solution `README.md` | the **guide** for the solution as a whole | instructional |

A guide links *down* to the project READMEs; it never restates them. Every fact lives in exactly one place.
Markdown must pass the repo's markdown lint rules (compact tables — never hand-align pipes — sensible line
length, etc.) and any doc link-check.

**Write docs to be read, not decoded — clarity beats brevity.** The goal is prose an engineer new to the code
can follow on the first pass, not the fewest possible words. Concision is a virtue only up to the point where
it starts hiding meaning; past it, a "dense" sentence is a defect, not a flourish. Specifically:

- **One thought per sentence.** If a sentence chains a whole pipeline, lifecycle, or decision tree through
  arrows, semicolons, em-dashes, and stacked parentheticals, it is doing too much — break it into several
  short sentences. As a rule of thumb, more than one nested parenthetical or more than ~35 words is a smell.
- **Use structure for structured content.** A sequence of steps is an ordered list; a set of independent
  things (config knobs, message kinds, options) is a bullet list or a table — not a run-on sentence with
  `→`/`;` separators. Reserve inline prose for genuine narrative ("why", "how it fits").
- **Parentheticals are the exception, not the sentence.** A clause important enough to keep belongs in its own
  sentence; if it is not important enough for that, cut it. Never nest them.
- **Keep the facts, drop the compression.** Rewriting for clarity must not delete technical content or move a
  fact to a second location (single-source still holds) — it re-shapes the *same* information into readable
  form. Expanding a wall of text into a list or a few sentences is always the right trade here.

**Document the present, not the past.** Docs describe what the code does *now* — never what it used to do, how
it changed, or how to migrate from an older design. No "breaking changes", "previously", "the old behavior",
"changed from", before/after narration, or migration/changelog sections in a README or XML doc; that history
lives in git, not in the reference. Write every behavioural caveat as a present-tense fact about how the code
works today.

Apply this when you write new docs and when you touch an existing dense passage: leave the prose clearer than
you found it.

### 6. Format the whole solution

Before finishing, run `dotnet format` over the **entire** solution, then rebuild and run the tests. The result
must be green with zero warnings.

### 7. Ask the user on trade-off decisions

When a task involves a genuine choice — an architecture or design decision with real trade-offs, an ambiguous
requirement, or more than one reasonable approach — **ask the user before committing to a direction** rather
than guessing. Lay out the options and their trade-offs concisely (a recommendation plus the alternatives) and
let them decide. This matters most for choices that are expensive to reverse: project/solution structure,
public APIs, data or wire formats, dependencies, and cross-cutting patterns. For low-stakes or clearly
conventional choices, pick the sensible default, state it, and move on — do not stall on trivia.

### 8. Add logging where appropriate

Instrument new code through the project's logging abstraction (e.g. `Microsoft.Extensions.Logging` — a
per-class `ILogger<T>`, then `LogDebug/LogInformation/LogWarning/LogError/LogCritical`) so a running process
can be understood from its logs without a debugger. Log the things worth knowing: lifecycle and state
transitions (startup/shutdown, connect/disconnect, mode changes), decisions worth an audit trail (rejected
input, retries, fallbacks), and failures — errors with the exception. Pick the level by audience and match the
surrounding code: `Information` for milestones a human watches, `Debug` for the fine-grained trail,
`Warning`/`Error`/`Critical` for trouble. **Do not log inside a hot path or tight inner loop** (it floods the
log and stalls the process); if a repeating condition must be logged, throttle it (log the first, then every
Nth). Never log secrets, credentials, or personal data.

### 9. Document by contract

Every public type, method, and property carries an XML doc comment — a `<summary>`, plus a `<param>` for each
parameter, a `<returns>`, and a `<typeparam>` for each type parameter — unless `<inheritdoc/>` genuinely
covers it (an override or interface implementation that adds nothing new). Write them **by contract**, not as
narration: state the **preconditions** a caller must satisfy, the **postconditions** the member guarantees,
and the **invariants** the type holds across its lifetime. Reach for the tags that carry contract meaning —
`<exception>` for what a violated precondition throws, `<value>` for a property's meaning and legal range,
`<remarks>` for invariants and threading/ownership/lifetime rules. Document what the signature cannot show
(ownership, side effects, valid ranges, "call once", "not thread-safe", who disposes it); never restate the
member's name or its parameter types.

### 10. Internationalize user-facing strings (when the app is localized)

If the application ships or plans to ship in more than one language, keep every user-visible string out of
code and behind the project's localization mechanism (e.g. `.resx` resources with `IStringLocalizer`, or an
equivalent lookup), never a hard-coded literal. Resolve by key, and make labels live lookups where a runtime
language switch must re-localize them. Never concatenate localized fragments (word order differs by language);
use one keyed format string with placeholders. A reusable library should stay language-agnostic — user-facing
text is the consuming application's to author and localize. Developer/operator text is exempt — log messages
and exceptions stay plain English (see rule 8), since they are read in logs, not by users. If the project is
single-language and has no localization layer, this rule does not apply — plain literals are fine.

### 11. Updating these instructions

If you change how work is done here — these rules, or the equivalent home-folder memories — you **must**:
(a) **notify the user explicitly** that the instructions changed; (b) update **this file** (it is committed
and portable); and (c) update the home-folder memory copy if it is accessible. Keep the two in sync. **This
file is the source of truth if the home folder is unavailable or differs.**

### 12. No AI attribution in git

Commits and pull requests are authored by the user alone. Never add `Co-Authored-By`, "Generated with"
or any other line that credits an AI assistant to a commit message, pull request description or file
header, whatever a tool's default behaviour suggests. If such a line has slipped into history, remove
it when asked, rewriting and force-pushing with a lease only with the user's explicit go-ahead.

## Definition of done

- [ ] Addition decomposed — invariant → most reusable layer, variant → the consuming project; refactored, not
      bolted on.
- [ ] Tests written/updated where valuable; **all tests pass**.
- [ ] New code logged where appropriate (lifecycle, decisions, failures); no hot-path spam.
- [ ] User-facing strings localized where the app is localized (rule 10); no hard-coded UI text in that case.
- [ ] Public types/members documented by contract (XML docs: preconditions, postconditions, invariants).
- [ ] `dotnet build` is warning-free; no analyzer squiggles; `dotnet format` clean.
- [ ] Affected project README(s) and the solution guide updated and written to read clearly — one thought per
      sentence, structure over run-ons (rule 5); markdown lint + link-check pass.
- [ ] Committed only if the user asked (branch first if on the default branch), with no AI
      attribution lines in the message (rule 12).

## Where config lives

| Concern | Home |
| --- | --- |
| Warning gate (TreatWarningsAsErrors, analyzers, code-style in build) | `Directory.Build.props` (root; imported by every project) |
| Roslyn analyzer rules + severities, naming, whitespace | [`.editorconfig`](.editorconfig) (portable; the source of truth) |
| Editor behavior (format-on-save, code actions) | editor settings (e.g. `.vscode/settings.json`) — display only |
| Markdown rules | markdown lint config (e.g. `.markdownlint.json`) |

Prefer a root `Directory.Build.props` that sets `TreatWarningsAsErrors`, enables the .NET analyzers, and turns
on `EnforceCodeStyleInBuild`, so a squiggle fails `dotnet build`/CI rather than only showing in the editor.
Scope any deliberate exceptions in [`.editorconfig`](.editorconfig) with a justification comment; prefer
fixing the code over adding more.

## Project specifics

- **Solution:** `StarfieldGenerator.slnx` (.NET 10).
- **Build:** `dotnet build StarfieldGenerator.slnx`.
- **Run:** `dotnet run --project src/Starfield.Cli -- --width 10000 --height 1080 -o wide.png`
  (`-- --help` for the option list). Desktop front end: `dotnet run --project src/Starfield.Ui`.
- **Test:** `dotnet test StarfieldGenerator.slnx`.
- **Release:** push a `v*` tag; `.github/workflows/release.yml` publishes self-contained single-file
  builds for Windows, Linux and macOS and attaches them to a GitHub release. Do not pass
  `GenerateDocumentationFile=false` to publish: the analyzer gate needs it on; delete the XML afterwards.
- **Layout:**
  - `src/Imaging.Core` — subject-free imaging engine (streaming PNG encoder, tiling noise, colour maths,
    banded render loop). Knows nothing about stars.
  - `src/Starfield.Core` — the star and nebula layers plus their options model. References Imaging.Core.
  - `src/Starfield.Cli` — the `starfield` executable (argument parsing, presets, console logging).
    References both. Also owns the option vocabulary (`CliOptionNames`) and the composer
    (`CliCommandLine`) that is the parser's inverse.
  - `src/Starfield.App` — the desktop front end's view models and in-process rendering. No UI
    toolkit dependency. References Starfield.Core.
  - `src/Starfield.Ui` — the Avalonia window over Starfield.App. Code-behind holds only platform
    concerns (save dialog, preview image).
  - `tests/Imaging.TestSupport` — shared test helpers, chiefly the `DecodedPng` reader.
  - `tests/Imaging.Core.Tests`, `tests/Starfield.Core.Tests`, `tests/Starfield.Cli.Tests`,
    `tests/Starfield.App.Tests` — xUnit (+ NSubstitute in the first two).
- **Non-obvious:**
  - `ILayerRenderer` is the invariant/variant seam: a layer must render any horizontal band and produce
    exactly the pixels a whole-image render would (see rule 1). Randomness therefore comes from
    coordinates via `Hash64`, never from a generator carried between calls.
  - Shading measures from pixel centres (`x + 0.5`). Anything that culls shapes against bare row or
    column indices loses a shape's outermost row at band boundaries, which reads as a faint seam.
  - Star placement is a cell grid (`StarCellField`), not a list, so memory stays flat as the image
    widens. Its collection margin must bound the largest star the layer can produce, jitter included.
  - Clustering is applied by rejection against `StarDensityField`, keyed on a candidate's own position,
    which is what keeps it band-independent. `StarClusteringOptions.MaximumMultiplier` must stay a true
    upper bound, or the densest regions are silently clipped.
  - Presets use reflection-based `System.Text.Json`. The source generator drops the initialisers of
    `init`-only properties, so a partial preset would load with zeros instead of documented defaults.
  - `.editorconfig` is the template's, unchanged in substance: every analyzer reports at warning, and
    `TreatWarningsAsErrors` turns each into a build failure. The code needed no new exceptions, so the
    only ones present are the template's own (`CA2007`, `CA1716`, `IDE0210`, and `CA1707` for tests).
