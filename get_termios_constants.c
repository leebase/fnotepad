#include <termios.h>
#include <unistd.h>
#include <stdio.h>

int main() {
    printf("constant TCGETS %d\n", 0x5401); // often used for ioctl if tcgetattr is tricky, but we can just use constants
    printf("%d constant TCSAFLUSH\n", TCSAFLUSH);
    printf("%d constant ECHO\n", ECHO);
    printf("%d constant ICANON\n", ICANON);
    printf("%d constant IEXTEN\n", IEXTEN);
    printf("%d constant ISIG\n", ISIG);
    printf("%d constant IXON\n", IXON);
    printf("%d constant ICRNL\n", ICRNL);
    printf("%d constant OPOST\n", OPOST);
    printf("%d constant CS8\n", CS8);
    printf("%d constant VMIN\n", VMIN);
    printf("%d constant VTIME\n", VTIME);
    printf("%d constant STDIN_FILENO\n", STDIN_FILENO);
    printf("%zu constant sizeof_termios\n", sizeof(struct termios));
    
    struct termios t;
    printf("%zu constant offset_c_iflag\n", (char*)&t.c_iflag - (char*)&t);
    printf("%zu constant offset_c_oflag\n", (char*)&t.c_oflag - (char*)&t);
    printf("%zu constant offset_c_cflag\n", (char*)&t.c_cflag - (char*)&t);
    printf("%zu constant offset_c_lflag\n", (char*)&t.c_lflag - (char*)&t);
    printf("%zu constant offset_c_line\n", (char*)&t.c_line - (char*)&t);
    printf("%zu constant offset_c_cc\n", (char*)&t.c_cc - (char*)&t);
    return 0;
}
