\ fnotepad - A TUI Notepad Clone in Forth
\ Requires Gforth
\ 
\ This is a student-commented version. Comments emphasize stack effects 
\ and memory handling for readers coming from C/Python.

\ --- Constants ---
27 constant ESC  \ ASCII code for the Escape character

\ --- Terminal Raw Mode ---
\ We use stty to switch the Linux terminal out of line-buffered mode 
\ (where it waits for Enter) into raw mode (where it passes every key instantly).
: enable-raw-mode  ( -- ) 
  s" stty raw -echo" system ;

: disable-raw-mode ( -- ) 
  s" stty sane" system ;

\ --- Memory ---
\ Forth doesn't have malloc() out of the box in the same way C does.
\ We use `create` to name a dictionary entry, and `allot` to reserve bytes.
65536 constant MAX-BUFFER
create text-buffer    MAX-BUFFER allot  \ 64KB main text Gap Buffer
create clipboard-buf  MAX-BUFFER allot  \ 64KB secondary clipboard

\ --- Virtual Console (Double Buffering) ---
\ 80 columns x 24 rows = 1920 cells. Each cell contains 2 bytes:
\ Byte 0: Character ascii
\ Byte 1: Color/Style (0=normal, 1=highlighted menu, 2=highlighted text)
80 constant TERM-COLS
24 constant TERM-ROWS
TERM-COLS TERM-ROWS * 2 * constant VRAM-SIZE
create vram     VRAM-SIZE allot
create old-vram VRAM-SIZE allot

\ Variables hold memory addresses. Use `@` to get the value, `!` to set it.
variable gap-start       \ Pointer to the start of the gap
variable gap-end         \ Pointer to the end of the gap
variable needs-redraw    \ Flag: 1 = redraw requested, 0 = screen is fresh
variable sel-anchor      \ Logical text index where highlighting started
variable clipboard-len   \ Size of the copied text

\ VRAM writing coordinates
variable vrow
variable vcol
variable vstyle

\ Initialize the Gap Buffer and VRAM state
: init-buffer ( -- )
  \ Start the gap exactly at the beginning of our memory
  text-buffer gap-start !
  \ End the gap at the very end of our memory
  text-buffer MAX-BUFFER + gap-end !
  
  1 needs-redraw !
  -1 sel-anchor !          \ -1 means no active selection
  0  clipboard-len !
  vram VRAM-SIZE 0 fill
  old-vram VRAM-SIZE 255 fill \ force full redraw on first pass
;

\ --- File I/O ---
create filename-buf 256 allot
variable filename-len

\ Save the given string as the active filename
: set-filename ( c-addr u -- )
  dup 256 min filename-len !  \ Save up to 256 bytes of length
  filename-buf swap cmove     \ ( c-addr dest u ) Copy the characters
;

\ Retrieve the active filename
: get-filename ( -- c-addr u ) 
  filename-buf filename-len @ ;

\ Handle command line arguments (e.g., `gforth notepad.fs file.txt`)
: handle-args ( -- )
  next-arg dup 0> if
    2dup set-filename       \ ( c-addr u ) Store it
    r/w open-file 0= if     \ Try returning a file-id. 0 means success.
      >r                    \ Move file-id to return stack
      \ Read file into the text buffer
      text-buffer MAX-BUFFER r@ read-file throw
      \ Advance the gap start by how many bytes we read
      text-buffer + gap-start !
      r> close-file throw
    else 
      drop                  \ If open failed, drop the error code
    then
  else 
    2drop                   \ If no argument, drop string/length
  then
;

\ Save the main text buffer back to disk
: save-file ( -- )
  filename-len @ 0> if
    get-filename w/o create-file 0= if
      >r
      \ Write bytes *before* the gap
      text-buffer gap-start @ text-buffer - r@ write-file throw
      \ Write bytes *after* the gap
      gap-end @ text-buffer MAX-BUFFER + gap-end @ - r@ write-file throw
      r> close-file throw
    else 
      drop 
    then
  then
;

\ --- Gap Buffer Primitives ---
\ Calculate size of the gap
: gap-size     ( -- n )    
  gap-end @ gap-start @ - ;

\ Logical cursor index (how many real characters before the cursor)
: cursor-pos   ( -- n )    
  gap-start @ text-buffer - ;

\ Flag the engine to render a frame
: request-redraw ( -- ) 
  1 needs-redraw ! ;

\ Insert a character at the cursor
: insert-char ( c -- )
  gap-size 0> if            \ Ensure we have room
    gap-start @ c!          \ Store byte at gap-start
    1 gap-start +!          \ Advance gap-start by 1
    request-redraw
  else 
    drop                    \ Room full, discard character
  then
