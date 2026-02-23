# Forth for Experienced Programmers — Enough to Understand the Editor

If you're reading this, you probably know C, Python, Java, or Rust. You understand memory allocation, pointers, and control execution. But reading Forth feels like reading alien hieroglyphics.

This tutorial bridges the gap. We are going to cover exactly what you need to know about Forth to understand how our hardware-accelerated TUI Notepad clone works. 

---

## 1. What Makes Forth Different

Forth is not a language; it is an environment and a philosophy. 
There are no functions, only **Words**. There are no function arguments like `foo(1, 2)`, there is only the **Data Stack**.

Everything you type in Forth is space-delimited text. The interpreter reads a word, executes it, and moves to the next.
* `5` puts the number 5 on the stack.
* `+` pops the top two numbers off the stack, adds them, and pushes the result.

```forth
3 4 + .
```
> In English: Push 3. Push 4. Pop them, add them, push 7. The `.` word means "pop the top value and print it". Output: `7`.

## 2. Installing and Running (gforth)

To follow along or run the editor, you need `gforth`.
```bash
sudo apt install gforth
```
You can run a script directly: `gforth my_script.fs`
Or you can drop into the REPL by just typing `gforth`.

## 3. The Stack Model

When reading Forth, you must maintain a mental model of the stack. Because words don't have named parameters, standard practice dictates writing a "Stack Effect Comment" next to the definition.

`( before -- after )`

If a word takes two integers and returns one, its comment is:
`( n1 n2 -- sum )`

The right-most item is the top of the stack.

## 4. Core Stack Words

Because you don't have local variables by default, you spend a lot of time juggling the top 3 items on the stack to get them into the right order for the next Word.

* `dup` : `( a -- a a )` duplicates the top item.
* `drop`: `( a -- )` throws away the top item.
* `swap`: `( a b -- b a )` swaps the top two items.
* `over`: `( a b -- a b a )` copies the second item to the top.
* `rot`:  `( a b c -- b c a )` rotates the third item to the top.

## 5. Defining Words

You define a new word using a colon `:` and end it with a semicolon `;`.

```forth
: square ( n -- n^2 )
  dup * 
;

5 square . \ prints 25
```

## 6. Control Flow

Forth uses Postfix IF/ELSE conditionals. The condition is evaluated *before* the IF keyword, which simply checks if the top of the stack is true (non-zero) or false (0).

```forth
: is-even? ( n -- )
  2 mod 0 = if
    ." It is even!" 
  else
    ." It is odd!"
  then
;
```

**Loops**
A `do ... loop` takes its limits from the stack in `( limit start -- )` format.

```forth
: count-to-five ( -- )
  6 1 do
    i .
  loop
;
```
*(The word `i` pushes the current loop index).*

For infinite or conditional loops, use `begin ... while ... repeat`.

## 7. Memory and Buffers

Forth gives you direct, low-level access to memory. 

* `create` makes a named pointer.
* `allot` reserves X bytes of memory.
* `variable` creates a named pointer holding exactly 1 cell (usually 64-bits).

To write to memory, use `!` (store 64-bit) or `c!` (store character / 1-byte).
To read from memory, use `@` (fetch 64-bit) or `c@` (fetch character / 1-byte).

```forth
variable score
100 score !    \ Set score to 100
score @ 10 +   \ Fetch score, add 10
score !        \ Store 110 back
```

In the editor, we allocate 64KB for a Gap Buffer like this:
```forth
create text-buffer 65536 allot
```

## 8. File I/O Basics

File handles in Forth are standard identifiers. 

```forth
\ Open a file
s" file.txt" r/w open-file 

\ Write to it
>r                    \ save file handle to return stack temporarily
s" Hello data" r@ write-file 

\ Close it
r> close-file 
```

## 9. Structuring Larger Programs in Forth

Forth programs read from bottom to top. You must define a word before you can use it. The last line of a Forth program is usually the invocation of the main application loop.

You will see the editor structured sequentially:
1. Constants and Settings
2. Buffer and Memory initialization
3. Movement Primitives (navigating the Gap Buffer)
4. Screen Drawing calculations 
5. Master Input Loop

## 10. Reading the Editor Code

Let's look at one critical word from the Editor: inserting a character.

```forth
: insert-char ( c -- )
  gap-size 0> if            
    gap-start @ c!          \ Store the ascii byte exactly at the gap-start pointer
    1 gap-start +!          \ Increment the gap-start pointer by 1
    request-redraw          \ Flag the screen engine to update
  else 
    drop                    \ If gap-size is 0, we are out of memory. Drop the char.
  then
;
```

By keeping Forth words small (1-5 lines max), the mental overhead of tracking stack state is minimized. If a word gets too large, you should refactor it into smaller helper words.
