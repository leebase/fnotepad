#include <termios.h>
#include <unistd.h>
#include <stdio.h>

struct termios orig_termios;

void disable_raw_mode() {
    tcsetattr(STDIN_FILENO, TCSAFLUSH, &orig_termios);
}

void enable_raw_mode() {
    tcgetattr(STDIN_FILENO, &orig_termios);
    struct termios raw = orig_termios;
    
    // Turn off echoing, canonical mode (line buffering), 
    // extended input processing, and signals
    raw.c_lflag &= ~(ECHO | ICANON | IEXTEN | ISIG);
    
    // Turn off software flow control
    raw.c_iflag &= ~(IXON | ICRNL);
    
    // Turn off output processing
    raw.c_oflag &= ~(OPOST);
    
    // Character size 8 bits
    raw.c_cflag |= (CS8);
    
    // Setup timeout and minimum characters to read
    raw.c_cc[VMIN] = 0;
    raw.c_cc[VTIME] = 1; // 100ms timeout
    
    tcsetattr(STDIN_FILENO, TCSAFLUSH, &raw);
}
