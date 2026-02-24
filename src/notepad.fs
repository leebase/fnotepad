\ fnotepad - A TUI Notepad Clone in Forth
\ Requires Gforth

\ --- Constants ---
27 constant ESC
256 constant MAX-FILENAME-LEN
65536 constant MAX-BUFFER

\ --- Error Status Codes ---
0 constant ERR-NONE
1 constant ERR-FILENAME-TOO-LONG
2 constant ERR-BUFFER-FULL

\ Status message buffer
variable last-err-code
create status-msg 256 allot
variable status-msg-len

: set-status ( c-addr u -- )
  dup 255 min status-msg-len !
  status-msg swap cmove
;

: clear-status ( -- )
  s" " set-status
  ERR-NONE last-err-code !
;

\ --- Terminal Raw Mode via C FFI ---
library libtermios libtermios_helper.so
libtermios helper-enable enable_raw_mode
libtermios helper-disable disable_raw_mode

: enable-raw-mode  ( -- ) helper-enable drop ;
: disable-raw-mode ( -- ) helper-disable drop ;

\ --- Memory ---
create text-buffer    MAX-BUFFER allot
create clipboard-buf  MAX-BUFFER allot

\ --- Virtual Console (Double Buffering) ---
\ 80 columns x 24 rows = 1920 cells. Each cell contains 2 bytes:
\ Byte 0: Character ascii
\ Byte 1: Color/Style (0=normal, 1=highlighted menu, 2=highlighted text)
80 constant TERM-COLS
24 constant TERM-ROWS
TERM-COLS TERM-ROWS * 2 * constant VRAM-SIZE
create vram     VRAM-SIZE allot
create old-vram VRAM-SIZE allot

variable gap-start
variable gap-end
variable needs-redraw
variable sel-anchor
variable clipboard-len

\ VRAM writing coordinates
variable vrow
variable vcol
variable vstyle

: init-buffer ( -- )
  text-buffer gap-start !
  text-buffer MAX-BUFFER + gap-end !
  1 needs-redraw !
  -1 sel-anchor !
  0  clipboard-len !
  clear-status
  vram VRAM-SIZE 0 fill
  old-vram VRAM-SIZE 255 fill \ force full redraw on first pass
;

\ --- File I/O ---
create filename-buf MAX-FILENAME-LEN allot
variable filename-len

: set-filename ( c-addr u -- flag )
  \ Returns true on success, false if filename too long
  dup MAX-FILENAME-LEN >= if
    2drop
    ERR-FILENAME-TOO-LONG last-err-code !
    s" Error: Filename too long (>255 chars)" set-status
    0 exit
  then
  dup filename-len !
  filename-buf swap cmove
  ERR-NONE last-err-code !
  1
;

: get-filename ( -- c-addr u ) filename-buf filename-len @ ;

: handle-args ( -- )
  next-arg dup 0> if
    2dup set-filename if
      r/w open-file 0= if
        >r
        text-buffer MAX-BUFFER r@ read-file throw
        text-buffer + gap-start !
        r> close-file throw
      else drop then
    else 2drop then
  else 2drop then
;

: save-file ( -- )
  filename-len @ 0> if
    get-filename w/o create-file 0= if
      >r
      text-buffer gap-start @ text-buffer - r@ write-file throw
      gap-end @ text-buffer MAX-BUFFER + gap-end @ - r@ write-file throw
      r> close-file throw
      s" File saved." set-status
    else drop then
  then
;

\ --- Gap Buffer Primitives ---
: gap-size     ( -- n )    gap-end @ gap-start @ - ;
: cursor-pos   ( -- n )    gap-start @ text-buffer - ;

: request-redraw ( -- ) 1 needs-redraw ! ;

: insert-char ( c -- flag )
  \ Returns true if inserted, false if buffer full
  gap-size 0> if
    gap-start @ c!
    1 gap-start +!
    request-redraw
    ERR-NONE last-err-code !
    1
  else
    drop
    ERR-BUFFER-FULL last-err-code !
    s" Error: Buffer full!" set-status
    0
  then
