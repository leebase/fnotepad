: enable-raw-mode ( -- ) s" stty raw -echo" system ;
: disable-raw-mode ( -- ) s" stty sane" system ;

enable-raw-mode
cr ." Press your Up arrow key, then 'q' to quit..." cr
begin
  key
  dup [char] q = if drop 1 else
    dup . space
    \ If we see ESC (27), aggressively read the next two bytes without key? timeout
    dup 27 = if
      key . space
      key . space
    then
    0
  then
until
disable-raw-mode
bye
