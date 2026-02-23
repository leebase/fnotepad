: enable-raw-mode ( -- ) s" stty raw -echo" system ;
: disable-raw-mode ( -- ) s" stty sane" system ;

enable-raw-mode
cr ." Press your Up arrow key, then 'q' to quit..." cr
begin
  key
  dup [char] q = if drop 1 else
    dup . space
    0
  then
until
disable-raw-mode
bye
