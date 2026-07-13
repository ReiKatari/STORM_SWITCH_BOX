import struct
import sys

with open(r'E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\Debug\net8.0-windows10.0.19041.0\win-x64\StormSwitchBox.dll', 'rb') as f:
    data = f.read()

# Search for UTF-16 strings
strings = []
current_str = []
for i in range(0, len(data) - 1, 2):
    b1 = data[i]
    b2 = data[i+1]
    
    if (b2 == 4 and 0x10 <= b1 <= 0x4f) or (b2 == 0 and 0x20 <= b1 <= 0x7e):
        current_str.append(chr(b1 + (b2 << 8)))
    else:
        if len(current_str) > 3:
            s = ''.join(current_str)
            if any('\u0400' <= c <= '\u04ff' for c in s):
                strings.append(s)
        current_str = []

for s in set(strings):
    print(s)