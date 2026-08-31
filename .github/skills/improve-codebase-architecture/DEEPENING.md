# Refactoring a cluster of weak abstractions

How to merge and clean up a cluster of overly-indirected code safely, given its dependencies.

## Dependency categories

When assessing a candidate for refactoring, classify its dependencies. The category determines how the new, merged code gets tested.

### 1. In-process

Pure computation, in-memory state, no I/O. Always safe to merge — combine the pieces and test the result directly. No stand-in needed.

### 2. Local-substitutable

Dependencies that have local test stand-ins (PGLite for Postgres, in-memory filesystem). Safe to merge if the stand-in exists. Test with the stand-in running in the test suite. No public interface needed just for swapping test vs. production — that split can stay internal.

### 3. Remote but owned (yours, over a network)

Your own services across a network (microservices, internal APIs). Define an interface where the network call happens. The real logic owns the interface; the transport is injected as a separate implementation. Tests use an in-memory implementation. Production uses an HTTP/gRPC/queue implementation.

Recommendation shape: *"Define an interface where the network call happens, implement an HTTP version for production and an in-memory version for testing, so the logic lives in one place even though it's deployed across a network."*

### 4. True external (mock it)

Third-party services (Stripe, Twilio, etc.) you don't control. The refactored code takes the external dependency as an injected interface; tests provide a mock.

## When an interface is worth having

- **One implementation means it's probably just indirection. Two implementations means something real is varying.** Don't introduce an interface unless at least two implementations are justified (typically production + test). A single-implementation interface is just an extra file to read through.
- **Internal splits vs. the public interface.** Code can have implementation details split out for its own tests without exposing those splits as part of what callers see. Don't make an internal detail part of the public interface just because a test happens to use it directly.

## Testing strategy: replace, don't layer

- Old unit tests on the pieces you're merging become waste once tests against the new, merged code exist — delete them.
- Write new tests against the new code's public interface, not its internals.
- Tests should assert on what comes out, not on internal state.
- Tests should survive refactors — they should describe behavior, not implementation. If a test breaks every time the internals change, it's testing the wrong thing.
