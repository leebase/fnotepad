# Pragmatic Code Review Notes

Date: 2026-02-23

This note captures a high-signal pass over `fnotepad` focused on security, correctness, and maintainability.

## Top Risks

1. **Terminal mode restoration is not crash-safe**
   - The editor enables raw mode via `stty raw -echo` and only restores it on normal loop exit.
   - Any thrown exception before the final cleanup can leave the terminal unusable until manual `stty sane`.

2. **Unbounded filename truncation can silently redirect writes**
   - Filenames are copied into a fixed 256-byte buffer and truncated.
   - Save operations use the truncated name with no warning.

3. **No overflow signal on full gap buffer or paste overflow**
   - Inserts/pastes past capacity are dropped silently.
   - This creates correctness and UX ambiguity (data appears accepted but is not stored).

4. **Runtime shell dependency and platform coupling**
   - Terminal control relies on spawning `stty` through `system`.
   - This introduces shell/process dependency and weakens portability despite README "Zero Dependencies" messaging.

5. **Potential committed binary artifact risk**
   - `libtermios_helper.so` is present in repository root.
   - Committed binaries increase supply-chain review burden and can drift from source.

## Suggested PR-sized Follow-ups

1. Add a guarded top-level runner that always restores terminal state on failure.
2. Reject/alert on filename >255 bytes instead of truncating silently.
3. Add a user-visible status/error path for buffer-full inserts and partial paste.
4. Replace `stty` shell calls with C FFI already prototyped in repo, and update README dependency wording.
5. Remove committed `.so` artifact and build it from source in docs/script if still needed.

## Scope Reviewed

- Core editor implementation in `src/notepad.fs`
- Setup and architecture docs (`README.md`, `docs/*.md`, `CONTRIBUTING.md`)
- Helper shell scripts in repo root