;

\ Delete the character behind the cursor (Backspace)
: delete-char ( -- )
  gap-start @ text-buffer > if   \ Ensure we aren't at the very beginning
    -1 gap-start +!              \ Rewind the gap-start by 1 (virtually erasing it)
    request-redraw
  then
;

\ Move the cursor (and the gap) left one character
: move-left ( -- )
  gap-start @ text-buffer > if
    -1 gap-start +!
    -1 gap-end +!
    \ Copy the char from left side of gap to right side of gap
    gap-start @ c@ gap-end @ c!
    request-redraw
  then
;

\ Move the cursor (and the gap) right one character
: move-right ( -- )
  gap-end @ text-buffer MAX-BUFFER + < if
    \ Copy from right side of gap to left side
    gap-end @ c@ gap-start @ c!
    1 gap-start +!
    1 gap-end +!
    request-redraw
  then
;

\ Find logical X coordinate of cursor
: current-col ( -- x )
  0                               \ ( x ) initial column
  gap-start @ text-buffer > if
    gap-start @ 1-                \ ( x addr ) start looking backwards
    begin
      dup text-buffer >= while    \ while addr >= start of buffer
      dup c@ 10 = if              \ if we hit a newline (10)
        drop exit                 \ drop addr and return x
      then
      swap 1+ swap 1-             \ ( x+1 addr-1 ) increment col, decrement addr
    repeat
    drop
  then
;

\ Move cursor up one physical line
: move-up ( -- )
  current-col >r                  \ save current col on return stack
  \ 1. Move left until we hit previous newline
  begin gap-start @ text-buffer > gap-start @ 1- c@ 10 <> and while move-left repeat
  gap-start @ text-buffer > if
    move-left                     \ jump over the newline itself
    \ 2. Move left to the beginning of THAT line
    begin gap-start @ text-buffer > gap-start @ 1- c@ 10 <> and while move-left repeat
    \ 3. Move right up to `x` times to restore column position
    r> 0 ?do gap-end @ text-buffer MAX-BUFFER + < if gap-end @ c@ 10 <> if move-right then then loop
  else 
    \ We were already on top line, just jump to column
    r> 0 ?do move-right loop 
  then
  request-redraw
;

\ Move cursor down one physical line
: move-down ( -- )
  current-col >r
  \ 1. Move right until next newline
  begin gap-end @ text-buffer MAX-BUFFER + < gap-end @ c@ 10 <> and while move-right repeat
  gap-end @ text-buffer MAX-BUFFER + < if
    move-right                    \ jump over the newline 
    \ 2. Move right up to `x` times
    r> 0 ?do gap-end @ text-buffer MAX-BUFFER + < if gap-end @ c@ 10 <> if move-right then then loop
  else 
    r> drop 
  then
  request-redraw
;

\ --- Selection Helpers ---

\ Reset the selection
: clear-sel  ( -- ) 
  -1 sel-anchor ! request-redraw ;

\ Is a selection active?
: sel-active? ( -- flag ) 
  sel-anchor @ -1 <> ;

\ Calculate earliest logical index of the selection
: sel-lower ( -- n ) 
  sel-anchor @ cursor-pos min ;

\ Calculate latest logical index of the selection
: sel-upper ( -- n ) 
  sel-anchor @ cursor-pos max ;

\ Check if logical index `n` is highlighted
: in-sel? ( n -- flag )
  sel-active? if 
    \ stack: n
    dup sel-lower >=          \ ( n flag-low )
    swap sel-upper <          \ ( flag-low flag-high )
    and                       \ ( flag-inside )
  else 
    drop 0 
  then
;

\ Movement wrappers that clear selection
: nav-move-left  ( -- ) clear-sel move-left ;
: nav-move-right ( -- ) clear-sel move-right ;
: nav-move-up    ( -- ) clear-sel move-up ;
: nav-move-down  ( -- ) clear-sel move-down ;

\ Movement wrappers that extend selection
: sel-extend-left  ( -- ) 
  sel-active? 0= if cursor-pos sel-anchor ! then move-left request-redraw ;
: sel-extend-right ( -- ) 
  sel-active? 0= if cursor-pos sel-anchor ! then move-right request-redraw ;
: sel-extend-up ( -- ) 
  sel-active? 0= if cursor-pos sel-anchor ! then move-up request-redraw ;
: sel-extend-down ( -- ) 
  sel-active? 0= if cursor-pos sel-anchor ! then move-down request-redraw ;

