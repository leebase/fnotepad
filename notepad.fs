\ fnotepad - A TUI Notepad Clone in Forth
\ Requires Gforth

\ --- Constants ---
27 constant ESC

\ --- Terminal Raw Mode ---
: enable-raw-mode  ( -- ) s" stty raw -echo" system ;
: disable-raw-mode ( -- ) s" stty sane"     system ;

\ --- Memory ---
65536 constant MAX-BUFFER
create text-buffer    MAX-BUFFER allot
create clipboard-buf  MAX-BUFFER allot
variable gap-start
variable gap-end
variable needs-redraw
variable sel-anchor    \ logical pos of selection start (-1 = no selection)
variable clipboard-len

: init-buffer ( -- )
  text-buffer gap-start !
  text-buffer MAX-BUFFER + gap-end !
  1 needs-redraw !
  -1 sel-anchor !
  0  clipboard-len !
;

\ --- File I/O ---
create filename-buf 256 allot
variable filename-len

: set-filename ( c-addr u -- )
  dup 256 min filename-len !
  filename-buf swap cmove
;
: get-filename ( -- c-addr u ) filename-buf filename-len @ ;

: handle-args ( -- )
  next-arg dup 0> if
    2dup set-filename
    r/w open-file 0= if
      >r
      text-buffer MAX-BUFFER r@ read-file throw
      text-buffer + gap-start !
      r> close-file throw
    else drop
    then
  else 2drop
  then
;

: save-file ( -- )
  filename-len @ 0> if
    get-filename w/o create-file 0= if
      >r
      text-buffer gap-start @ text-buffer - r@ write-file throw
      gap-end @ text-buffer MAX-BUFFER + gap-end @ - r@ write-file throw
      r> close-file throw
    else drop
    then
  then
;

\ --- Gap Buffer Primitives ---
: gap-size     ( -- n )    gap-end @ gap-start @ - ;
: cursor-pos   ( -- n )    gap-start @ text-buffer - ;  \ logical index

: request-redraw ( -- ) 1 needs-redraw ! ;

: insert-char ( c -- )
  gap-size 0> if
    gap-start @ c!
    1 gap-start +!
    request-redraw
  else drop
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
      swap 1+ swap
      1-
    repeat
    drop
  then
;

: move-up ( -- )
  current-col >r
  begin
    gap-start @ text-buffer >
    gap-start @ 1- c@ 10 <> and while
    move-left
  repeat
  gap-start @ text-buffer > if
    move-left
    begin
      gap-start @ text-buffer >
      gap-start @ 1- c@ 10 <> and while
      move-left
    repeat
    r> 0 ?do
      gap-end @ text-buffer MAX-BUFFER + < if
        gap-end @ c@ 10 <> if move-right then
      then
    loop
  else
    r> 0 ?do move-right loop
  then
  request-redraw
;

: move-down ( -- )
  current-col >r
  begin
    gap-end @ text-buffer MAX-BUFFER + <
    gap-end @ c@ 10 <> and while
    move-right
  repeat
  gap-end @ text-buffer MAX-BUFFER + < if
    move-right
    r> 0 ?do
      gap-end @ text-buffer MAX-BUFFER + < if
        gap-end @ c@ 10 <> if move-right then
      then
    loop
  else
    r> drop
  then
  request-redraw
;

\ --- Selection Helpers ---
: clear-sel  ( -- ) -1 sel-anchor ! request-redraw ;

: sel-active? ( -- flag ) sel-anchor @ -1 <> ;

: sel-lower ( -- n )   \ logical lower bound of selection
  sel-anchor @ cursor-pos min
;
: sel-upper ( -- n )   \ logical upper bound
  sel-anchor @ cursor-pos max
;

\ Is logical index n inside the selection?
: in-sel? ( n -- flag )
  sel-active? if
    dup sel-lower >= swap sel-upper < and
  else
    drop 0
  then
;

\ Move without extending selection: clear selection first
: nav-move-left  ( -- ) clear-sel move-left ;
: nav-move-right ( -- ) clear-sel move-right ;
: nav-move-up    ( -- ) clear-sel move-up ;
: nav-move-down  ( -- ) clear-sel move-down ;

\ Move while extending selection: anchor if not yet set
: sel-extend-left  ( -- )
  sel-active? 0= if cursor-pos sel-anchor ! then
  move-left request-redraw
;
: sel-extend-right ( -- )
  sel-active? 0= if cursor-pos sel-anchor ! then
  move-right request-redraw
;
: sel-extend-up ( -- )
  sel-active? 0= if cursor-pos sel-anchor ! then
  move-up request-redraw
;
: sel-extend-down ( -- )
  sel-active? 0= if cursor-pos sel-anchor ! then
  move-down request-redraw
;

\ --- Clipboard ---
: do-copy ( -- )
  sel-active? if
    sel-upper sel-lower - dup clipboard-len !
    0= if exit then              \ nothing to copy
    sel-lower sel-upper over - 0 ?do
      over i +                   \ logical addr
      dup gap-start @ text-buffer - < if
        text-buffer +
      else
        gap-end @ +
        gap-start @ text-buffer - -
      then
      c@
      clipboard-buf i + c!
    loop
    drop
    clear-sel
  then
;

variable cut-lo   variable cut-hi

\ Move gap to logical position n
: set-cursor-pos ( n -- )
  begin dup cursor-pos > gap-end @ text-buffer MAX-BUFFER + < and while
    move-right repeat
  begin dup cursor-pos < gap-start @ text-buffer > and while
    move-left  repeat
  drop
;