;

: delete-char ( -- )
  gap-start @ text-buffer > if
    -1 gap-start +!
    request-redraw
  then
;

: move-left ( -- )
  gap-start @ text-buffer > if
    -1 gap-start +!
    -1 gap-end +!
    gap-start @ c@ gap-end @ c!
    request-redraw
  then
;

: move-right ( -- )
  gap-end @ text-buffer MAX-BUFFER + < if
    gap-end @ c@ gap-start @ c!
    1 gap-start +!
    1 gap-end +!
    request-redraw
  then
;

: current-col ( -- x )
  0
  gap-start @ text-buffer > if
    gap-start @ 1-
    begin
      dup text-buffer >= while
      dup c@ 10 = if drop exit then
      swap 1+ swap 1-
    repeat
    drop
  then
;

: move-up ( -- )
  current-col >r
  begin gap-start @ text-buffer > gap-start @ 1- c@ 10 <> and while move-left repeat
  gap-start @ text-buffer > if
    move-left
    begin gap-start @ text-buffer > gap-start @ 1- c@ 10 <> and while move-left repeat
    r> 0 ?do gap-end @ text-buffer MAX-BUFFER + < if gap-end @ c@ 10 <> if move-right then then loop
  else r> 0 ?do move-right loop then
  request-redraw
;

: move-down ( -- )
  current-col >r
  begin gap-end @ text-buffer MAX-BUFFER + < gap-end @ c@ 10 <> and while move-right repeat
  gap-end @ text-buffer MAX-BUFFER + < if
    move-right
    r> 0 ?do gap-end @ text-buffer MAX-BUFFER + < if gap-end @ c@ 10 <> if move-right then then loop
  else r> drop then
  request-redraw
;

\ --- Selection Helpers ---
: clear-sel  ( -- ) -1 sel-anchor ! request-redraw ;
: sel-active? ( -- flag ) sel-anchor @ -1 <> ;
: sel-lower ( -- n ) sel-anchor @ cursor-pos min ;
: sel-upper ( -- n ) sel-anchor @ cursor-pos max ;
: in-sel? ( n -- flag )
  sel-active? if dup sel-lower >= swap sel-upper < and else drop 0 then
;

: nav-move-left  ( -- ) clear-sel move-left ;
: nav-move-right ( -- ) clear-sel move-right ;
: nav-move-up    ( -- ) clear-sel move-up ;
: nav-move-down  ( -- ) clear-sel move-down ;

: sel-extend-left  ( -- ) sel-active? 0= if cursor-pos sel-anchor ! then move-left request-redraw ;
: sel-extend-right ( -- ) sel-active? 0= if cursor-pos sel-anchor ! then move-right request-redraw ;
: sel-extend-up ( -- ) sel-active? 0= if cursor-pos sel-anchor ! then move-up request-redraw ;
: sel-extend-down ( -- ) sel-active? 0= if cursor-pos sel-anchor ! then move-down request-redraw ;

\ --- Clipboard ---
: do-copy ( -- )
  sel-active? if
    sel-upper sel-lower - dup clipboard-len !
    0= if exit then
    sel-lower sel-upper over - 0 ?do
      over i +
      dup gap-start @ text-buffer - < if text-buffer + else gap-end @ + gap-start @ text-buffer - - then
      c@ clipboard-buf i + c!
    loop
    drop clear-sel
  then
;

variable cut-lo   variable cut-hi
: set-cursor-pos ( n -- )
  begin dup cursor-pos > gap-end @ text-buffer MAX-BUFFER + < and while move-right repeat
  begin dup cursor-pos < gap-start @ text-buffer > and while move-left repeat
  drop
;

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
    cut-hi @ set-cursor-pos
    clipboard-len @ 0 ?do delete-char loop
    request-redraw
  then
;

