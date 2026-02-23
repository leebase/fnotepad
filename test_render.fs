80 constant TERM-COLS
24 constant TERM-ROWS
TERM-COLS TERM-ROWS * 2 * constant VRAM-SIZE
create vram     VRAM-SIZE allot
create old-vram VRAM-SIZE allot

: ansi-start ;
: move-cursor ( r c -- ) 2drop ;

: render-screen ( -- )
  0 \ track active terminal style
  
  TERM-ROWS 0 ?do
    TERM-COLS 0 ?do
      j TERM-COLS * i + 2 *
      
      dup vram + c@       
      over vram + 1+ c@   
      
      2 pick old-vram + c@      
      3 pick old-vram + 1+ c@   
      
      rot over = rot rot = and 0= if
        j 1+ i 1+ move-cursor
        
        dup 4 pick <> if
          dup case
            0 of endof
            1 of endof
            2 of endof
          endcase
          swap rot drop swap rot swap
        then
        
        drop \ mock emit
        
        2 pick old-vram + 2 pick swap c!
        2 pick old-vram + 1+ over swap c!
      then
      
      drop drop drop
    loop
  loop
  drop
;

render-screen
bye
