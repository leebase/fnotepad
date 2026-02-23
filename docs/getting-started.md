# Getting Started with fnotepad

Welcome! Getting this editor running locally is extremely lightweight.

## Prerequisites

The only hard dependency is **gforth**, the GNU Forth interpreter.

### Linux / WSL
```bash
sudo apt-get update
sudo apt-get install gforth
```

### macOS
```bash
brew install gforth
```

## Running the Editor

To start with an empty, untitled buffer:
```bash
gforth src/notepad.fs
```

To open an existing file or create a named file:
```bash
gforth src/notepad.fs myfile.txt
```

## Controls

* **Arrows:** Move cursor
* **Shift + Arrows:** Highlight text (Visual Selection)
* **Ctrl + C:** Copy selection
* **Ctrl + X:** Cut selection
* **Ctrl + V:** Paste from clipboard
* **Ctrl + A:** Select all
* **Ctrl + S:** Save to disk
* **Ctrl + Q:** Quit editor

*(Note: Ensure your terminal emulator intercepts standard Ctrl keys; some combinations may overlap with your OS window manager).*

## Troubleshooting

### "Invalid Memory Address" or "Stack Underflow"
If you encounter deep Forth crashes while editing, verify you are using `gforth` 0.7.3 or above. Different Forth dialects handle memory and stack pointer operations differently; this editor assumes standard `gforth` layout.

### Screen Garbled / Key Mismatches
The application places your Unix terminal into "raw mode". If `fnotepad` crashes abruptly, your terminal may be left without local echo (typing will be invisible). To fix this, blindly type:
```bash
stty sane
```
and hit enter.

## What's Next?
* If you want to understand how the code works, read through [`src/notepad_commented.fs`](../src/notepad_commented.fs).
* If you want to tweak the Double Buffer rendering, see [`docs/development.md`](development.md).