: do-paste ( -- )
  clipboard-len @ 0> if
    \ Check if we have enough space
    gap-size clipboard-len @ < if
      ERR-BUFFER-FULL last-err-code !
      s" Error: Not enough space for paste!" set-status
      request-redraw
      exit
    then
    clipboard-len @ 0 ?do clipboard-buf i + c@ insert-char drop loop
    clear-status
  then
;

: do-select-all ( -- )
  0 sel-anchor !
  begin gap-end @ text-buffer MAX-BUFFER + < while
    gap-end @ c@ gap-start @ c! 1 gap-start +! 1 gap-end +!
  repeat
  request-redraw
;

\ --- Virtual Console Engine ---
: ansi-start   ( -- ) ESC emit [char] [ emit ;
: move-cursor  ( r c -- ) ansi-start swap 0 .r ." ;" 0 .r ." H" ;
: clear-screen ( -- ) ansi-start ." 2J" ansi-start ." H" ;

\ Clears the VRAM memory so we can draw a fresh frame
: clear-vram ( -- )
  0 vstyle !
  VRAM-SIZE 0 ?do
    32 vram i + c!          \ space character
    0  vram i + 1+ c!       \ style normal
  2 +loop
;

\ Set the 'cursor' position within vram.
\ Values are 0-indexed (row 0-23, col 0-79)
: vgotoyx ( r c -- ) vcol ! vrow ! ;

\ Write a single character to VRAM at current vrow/vcol, advancing vcol
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

\ Fill remainder of current VRAM row with spaces of current style
: vclear-eol ( -- )
  begin vcol @ TERM-COLS < while 32 vputc repeat
;

\ Print string to VRAM
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

\ Diff `vram` vs `old-vram` and emit ANSI codes only for changes
: render-screen ( -- )
  \ Hide cursor during drawing to prevent jumping
  ansi-start ." ?25l"
  
  0 cur-style ! \ track current terminal style
  
  TERM-ROWS 0 ?do
    TERM-COLS 0 ?do
      \ Calculate offset
      j TERM-COLS * i + 2 * roffset !
      
      \ Fetch new values from vram
      vram roffset @ + c@       vchar !
      vram roffset @ + 1+ c@    vsty !
      
      \ Fetch old values from old-vram
      old-vram roffset @ + c@      ochar !
      old-vram roffset @ + 1+ c@   osty !
      
      \ Compare: Did the character or style change?
      vchar @ ochar @ <>  vsty @ osty @ <>  or if
        \ Change detected! Move hardware cursor
        j 1+ i 1+ move-cursor
        
        \ Switch ANSI style if it differs from cur-style
        vsty @ cur-style @ <> if
          vsty @ case
            0 of ansi-start ." 0m"      endof
            1 of ansi-start ." 47;30m"  endof
            2 of ansi-start ." 7m"      endof
          endcase
          vsty @ cur-style !
        then
        
        \ Emit char
        vchar @ emit
        
        \ Sync old-vram so it matches physical screen state
        vchar @ old-vram roffset @ + c!
        vsty @  old-vram roffset @ + 1+ c!
      then
    loop
  loop
  
  \ Turn style back to normal at the end
  ansi-start ." 0m"
  
  \ Show cursor again
  ansi-start ." ?25h"
;

\ --- UI Drawing (VRAM Pass) ---
: draw-title-bar ( -- )
  0 0 vgotoyx
  1 vstyle ! \ Menu style
  s"  fNotepad - " vprint
  filename-len @ 0> if get-filename vprint else s" Untitled " vprint then
  vclear-eol
;

: draw-menu-bar ( -- )
  1 0 vgotoyx
  1 vstyle ! \ Menu style
  s"  File (^S) | Edit: Shift+Arrows Sel, ^C/^X/^V | ^Q Quit " vprint
  vclear-eol
;

: draw-status-bar ( -- )
  \ Draw status/error message at bottom of screen
  TERM-ROWS 1- 0 vgotoyx
  last-err-code @ 0<> if
    2 vstyle ! \ Error style (highlighted)
  else
    0 vstyle ! \ Normal style
  then
  status-msg status-msg-len @ vprint
  vclear-eol
;

\ draw-char writes to VRAM, handling newlines
: draw-char ( c -- )
  dup 10 = if
    vclear-eol
    1 vrow +! 0 vcol ! 
    drop
  else
    vputc
  then
;

\ Draw the text body. Emit highlight ANSI only on transitions.
: draw-text ( -- )
  2 0 vgotoyx \ Line 3 logically (0-indexed = 2)
  0 vstyle !

  \ --- Before-gap block ---
  gap-start @ text-buffer > if
    gap-start @ text-buffer ?do
      i text-buffer - in-sel? if 2 vstyle ! else 0 vstyle ! then
      i c@ draw-char
    loop
  then

  \ --- After-gap block ---
  gap-end @ text-buffer MAX-BUFFER + < if
    text-buffer MAX-BUFFER + gap-end @ ?do
      cursor-pos i gap-end @ - + in-sel? if 2 vstyle ! else 0 vstyle ! then
      i c@ draw-char
    loop
  then
  
  0 vstyle !
  \ Clear the rest of the VRAM screen if text didn't reach the bottom
  \ Reserve last row for status bar
  begin vrow @ TERM-ROWS 1- < while 
    vclear-eol
    1 vrow +! 0 vcol !
  repeat
  
  \ Draw status bar
  draw-status-bar
;

\ Calculates physical cursor for terminal (1-indexed) based on text buffer
: sync-real-cursor ( -- )
  3 1 \ start row=3, col=1
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
: parse-escape ( -- )
  key
  [char] [ = if
    key
    case
      [char] A of nav-move-up    endof
      [char] B of nav-move-down  endof
      [char] C of nav-move-right endof
      [char] D of nav-move-left  endof
      [char] 1 of
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

\ Word to safely clean up terminal state
: cleanup-editor ( -- )
  disable-raw-mode
  clear-screen
  \ Ensure terminal is in a sane state
  ansi-start ." 0m"  \ Reset colors
  ansi-start ." ?25h" \ Show cursor
;

\ Internal editor loop - this can throw exceptions
: editor-loop ( -- flag )
  init-buffer handle-args enable-raw-mode
  clear-screen
  
  begin
    needs-redraw @ 1 = if
      clear-vram
      draw-title-bar
      draw-menu-bar
      draw-text
      render-screen     \ Emit changes
      sync-real-cursor  \ Move hardware cursor to insertion point
      0 needs-redraw !
    then

    key

    dup 17 = if drop 1 else   \ Ctrl+Q  -> quit
    dup 19 = if save-file drop 0 else   \ Ctrl+S  -> save
    dup 27 = if drop parse-escape 0 else   \ ESC     -> arrows
    dup  1 = if do-select-all drop 0 else   \ Ctrl+A  -> select all
    dup  3 = if do-copy  drop 0 else   \ Ctrl+C  -> copy
    dup 24 = if do-cut   drop 0 else   \ Ctrl+X  -> cut
    dup 22 = if do-paste drop 0 else   \ Ctrl+V  -> paste
    dup 13 = over 10 = or if drop 10 insert-char drop 0 else   \ Enter
    dup 127 = over 8 = or if drop delete-char 0 else   \ Backspace
    dup 32 >= over 126 <= and if insert-char drop 0 else   \ Printable
    drop 0   
    then then then then then then then then then then
  until
  
  1  \ Normal exit with quit flag
;

\ Public entry point with exception handling
: run-editor ( -- )
  ['] editor-loop catch
  dup 0<> if
    \ An exception occurred - make sure we still clean up
    nip  \ Remove the xt, keep exception code
  then
  
  \ Always restore terminal, even if an exception occurred
  cleanup-editor
  
  \ If there was an error, display it
  dup 0<> if
    ." Exception occurred: " . cr
  else
    drop
  then
;

run-editor
bye
