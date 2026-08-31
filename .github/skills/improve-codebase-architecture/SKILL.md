---
name: improve-codebase-architecture
description: Find refactoring opportunities in a codebase, informed by the domain language in CONTEXT.md and the decisions in docs/adr/. Use when the user wants to improve architecture, find refactoring opportunities, consolidate tightly-coupled code, or make a codebase more testable and easier to navigate.
---

# Improve Codebase Architecture

Find code that's doing too little for how complicated it is to use, and propose refactors that fix that. The aim is testability and making the codebase easier to work in.

## What to look for

Plain language, no invented vocabulary:

- **Pass-through code.** A class or interface that just forwards to one other thing, with one real implementation and one caller. Ask: if I deleted this and inlined the real thing, would anything be lost? If not, it's ceremony, not abstraction.
- **A wrapper that's nearly as complicated as the thing it wraps.** If understanding what a piece of code does requires reading through it AND the two or three things behind it, it isn't actually hiding complexity — it's just adding a layer.
- **Duplicated logic across files** that isn't shared anywhere, so a fix in one place doesn't fix the others.
- **Extracted-for-testing functions where the real risk lives elsewhere.** Sometimes a pure helper function is well tested but the code that calls it and wires its output together is not — and that's where the actual bugs happen.
- **A class/interface with only one implementation and one caller.** That's a sign it was added on principle rather than because something genuinely needed to vary.

Two implementations of the same interface is a real reason for that interface to exist. One implementation, one caller, is usually just indirection.

This work should be informed by the project's own domain terms (from `CONTEXT.md`, if present) and past decisions (`docs/adr/`, if present) — don't re-litigate settled decisions without a real reason.

## Process

### 1. Explore

Read the project's domain glossary and any ADRs in the area you're touching first.

Then use the Agent tool with `subagent_type=Explore` to walk the codebase. Don't follow rigid heuristics — explore organically and note where you experience friction:

- Where does understanding one piece of code require bouncing between many small files?
- Where is a class/interface doing very little for how much it costs to use?
- Where were pure functions pulled out for testability, but the real bugs live in the code that calls them?
- Where does logic leak between files that are supposed to be separate?
- Which parts of the codebase are untested, or hard to test as currently written?

For anything you suspect is pointless indirection, ask: would deleting it concentrate the complexity somewhere real, or just move it one file over? "Concentrates it somewhere real" is the signal worth reporting.

### 2. Present candidates

Present a numbered list of refactor candidates. For each one:

- **Files** — which files/modules are involved
- **Problem** — why the current code is causing friction, in plain terms
- **Solution** — plain English description of what would change
- **Benefits** — what gets easier to change, what gets easier to test, and why

**Use CONTEXT.md vocabulary for the domain.** If `CONTEXT.md` defines "Order," talk about "the Order intake code" — not "the FooBarHandler," and not invented architecture-speak.

**ADR conflicts**: if a candidate contradicts an existing ADR, only surface it when the friction is real enough to warrant revisiting the ADR. Mark it clearly (e.g. _"contradicts ADR-0007 — but worth reopening because…"_). Don't list every theoretical refactor an ADR forbids.

Do NOT design the new interface yet. Ask the user: "Which of these would you like to explore?"

### 3. Follow-up conversation

Once the user picks a candidate, talk through the design with them — constraints, dependencies, what the refactored code would look like, what tests survive.

Side effects happen inline as decisions crystallize:

- **Naming something after a concept not in `CONTEXT.md`?** Add the term to `CONTEXT.md` — same discipline as `/grill-with-docs` (see [CONTEXT-FORMAT.md](../grill-with-docs/CONTEXT-FORMAT.md)). Create the file lazily if it doesn't exist.
- **Sharpening a fuzzy term during the conversation?** Update `CONTEXT.md` right there.
- **User rejects the candidate with a load-bearing reason?** Offer an ADR, framed as: _"Want me to record this as an ADR so future architecture reviews don't re-suggest it?"_ Only offer when the reason would actually be needed by a future explorer to avoid re-suggesting the same thing — skip ephemeral reasons ("not worth it right now") and self-evident ones. See [ADR-FORMAT.md](../grill-with-docs/ADR-FORMAT.md).
- **Want to explore alternative designs for the refactored code?** See [INTERFACE-DESIGN.md](INTERFACE-DESIGN.md).
