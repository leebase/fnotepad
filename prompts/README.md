# Prompt Examples

To showcase the "Antigravity" workflow, here are annotated examples of prompts we used to steer the AI while building `fnotepad` in an unfamiliar language.

## 1. Defining Constraints (Initial Architecture Request)

> "Provide the starting code for a terminal-based Notepad clone written in standard Gforth. We will use a Gap Buffer for memory. Your first step should simply set up the boilerplate, write the ANSI commands to clear the screen, draw a fake 80-col menu bar at the top, and put the Linux terminal into raw Mode so we can intercept keystrokes in a main `begin...while` loop."

**Why it worked:** It isolates the initial work. It demands specific architectural compliance (Gap Buffer, Raw mode, standard Gforth) rather than asking the AI "how to make a notepad". 

## 2. Pushing Back on Complexity

> "This `draw-text` logic is causing the screen to flicker on my SSH terminal. I suspect emitting ANSI codes per-character over serial is too slow. Redesign the engine to use a Virtual Screen approach format. Allocate an array, draw text into it logically, and then 'diff' it against an `old-array` structure to send minimal updates."

**Why it worked:** When we hit a performance limitation, we told the AI exactly *what* system paradigm to use (Diff engine Double Buffering), relying on the AI to figure out *how* to write the arrays in Forth syntax.

## 3. Triage Using Compiler Errors

> "The Double Buffering compile crashed with the following error:
> `notepad.fs:445: Stack underflow`
> `>>>run-editor<<<`
> `Backtrace:`
> `$7C57B447B4E0 pick`
> `...render-screen` 
> I suspect your `render-screen` logic is failing to track stack depths across the internal `loop` logic. Please refactor."

**Why it worked:** Dropping raw compiler backtraces into the prompt forces the AI into a heavily analytical state. By pointing directly at the `pick` function in `render-screen`, we prevented the AI from getting confused and randomly tweaking other parts of the application. 

## 4. Forcing Safe Syntax 

> "Your stack math for VRAM diffing keeps underflowing. Stop trying to juggle 5 variables using `pick` and `roll` inside the nested loops. Set up dedicated `variable` entries outside the loop and use `!` and `@` to update states safely without touching the data stack."

**Why it worked:** An experienced engineer recognizes when stack-juggling has become too deep. Forcing the AI to retreat to standard variables solved the crash immediately.
