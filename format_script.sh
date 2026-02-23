#!/bin/bash
# Reorganize notepad.fs to ensure definitions are in the correct order

cat << 'INNER_EOF' > new_notepad.fs
\ fnotepad - A TUI Notepad Clone in Forth
\ Requires Gforth

\ --- Constants and Configuration ---
27 constant ESC  \ ASCII Escape Character

\ --- C FFI: Terminal Raw Mode ---
c-library termios_helper
  \c #include <termios.h>
  \c #include <unistd.h>
  \c struct termios orig_termios;
  \c void disable_raw_mode() { tcsetattr(STDIN_FILENO, TCSAFLUSH, &orig_termios); }
  \c void enable_raw_mode() {
  \c     tcgetattr(STDIN_FILENO, &orig_termios);
  \c     struct termios raw = orig_termios;
  \c     raw.c_lflag &= ~(ECHO | ICANON | IEXTEN | ISIG);
  \c     raw.c_iflag &= ~(IXON | ICRNL);
  \c     raw.c_oflag &= ~(OPOST);
  \c     raw.c_cflag |= (CS8);
  \c     raw.c_cc[VMIN] = 0;
  \c     raw.c_cc[VTIME] = 1;
  \c     tcsetattr(STDIN_FILENO, TCSAFLUSH, &raw);
  \c }
  
  c-function enable-raw-mode enable_raw_mode -- void
  c-function disable-raw-mode disable_raw_mode -- void
end-c-library

\ --- Gap Buffer Memory ---
65536 constant MAX-BUFFER \ 64KB max text size
create text-buffer MAX-BUFFER allot
variable gap-start
variable gap-end
variable cursor-x
variable cursor-y

: init-buffer ( -- )
  text-buffer gap-start !
  text-buffer MAX-BUFFER + gap-end !
  1 cursor-x !
  2 cursor-y !
;

: gap-size ( -- n ) gap-end @ gap-start @ - ;

: insert-char ( c -- )
  gap-size 0> if
    gap-start @ c!
    1 gap-start +!
    1 cursor-x +!
  else
    drop \ Buffer full
  then
;

: delete-char ( -- ) \ Backspace
  gap-start @ text-buffer > if
    -1 gap-start +!
    cursor-x @ 1 > if -1 cursor-x +! then
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

\ Move cursor to Row (R) and Col (C) - 1-indexed
: move-cursor ( r c -- )
  ansi-start swap 0 .r ." ;" 0 .r ." H"
;

\ --- UI Drawing ---

: draw-title-bar ( -- )
  ansi-start ." 44;37m"  \ Background Blue (44), Text White (37)
  1 1 move-cursor
  ."  fNotepad - Untitled "
  ansi-start ." 0m"
;

: draw-status-bar ( -- )
  ansi-start ." 47;30m"
  24 1 move-cursor
  ."  ^S Save  |  ^Q Quit  |  Arrows Navigate "
  ansi-start ." 0m"
;

\ --- Drawing Text ---
: draw-text ( -- )
  \ Move to start of text area
  2 1 move-cursor
  
  \ Print text before gap
  gap-start @ text-buffer > if
    gap-start @ text-buffer ?do
      i c@ emit
    loop
  then
  
  \ Assuming no newlines for this very first test, print the rest
  gap-end @ text-buffer MAX-BUFFER + < if
    text-buffer MAX-BUFFER + gap-end @ ?do
      i c@ emit
    loop
  then
;

\ --- Main Loop ---

: run-editor ( -- )
  init-buffer
  enable-raw-mode
  
  begin
    clear-screen
    draw-title-bar
    draw-status-bar
    draw-text
    cursor-y @ cursor-x @ move-cursor \ Place cursor
    
    key
    
    dup 17 = if \ Ctrl+Q
      drop 1
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
  until
  
  disable-raw-mode
  clear-screen
;

\ Start the editor
run-editor
bye
INNER_EOF
mv new_notepad.fs notepad.fs
