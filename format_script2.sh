#!/bin/bash
cat << 'INNER_EOF' > new_notepad2.fs
\ fnotepad - A TUI Notepad Clone in Forth
\ Requires Gforth

\ --- Constants and Configuration ---
27 constant ESC  \ ASCII Escape Character

\ --- Terminal Raw Mode ---
: enable-raw-mode ( -- ) s" stty raw -echo" system ;
: disable-raw-mode ( -- ) s" stty sane" system ;

\ --- Gap Buffer Memory ---
65536 constant MAX-BUFFER \ 64KB max text size
create text-buffer MAX-BUFFER allot
variable gap-start
variable gap-end
variable needs-redraw

: init-buffer ( -- )
  text-buffer gap-start !
  text-buffer MAX-BUFFER + gap-end !
  1 needs-redraw !
;

\ --- File I/O ---
create filename-buf 256 allot
variable filename-len

: set-filename ( c-addr u -- )
  dup 256 min filename-len !
  filename-buf swap cmove
;

: get-filename ( -- c-addr u )
  filename-buf filename-len @
;

: handle-args ( -- )
  next-arg dup 0> if
    2dup set-filename
    r/w open-file 0= if
      >r
      text-buffer MAX-BUFFER r@ read-file throw
      text-buffer + gap-start !
      r> close-file throw
    else
      drop \ File might not exist yet
    then
  else
    2drop
  then
;

: save-file ( -- )
  filename-len @ 0> if
    get-filename w/o create-file 0= if
      >r
      text-buffer gap-start @ text-buffer - r@ write-file throw
      gap-end @ MAX-BUFFER text-buffer + gap-end @ - r@ write-file throw
      r> close-file throw
    else
      drop
    then
  then
;

: gap-size ( -- n ) gap-end @ gap-start @ - ;

: request-redraw ( -- ) 1 needs-redraw ! ;

: insert-char ( c -- )
  gap-size 0> if
    gap-start @ c!
    1 gap-start +!
    request-redraw
  else
    drop \ Buffer full
  then
;

: delete-char ( -- ) \ Backspace
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

\ Find previous newline or start of buffer
: line-start ( addr -- addr' )
  begin
    dup text-buffer > while
    dup 1- c@ 10 = if exit then
    1-
  repeat
;

\ Find next newline or end of buffer
: line-end ( addr -- addr' )
  begin
    dup text-buffer MAX-BUFFER + < while
    dup c@ 10 = if exit then
    1+
  repeat
;

: move-up ( -- )
  \ Find start of current line
  gap-start @ line-start >r
  r@ text-buffer > if \ If not on first line
    \ Find start of previous line
    r@ 1- line-start >r
    \ Calculate visual column on current line
    gap-start @ r> - >r  \ ( -- R: start-of-prev, current-col )
    
    \ Move gap left until we hit the new position
    begin
      gap-start @ r> r@ + > while
      move-left
    repeat
    r> drop
  else
    r> drop
  then
;

: move-down ( -- )
  gap-end @ line-end dup text-buffer MAX-BUFFER + < if
    \ We found the end of the current line, next line starts at 1+
    1+ >r
    \ Calculate visual column on current line
    gap-start @ gap-start @ line-start - >r \ ( -- R: start-of-next, current-col )
    
    \ Move gap right until we hit the new position or end of next line
    begin
      gap-end @ r> r@ + <
      gap-end @ text-buffer MAX-BUFFER + < and
      gap-end @ c@ 10 <> and while
      move-right
    repeat
    r> drop
  else
    drop
  then
;

: calc-cursor ( -- r c ) \ Returns Row and Col for move-cursor
  2 1 \ start row=2, col=1
  gap-start @ text-buffer > if
    gap-start @ text-buffer ?do
      i c@ 10 = if
        drop 1 swap 1+ swap
      else
        1+
      then
    loop
  then
;

\ --- Terminal Control (ANSI Escape Sequences) ---

: emit-esc ( -- ) ESC emit ;
: emit-bracket ( -- ) [char] [ emit ;
: ansi-start ( -- ) emit-esc emit-bracket ;

\ Clear the entire terminal screen
: clear-screen ( -- )
  ansi-start ." 2J" ansi-start ." H"
;

\ Clear to end of screen
: clear-to-eos ( -- )
  ansi-start ." 0J"
;

\ Move cursor to Row (R) and Col (C) - 1-indexed
: move-cursor ( r c -- )
  ansi-start swap 0 .r ." ;" 0 .r ." H"
;

\ --- UI Drawing ---

: draw-title-bar ( -- )
  ansi-start ." 44;37m"  \ Background Blue (44), Text White (37)
  1 1 move-cursor
  ."  fNotepad - "
  filename-len @ 0> if
    get-filename type
  else
    ." Untitled "
  then
  ansi-start ." K" \ Clear to end of line with blue
  ansi-start ." 0m"
;

: draw-status-bar ( -- )
  ansi-start ." 47;30m"
  24 1 move-cursor
  ."  ^S Save  |  ^Q Quit  |  Arrows Navigate "
  ansi-start ." K" \ Clear to end of line with grey
  ansi-start ." 0m"
;

\ --- Drawing Text ---
: draw-char ( c -- )
  dup 10 = if
    drop 13 emit 10 emit \ CRLF
  else
    emit
  then
;

: draw-text ( -- )
  \ Move to start of text area
  2 1 move-cursor
  
  \ Print text before gap
  gap-start @ text-buffer > if
    gap-start @ text-buffer ?do
      i c@ draw-char
    loop
  then
  
  \ Print the rest
  gap-end @ text-buffer MAX-BUFFER + < if
    text-buffer MAX-BUFFER + gap-end @ ?do
      i c@ draw-char
    loop
  then
  
  \ Clear any remaining old text on the screen
  clear-to-eos
;

\ --- Main Loop ---

: parse-escape ( -- )
  \ Read the rest of the escape sequence
  key? if
    key [char] [ = if
      key? if
        key
        case
          [char] A of move-up endof \ Up
          [char] B of move-down endof \ Down
          [char] C of move-right endof \ Right
          [char] D of move-left endof \ Left
        endcase
      then
    then
  then
;

: run-editor ( -- )
  init-buffer
  handle-args
  enable-raw-mode
  
  begin
    needs-redraw @ 1 = if
      draw-title-bar
      draw-status-bar
      draw-text
      0 needs-redraw !
    then
    
    calc-cursor move-cursor \ Always place cursor dynamically
    
    key
    
    dup 17 = if \ Ctrl+Q
      drop 1
    else
      dup 19 = if \ Ctrl+S
        save-file drop 0
      else
        dup 27 = if \ ESC
          drop parse-escape 0
        else
          dup 13 = over 10 = or if \ Return / Enter (CR or LF)
            drop 10 insert-char 0
          else
            dup 127 = over 8 = or if \ Backspace (127 or 8)
              drop delete-char 0
            else
              \ Printable characters
              dup 32 >= over 126 <= and if
                insert-char 0
              else
                drop 0 \ Ignore unhandled for now
              then
            then
          then
        then
      then
    then
  until
  
  disable-raw-mode
  clear-screen
;

\ Start the editor
run-editor
bye
INNER_EOF
mv new_notepad2.fs notepad.fs
