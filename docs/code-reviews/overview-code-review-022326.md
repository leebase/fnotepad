# 🧾 Pragmatic Code Review Report

## A) Executive Summary

The core implementation is **compact and conceptually clear**: the gap-buffer and diff-based VRAM rendering are cleanly centralized in one file, which makes core behavior easy to trace.

The rendering path is **performance-aware** (double buffering and diff writes), which is a strong architectural choice for terminal UX.

The documentation does a good job explaining architecture and intended contribution style, lowering onboarding friction for contributors.

### Highest-Leverage Issues to Fix First

- 🔴 **High Risk** — Terminal raw mode restoration is not fail-safe; exceptions (`throw`) can bypass cleanup and leave the terminal broken.
- 🔴 **High Risk** — Long filenames are silently truncated to 256 bytes and reused for the write path, which can save to an unintended file.
- 🟠 **Medium Risk** — Opening files larger than `MAX-BUFFER` truncates content with no user-visible warning.
- 🟠 **Medium Risk** — Insert/paste overflow is silently dropped when the gap is full, causing implicit data-loss behavior.
- 🟡 **Medium Maintainability** — `run-editor` input dispatch is a deep `dup ... if ... then` chain that is hard to extend safely.
- 🟡 **Low/Medium Safety + DX** — Helper scripts write to absolute local paths (`/home/lee/...`), which is risky and non-portable.
- 🟡 **Low Consistency** — README claims “Zero Dependencies,” but implementation shells out to `stty` via `system`; docs and runtime assumptions are misaligned.

---

## B) Priority Fix List (Ranked)

### 1️⃣ Crash-Safe Terminal Restoration

**Severity:** High  
**Type:** Correctness / Safety  
**Where:** `src/notepad.fs` (`enable-raw-mode`, `disable-raw-mode`, `run-editor`, file I/O words using `throw`)  

**Why it matters:**  
Any thrown error while in raw mode can skip cleanup and leave the user terminal unusable.

**Suggested Fix (Minimal):**  
Wrap the editor loop in a guard word that always calls `disable-raw-mode` before rethrowing or exiting. Avoid direct `throw` inside the hot path unless the caller guarantees restoration.

**Verification:**  
Trigger a failing open/save path and confirm terminal echo is restored automatically (no manual `stty sane`).

---

### 2️⃣ Stop Silent Filename Truncation

**Severity:** High  
**Type:** Correctness / Safety  
**Where:** `src/notepad.fs` (`set-filename`, `save-file`)  

**Why it matters:**  
Silent truncation can redirect writes to an unintended filename.

**Suggested Fix:**  
Reject overlong paths early (status message / no-op save) instead of truncating. Keep original length and compare against max before `cmove`.

**Verification:**  
Pass a filename >255 bytes and confirm save is blocked with an explicit message.

---

### 3️⃣ Detect Truncated File Loads

**Severity:** Medium  
**Type:** Correctness  
**Where:** `src/notepad.fs` (`handle-args`)  

**Why it matters:**  
Files larger than 64 KB are truncated silently; user may later overwrite the full file with partial contents.

**Suggested Fix:**  
Detect “buffer full during read,” mark buffer as truncated, and surface a warning in the title/status line. Block blind save unless acknowledged.

**Verification:**  
Open a file >64 KB and confirm a warning appears; ensure save requires confirmation.

---

### 4️⃣ Buffer Overflow Feedback on Insert/Paste

**Severity:** Medium  
**Type:** Correctness / UX  
**Where:** `src/notepad.fs` (`insert-char`, `do-paste`)  

**Why it matters:**  
Overflow silently drops input/paste bytes.

**Suggested Fix:**  
Make `insert-char` return a success flag; aggregate in paste and show “buffer full” once.

**Verification:**  
Fill buffer, paste more text, confirm visible overflow signal and deterministic behavior.

---

### 5️⃣ Input Dispatch Maintainability

**Severity:** Medium  
**Type:** Architecture / Maintainability  
**Where:** `src/notepad.fs` (`run-editor`)  

**Why it matters:**  
Deep conditional chains are brittle when adding keys/features.

