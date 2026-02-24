#!/bin/bash
# Build script for fnotepad termios helper library

set -e

echo "Building termios_helper library..."

# Detect architecture
ARCH=$(uname -m)
OS=$(uname -s)

echo "Detected: $OS $ARCH"

# Build the shared library
gcc -shared -fPIC -o libtermios_helper.so termios_helper.c

echo "Build successful: libtermios_helper.so"

# Also build the constants generator (optional, for development)
gcc -o get_termios_constants get_termios_constants.c
echo "Built: get_termios_constants"

echo ""
echo "To run fnotepad:"
echo "  gforth src/notepad.fs [filename]"