: do-cut ( -- )
  sel-active? if
    sel-lower cut-lo !
    sel-upper cut-hi !
    cut-hi @ cut-lo @ - dup clipboard-len !
    0> if
      \ copy bytes [cut-lo, cut-hi) into clipboard
      clipboard-len @ 0 ?do
        cut-lo @ i +              \ logical address
        dup gap-start @ text-buffer - < if
          text-buffer +
        else
          gap-end @ +
          gap-start @ text-buffer - -
        then
        c@ clipboard-buf i + c!
      loop
    else drop
    then
    clear-sel
    cut-hi @ set-cursor-pos       \ move cursor to upper bound
    clipboard-len @ 0 ?do delete-char loop
    request-redraw
  then
;

: do-paste ( -- )
  clipboard-len @ 0> if
    clipboard-len @ 0 ?do
      clipboard-buf i + c@ insert-char
    loop
  then
;

\ Select all
: do-select-all ( -- )
  0 sel-anchor !               \ anchor at logical 0
  \ move cursor to end of buffer
  begin
    gap-end @ text-buffer MAX-BUFFER + <
  while
    gap-end @ c@ gap-start @ c!
    1 gap-start +!
    1 gap-end +!
  repeat
  request-redraw
;

\ --- ANSI / Terminal ---
: ansi-start   ( -- ) ESC emit [char] [ emit ;
: clear-screen ( -- ) ansi-start ." 2J" ansi-start ." H" ;
: clear-to-eos ( -- ) ansi-start ." 0J" ;
: move-cursor  ( r c -- ) ansi-start swap 0 .r ." ;" 0 .r ." H" ;

\ --- Calc cursor position on screen ---
: calc-cursor ( -- r c )
  3 1
  cursor-pos 0 ?do
    \ Map logical i to physical char
    i gap-start @ text-buffer - < if
      text-buffer i +
    else
      gap-end @ i + gap-start @ text-buffer - -
    then
    c@ 10 = if
      drop 1 swap 1+ swap
    else
      1+
    then
  loop
;

\ --- UI Drawing ---
: draw-title-bar ( -- )
  ansi-start ." 44;37m"
  1 1 move-cursor
  ."  fNotepad - "
  filename-len @ 0> if get-filename type else ." Untitled " then
  ansi-start ." K"
  ansi-start ." 0m"
;

: draw-menu-bar ( -- )
  ansi-start ." 47;30m"
  2 1 move-cursor
  ."  File (^S) | Edit: Shift+Arrows Sel, ^C Copy, ^X Cut, ^V Paste, ^A All | ^Q Quit "
  ansi-start ." K"
  ansi-start ." 0m"
;

\ draw-char takes the current hl-state, draws the char, and returns
\ the new hl-state. Newlines reset the terminal ANSI AND our hl tracking.
: draw-char ( hl c -- hl' )
  dup 10 = if
    drop
    ansi-start ." 0m"  \ reset terminal before newline scroll
    ansi-start ." K"   \ clear to end of current line
    13 emit 10 emit
    drop 0             \ new hl-state = 0 (terminal is now reset)
  else
    emit               \ hl-state unchanged; leave on stack
  then
;

\ Draw the text body. Emit highlight ANSI only on transitions.
\ Stack during loop: ( current-hl-state ) where 0=normal, 1=highlighted
: draw-text ( -- )
  3 1 move-cursor
  ansi-start ." 0m" \ forcefully strip any menu bar formatting bleed
  0 \ initial state: normal

  \ --- Before-gap block ---
  gap-start @ text-buffer > if
    gap-start @ text-buffer ?do
      i text-buffer - in-sel?    \ ( hl new-hl )
      2dup <> if
        dup if ansi-start ." 7m" else ansi-start ." 0m" then
        nip                       \ ( new-hl )
      else
        drop                      \ ( hl )
      then
      i c@ draw-char             \ ( hl' ) - may reset hl on newline
    loop
  then

  \ --- After-gap block ---
  gap-end @ text-buffer MAX-BUFFER + < if
    text-buffer MAX-BUFFER + gap-end @ ?do
      cursor-pos i gap-end @ - + in-sel?   \ ( hl new-hl )
      2dup <> if
        dup if ansi-start ." 7m" else ansi-start ." 0m" then
        nip
      else
        drop
      then
      i c@ draw-char
    loop
  then

  drop
  ansi-start ." 0m"
  clear-to-eos
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
      [char] 1 of  \ Shift+Arrow: ESC [ 1 ; 2 X
        key drop  \ consume ';'
        key drop  \ consume '2'
        key
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

: run-editor ( -- )
  init-buffer handle-args enable-raw-mode

  begin
    needs-redraw @ 1 = if
      draw-title-bar
      draw-menu-bar
      draw-text
      0 needs-redraw !
    then

    calc-cursor move-cursor

    key

    dup 17 = if drop 1 else   \ Ctrl+Q  → quit
    dup 19 = if save-file drop 0 else   \ Ctrl+S  → save
    dup 27 = if drop parse-escape 0 else   \ ESC     → arrows
    dup  1 = if do-select-all drop 0 else   \ Ctrl+A  → select all
    dup  3 = if do-copy  drop 0 else   \ Ctrl+C  → copy
    dup 24 = if do-cut   drop 0 else   \ Ctrl+X  → cut
    dup 22 = if do-paste drop 0 else   \ Ctrl+V  → paste
    dup 13 = over 10 = or if drop 10 insert-char 0 else   \ Enter
    dup 127 = over 8 = or if drop delete-char 0 else   \ Backspace
    dup 32 >= over 126 <= and if insert-char 0 else   \ Printable
    drop 0   \ ignore
    then then then then then then then then then then
  until

  disable-raw-mode clear-screen
;

run-editor
bye
