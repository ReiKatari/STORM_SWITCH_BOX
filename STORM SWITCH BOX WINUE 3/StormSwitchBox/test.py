import os, struct, glob

files = glob.glob(r"e:\STORM SWITCH BOX\**\*v65536*.nsz", recursive=True)
if not files:
    print("File not found")
    exit()

nszPath = files[0]
print("Found:", nszPath)

with open(nszPath, "rb") as f:
    f.seek(0)
    magic = f.read(4)
    if magic != b"PFS0":
        print("Not PFS0")
        exit()
        
    num_files, string_table_size = struct.unpack("<II", f.read(8))
    f.read(4) # reserved
    
    entries = []
    for i in range(num_files):
        offset, size, string_offset, _ = struct.unpack("<QQII", f.read(24))
        entries.append({"offset": offset, "size": size, "name_offset": string_offset})
        
    string_table = f.read(string_table_size)
    data_start = f.tell()
    
    for e in entries:
        name = string_table[e["name_offset"]:].split(b'\0', 1)[0].decode("utf-8")
        print(f"File: {name}, Size: {e['size']}, Offset: {e['offset']}")
        f.seek(data_start + e['offset'])
        data = f.read(120)
        print("  Data hex:", data.hex())
        try:
            print("  Data ASCII:", data.decode('ascii', errors='replace'))
        except:
            pass
