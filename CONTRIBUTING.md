# Contributing to fnotepad

First, thank you for your interest! We built `fnotepad` not just as a text editor, but as a living case study for the **Antigravity Programming** workflow.

Contributions are enthusiastically welcomed, regardless of whether you are a 30-year Forth veteran or a Python engineer who just learned Forth today using an AI pair-programmer.

## How to Contribute

We manage development through standard GitHub pull requests.

### 1. Identify a Need
Check our `docs/development.md` file for an active list of "Future Ideas" (like an Undo/Redo stack or Regex Search capabilities). You can also dive into the issue tracker.

### 2. The Antigravity Approach
If you are new to Forth, we *encourage* you to use an AI (like Claude, Gemini, or ChatGPT) to help write your feature! 

If you do, please include the prompts you used in the PR description or add them to the `/prompts` directory. We consider the documentation of *how* the AI was steered just as valuable as the code itself.

### 3. Engineering Rigor
AI assistance is strongly encouraged, but blind AI implementation will be rejected. 
As the human orchestrator, you must:
* Verify the architectural soundness of the logic.
* Ensure no Deep Stack abuse (`pick` / `roll` chains).
* Test the edge cases manually in `gforth`.

## Coding Style Expectations

* **Forth Standard:** Adhere to ANS Forth definitions where possible.
* **Keep Words Small:** If a definition (`: ... ;`) exceeds 10 lines of logic, break it up into helper words.
* **Stack Comments Required:** Every single word you create must include a stack effect comment: `( input -- output )`.
* **Mind your Variables:** Do not juggle deep parameters on the data stack. If you need temporary state across loops, declare explicit `variable` entries. Memory is cheap; stack underflows are expensive.

## Reporting Issues

If you find a bug (especially visual glitches or rendering bleed), open an Issue. Please include:
* OS Version and Terminal Emulator used.
* The exact keystroke sequence to reproduce the error.
* If a Forth backtrace was printed, paste the full log!

## Getting Started Fast
1. Fork the repo.
2. Clone your copy.
3. Read `tutorials/forth_tutorial.md`.
4. Run `gforth src/notepad.fs`.
5. Break it. Fix it.
6. Open your PR!