\ --- Clipboard ---
\ Copy highlighted text into the secondary buffer
: do-copy ( -- )
  sel-active? if
    sel-upper sel-lower - dup clipboard-len !
    0= if exit then               \ zero-length selection
    \ Loop over every character in the selection bounds
    sel-lower sel-upper over - 0 ?do
      over i +                    \ logic addr = lower + i
      dup gap-start @ text-buffer - < if 
        text-buffer +             \ physically before gap
      else 
        gap-end @ + gap-start @ text-buffer - - \ physically after gap
      then
      c@ clipboard-buf i + c!     \ fetch byte and write to clipboard
    loop
    drop clear-sel
  then
;

variable cut-lo   
variable cut-hi

\ Move gap pointer exactly to logical index `n`
: set-cursor-pos ( n -- )
  begin dup cursor-pos > gap-end @ text-buffer MAX-BUFFER + < and while move-right repeat
  begin dup cursor-pos < gap-start @ text-buffer > and while move-left repeat
  drop
;

\ Cut: Save bounds, copy to clipboard, move cursor, then delete
: do-cut ( -- )
  sel-active? if
    sel-lower cut-lo ! sel-upper cut-hi !
    cut-hi @ cut-lo @ - dup clipboard-len !
    0> if
      clipboard-len @ 0 ?do
        cut-lo @ i +
        dup gap-start @ text-buffer - < if text-buffer + else gap-end @ + gap-start @ text-buffer - - then
        c@ clipboard-buf i + c!
      loop
    else drop then
    clear-sel
    cut-hi @ set-cursor-pos       \ physically move the gap to cover the selection
    clipboard-len @ 0 ?do delete-char loop
    request-redraw
  then
;

\ Paste clipboard at cursor
: do-paste ( -- )
  clipboard-len @ 0> if
    clipboard-len @ 0 ?do clipboard-buf i + c@ insert-char loop
  then
;

\ Select all text in buffer
: do-select-all ( -- )
  0 sel-anchor !
  begin gap-end @ text-buffer MAX-BUFFER + < while
    gap-end @ c@ gap-start @ c! 1 gap-start +! 1 gap-end +!
  repeat
  request-redraw
;

