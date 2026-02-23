: enable-raw-mode ( -- ) s" stty raw -echo" system ;
: disable-raw-mode ( -- ) s" stty sane" system ;
enable-raw-mode
key emit
disable-raw-mode
cr bye
