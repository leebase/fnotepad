# Architecture & Development Notes

Understanding the internals of `fnotepad`. 

## 1. The Gap Buffer

Most text editors do not store text as a giant string, because inserting a character in the middle of a megabyte of text requires shifting half a million bytes in memory on every keystroke. 

Instead, `fnotepad` uses a **Gap Buffer** (the same model used by Emacs). 
* A massive fixed-size block of memory is allocated (64KB). 
* Two pointers are maintained: `gap-start` and `gap-end`.
* Text before the cursor sits at the beginning of memory up to `gap-start`.
* Text after the cursor sits at the end of memory down to `gap-end`.
* The "gap" is the empty space in between.

When you type a letter, it goes into `gap-start`, incrementing the pointer by 1. The operation is instant — O(1). 
When you move the cursor left, the character immediately behind `gap-start` is moved to `gap-end`, effectively "sliding" the gap backward.

## 2. Double Buffering (Virtual Console)

Writing to the terminal serially over `stdout` using ANSI escape codes is slow. If you clear the screen and redraw 500 characters on every keystroke, users on SSH connections will notice severe flickering.

To eradicate flicker, we decoupled the UI logic from the terminal pipeline.
* **VRAM (`vram` & `old-vram`):** Two 1920-cell (80x24) grids allocated in Forth memory. 
* **Draw Pass:** The Gap Buffer text, title bar, and menus are mathematically "drawn" into `vram` by updating bytes.
* **Render/Diff Pass:** `render-screen` sweeps over `vram` and compares it to `old-vram`. It *only* moves the physical terminal cursor and emits a character over `stdout` if that exact cell's character or style changed. 

This diff engine reduces a 1000-instruction frame down to 1 hardware instruction when typing a single letter.

## 3. Terminal Handling

To enable raw mode (reading keys without Enter, no echo), the editor uses `stty` system calls:

```forth
: enable-raw-mode  ( -- ) s" stty raw -echo" system ;
: disable-raw-mode ( -- ) s" stty sane"     system ;
```

While native termios via C FFI would be more efficient, `stty` provides maximum compatibility across Gforth versions.

## 4. Error Handling & Crash Safety

### Crash-Safe Terminal Restoration

The editor uses Forth's `catch`/`throw` exception mechanism to ensure terminal state is always restored:

```forth
: run-editor ( -- )
  ['] editor-loop catch
  cleanup-editor          \ Always runs, even on exception
  ...
;
```

This prevents the terminal from being left in raw mode if a stack underflow or other exception occurs.

### Status/Error Reporting

A status bar at the bottom of the screen displays user feedback:

| Error Code | Trigger | Message |
|------------|---------|---------|
| `ERR-FILENAME-TOO-LONG` | Filename > 255 chars | "Error: Filename too long (>255 chars)" |
| `ERR-BUFFER-FULL` | Insert/paste exceeds 64KB | "Error: Buffer full!" / "Error: Not enough space for paste!" |

Status messages are cleared on successful operations.

## Limitations

* **No Virtual Scrolling:** The visual layout maps directly to the absolute logical text buffer length. Files longer than the physical terminal height will either break parsing boundaries or fail to render the bottom. 
* **ANSI Assumptions:** We assume an 80x24 layout and generic VT100/ANSI sequences. 

## Future Ideas

If you want to contribute, here are great starting places for modifying the system:
1. **Vertical Offset Scrolling:** Add `scroll-y` and `scroll-x` tracking variables to decouple the viewport from physical memory arrays.
2. **Undo/Redo Stack:** Implement a generic ring-buffer storing recent `[index, action, byte]` operations.
3. **Regex Search:** A great way to push your Forth array-processing skills.