\ --- Virtual Console Engine ---
\ Terminal escape sequence wrapper
: ansi-start   ( -- ) ESC emit [char] [ emit ;

\ Move physical terminal cursor to Row/Col
: move-cursor  ( r c -- ) ansi-start swap 0 .r ." ;" 0 .r ." H" ;

\ Wipe entire terminal canvas
: clear-screen ( -- ) ansi-start ." 2J" ansi-start ." H" ;

\ Blank out our virtual memory representation
: clear-vram ( -- )
  0 vstyle !
  VRAM-SIZE 0 ?do
    32 vram i + c!          \ space character
    0  vram i + 1+ c!       \ style normal
  2 +loop
;

\ Point our VRAM drawing "pointer" to a row/col
: vgotoyx ( r c -- ) vcol ! vrow ! ;

\ Write a single character (+ current style) to VRAM
: vputc ( c -- )
  vrow @ TERM-ROWS < vcol @ TERM-COLS < and if
    \ offset = (row * cols + col) * 2
    vrow @ TERM-COLS * vcol @ + 2 *
    vram over +      \ ( c offset vaddr )
    rot over c!      \ ( offset vaddr ) write c to vaddr
    vstyle @ swap 1+ c! \ write style to vaddr+1
    drop             \ drop offset
  else drop then
  1 vcol +!
;

\ Fill remainder of current VRAM row with spaces
: vclear-eol ( -- )
  begin vcol @ TERM-COLS < while 32 vputc repeat
;

\ Print a Forth string to VRAM natively
: vprint ( c-addr u -- )
  0 ?do dup c@ vputc 1+ loop drop
;

\ State variables for the diff renderer
variable cur-style
variable roffset
variable vchar
variable vsty
variable ochar
variable osty

\ Diff `vram` vs `old-vram`. If they match, do nothing (Zero Flicker!)
\ If they differ, move hardware cursor and emit the change.
: render-screen ( -- )
  ansi-start ." ?25l"          \ Hide cursor to prevent jumping
  0 cur-style !                \ Track active terminal ANSI style
  
  TERM-ROWS 0 ?do
    TERM-COLS 0 ?do
      j TERM-COLS * i + 2 * roffset !
      
      vram roffset @ + c@       vchar !
      vram roffset @ + 1+ c@    vsty !
      old-vram roffset @ + c@      ochar !
      old-vram roffset @ + 1+ c@   osty !
      
      \ Did char or style change?
      vchar @ ochar @ <>  vsty @ osty @ <>  or if
        \ Force hardware cursor exactly here
        j 1+ i 1+ move-cursor
        
        \ Inject ANSI commands only if style changed
        vsty @ cur-style @ <> if
          vsty @ case
            0 of ansi-start ." 0m"      endof
            1 of ansi-start ." 47;30m"  endof
            2 of ansi-start ." 7m"      endof
          endcase
          vsty @ cur-style !
        then
        
        vchar @ emit
        
        \ Save to history buffer
        vchar @ old-vram roffset @ + c!
        vsty @  old-vram roffset @ + 1+ c!
      then
    loop
  loop
  ansi-start ." 0m"
  ansi-start ." ?25h"          \ Show cursor again
;

\ --- UI Drawing (VRAM Pass) ---
: draw-title-bar ( -- )
  0 0 vgotoyx
  1 vstyle ! \ Set 1 (grey highlight) for menus
  s"  fNotepad - " vprint
  filename-len @ 0> if get-filename vprint else s" Untitled " vprint then
  vclear-eol
;

: draw-menu-bar ( -- )
  1 0 vgotoyx
  1 vstyle ! 
  s"  File (^S) | Edit: Shift+Arrows Sel, ^C/^X/^V | ^Q Quit " vprint
  vclear-eol
;

\ Draw 1 character. Handle hardware newlines by shifting VRAM rows
: draw-char ( c -- )
  dup 10 = if
    vclear-eol     \ clear remainder of line
    1 vrow +! 0 vcol ! \ carriage return
    drop
  else
    vputc
  then
;

\ Plot gap buffer characters into VRAM map
: draw-text ( -- )
  2 0 vgotoyx
  0 vstyle !

  gap-start @ text-buffer > if
    gap-start @ text-buffer ?do
      \ Logical index `i - text-buffer` determines selection
      i text-buffer - in-sel? if 2 vstyle ! else 0 vstyle ! then
      i c@ draw-char
    loop
  then

  gap-end @ text-buffer MAX-BUFFER + < if
    text-buffer MAX-BUFFER + gap-end @ ?do
      cursor-pos i gap-end @ - + in-sel? if 2 vstyle ! else 0 vstyle ! then
      i c@ draw-char
    loop
  then
  
  0 vstyle !
  \ Clear empty space
  begin vrow @ TERM-ROWS < while 
    vclear-eol
    1 vrow +! 0 vcol !
  repeat
;

\ Find exact 1-indexed R,C for hardware cursor placement
: sync-real-cursor ( -- )
  3 1 
  cursor-pos 0 ?do
    i gap-start @ text-buffer - < if text-buffer i + else gap-end @ i + gap-start @ text-buffer - - then
    c@ 10 = if
      drop 1 swap 1+ swap
    else
      1+
    then
  loop
  move-cursor
;

\ --- Key Parsing ---
\ Decode long ANSI sequences for arrows and shift-modifications
: parse-escape ( -- )
  key
  [char] [ = if
    key
    case
      [char] A of nav-move-up    endof
      [char] B of nav-move-down  endof
      [char] C of nav-move-right endof
      [char] D of nav-move-left  endof
      [char] 1 of  \ Shift+Arrow e.g. ESC [ 1 ; 2 A
        key drop key drop key
        case
          [char] A of sel-extend-up    endof
          [char] B of sel-extend-down  endof
          [char] C of sel-extend-right endof
          [char] D of sel-extend-left  endof
        endcase
      endof
    endcase
  then
;

\ Master application loop
: run-editor ( -- )
  init-buffer handle-args enable-raw-mode
  clear-screen
  
  begin
    needs-redraw @ 1 = if
      clear-vram
      draw-title-bar
      draw-menu-bar
      draw-text
      render-screen     
      sync-real-cursor  
      0 needs-redraw !
    then

    key

    dup 17 = if drop 1 else   \ Ctrl+Q
    dup 19 = if save-file drop 0 else   
    dup 27 = if drop parse-escape 0 else   
    dup  1 = if do-select-all drop 0 else   
    dup  3 = if do-copy  drop 0 else   
    dup 24 = if do-cut   drop 0 else   
    dup 22 = if do-paste drop 0 else   
    dup 13 = over 10 = or if drop 10 insert-char 0 else   
    dup 127 = over 8 = or if drop delete-char 0 else   
    dup 32 >= over 126 <= and if insert-char 0 else   
    drop 0   
    then then then then then then then then then then
  until \ Loop until top parameter is 1 (Ctrl+Q hit)

  disable-raw-mode clear-screen
;

run-editor
bye
