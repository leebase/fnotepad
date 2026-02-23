# fnotepad

A hardware-accelerated, double-buffered text editor written entirely in Forth. 

Inspired by Windows Notepad, built for the Linux Terminal, and engineered from scratch without prior knowledge of the language using the **Antigravity Programming** workflow.

![fnotepad demo placeholder](assets/demo.gif)

## What is Antigravity Programming?

We believe language syntax is no longer a barrier to entry. **Antigravity Programming** is the concept of leveraging AI as a Senior Staff Engineering partner to drastically reduce the "gravity well" of learning a new technology stack. 

This editor was not built *after* mastering Forth. It was built *during* the learning phase. By providing robust architectural constraints to the AI and letting it handle the syntax translation, you can build complex systems in unfamiliar domains on day one. 

Read more about this paradigm shift in [docs/antigravity.md](docs/antigravity.md).

## Why Forth?

Forth is not just a language; it's a philosophy. It offers zero abstraction between you and the memory you control. Operating without native functions, local variables, or standard IDE tooling forces you to construct a flawless mental model of the stack. It is the ultimate exercise in systems engineering minimalism.

## Features

- **Double-Buffered Rendering:** A custom "Virtual Console" (VRAM) diff engine eliminates terminal flicker by only emitting ANSI codes for changed characters.
- **Gap Buffer Architecture:** Memory management mimics Emacs, allowing O(1) text insertions and deletions at the cursor.
- **Notepad-Style UX:** 4-way arrow navigation, Shift+Arrow highlighting, Ctrl+C/X/V clipboard operations, and Ctrl+S saving.
- **Zero Dependencies:** Written in standard ANS Forth.

## Quick Start
### 1. Install gforth
```bash
sudo apt-get install gforth
```

### 2. Run the Editor
```bash
git clone https://github.com/yourusername/fnotepad.git
cd fnotepad
gforth src/notepad.fs [optional_filename.txt]
```

## Learning Paths

We designed this repository to be a playground for curious engineers. Choose your journey:

* **Learn Forth:** Check out our [Forth for Experienced Programmers](tutorials/forth_tutorial.md) guide and read the heavily annotated [src/notepad_commented.fs](src/notepad_commented.fs) file.
* **Learn Antigravity:** Read [docs/antigravity.md](docs/antigravity.md) to understand the AI-collaboration workflow, then check out [prompts/README.md](prompts/README.md) to see exactly how we prompted the AI.
* **Explore the Architecture:** Dive straight into [docs/development.md](docs/development.md) for a technical breakdown of the double-buffer and gap-buffer implementations.

## Repository Layout
```
/src        - Core editor source code (`notepad.fs`)
/docs       - Architecture, Getting Started, and Philosophy documentation
/tutorials  - Deep dives into Forth and Antigravity paradigms
/prompts    - Real transcripts of AI prompts used to construct this repo
/assets     - Images and media
```

## Philosophy

AI assistance does not eliminate the need for engineering rigor—it amplifies it. The AI can write standard algorithms in seconds, but as the orchestrator, you must still enforce architectural constraints, verify performance, and triage low-level stack crashes. We do not trust the AI; we trust the compiler.

## Contributing

We welcome pull requests for bug fixes, tutorials, and Forth refactoring! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and review process.

## License
MIT License. See [LICENSE](LICENSE) for details.