**Suggested Fix:**  
Extract `handle-key ( ch -- quit? )` with small words per key class. No behavior rewrite required.

**Verification:**  
Existing key behavior unchanged; add one new key binding with minimal diff.

---

### 6️⃣ Helper Script Safety & Portability

**Severity:** Low-Medium  
**Type:** DX / Safety  
**Where:** `rewrite.sh`, `format_script2.sh`  

**Why it matters:**  
Scripts can overwrite unexpected local files and are hard to use cross-machine.

**Suggested Fix:**  
Use repo-relative paths and guard with:

```bash
set -euo pipefail

Verification:
Run scripts from repo root and confirm only intended files are modified.

⸻

C) If You Only Do One Thing

Implement crash-safe terminal restoration first.

Always call disable-raw-mode on every exit path, including exceptions.

This yields the largest reliability and safety gain per line of code and prevents the most disruptive failure mode for users.

⸻

D) Patch Suggestions (Top 3)

1️⃣ Crash-Safe Raw Mode Cleanup

Before

: run-editor ( -- )
  init-buffer handle-args enable-raw-mode
  clear-screen
  ... main loop ...
  disable-raw-mode clear-screen
;

After (Incremental)

: editor-loop ( -- )
  clear-screen
  begin
    ... existing loop body ...
  until ;

: run-editor-safe ( -- )
  init-buffer handle-args
  enable-raw-mode
  ['] editor-loop catch
  disable-raw-mode clear-screen
  ?dup if throw then ;


⸻

2️⃣ Stop Silent Filename Truncation

Before

: set-filename ( c-addr u -- )
  dup 256 min filename-len !
  filename-buf swap cmove
;

After (Minimal)

variable filename-too-long

: set-filename ( c-addr u -- )
  dup 256 > filename-too-long !
  dup 256 min filename-len !
  filename-buf swap cmove ;

: save-file ( -- )
  filename-too-long @ if exit then
  ... existing save logic ... ;


⸻

3️⃣ Detect / Trap Truncated File Load

Before

text-buffer MAX-BUFFER r@ read-file throw
text-buffer + gap-start !

After (Minimal)

variable file-truncated
...
text-buffer MAX-BUFFER r@ read-file throw dup
MAX-BUFFER = file-truncated !
text-buffer + gap-start !

Then display a one-line warning when file-truncated @ is true.

⸻

E) Follow-Up Questions
	1.	Do you want files larger than 64 KB to be blocked, truncated with warning, or should dynamic growth be prioritized next?
	2.	What UX surface is preferred for errors/warnings (status bar, transient message, stderr)?
	3.	Are helper scripts intended for maintainers only, or should they be contributor-safe by default?
	4.	Should the first hardening PR focus only on raw-mode crash safety, or combine with filename/truncation warnings?
	5.	Is Linux-only an explicit constraint, or should macOS portability be considered?

⸻

Reviewed Scope
	•	Core editor (src/notepad.fs)
	•	Main documentation
	•	Root helper scripts

Dynamic runtime interaction testing was not performed due to the interactive nature of run-editor.

Assumption: The placeholder URL indicated the currently checked-out local repository.

⸻

Summary of Actions

Added a repository-local review artifact with prioritized findings and PR-sized follow-ups:

docs/code-review.md


⸻

Testing Evidence

pwd && rg --files | head
rg --files
sed -n '1,220p' src/notepad.fs
sed -n '260,520p' src/notepad.fs
sed -n '1,220p' docs/development.md
sed -n '1,220p' CONTRIBUTING.md
sed -n '1,220p' docs/getting-started.md
nl -ba src/notepad.fs | sed -n '1,260p'
nl -ba src/notepad.fs | sed -n '260,520p'
nl -ba rewrite.sh | sed -n '1,120p'
nl -ba format_script.sh | sed -n '1,80p'
nl -ba format_script2.sh | sed -n '1,80p'
nl -ba README.md | sed -n '1,180p'
nl -ba docs/getting-started.md | sed -n '1,200p'
git status --short
nl -ba docs/code-review.md | sed -n '1,220p'
git add docs/code-review.md
git commit -m "docs: add pragmatic code review notes"
