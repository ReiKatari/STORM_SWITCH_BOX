import winreg
import sys

def delete_sub_key(key0, current_key):
    try:
        open_key = winreg.OpenKey(key0, current_key, 0, winreg.KEY_ALL_ACCESS)
    except FileNotFoundError:
        return
    info_key = winreg.QueryInfoKey(open_key)
    for x in range(0, info_key[0]):
        sub_key = winreg.EnumKey(open_key, 0)
        delete_sub_key(key0, f"{current_key}\\{sub_key}")
    winreg.DeleteKey(open_key, "")
    open_key.Close()

app_path = r"C:\Users\ReiKatari\AppData\Local\Programs\STORM_SWITCH_BOX\StormSwitchBox.exe"
associations = [".nsp", ".nsz", ".xci", ".xcz", "Directory"]
formats = ["NSP", "NSZ", "XCI", "XCZ"]

for assoc in associations:
    if assoc == "Directory":
        base_path = r"Software\Classes\Directory\shell\StormSwitchBox"
    else:
        base_path = fr"Software\Classes\SystemFileAssociations\{assoc}\shell\StormSwitchBox"

    delete_sub_key(winreg.HKEY_CURRENT_USER, base_path)

    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, base_path) as key:
        winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, "STORM SWITCH BOX")
        winreg.SetValueEx(key, "Icon", 0, winreg.REG_SZ, app_path)
        winreg.SetValueEx(key, "SubCommands", 0, winreg.REG_SZ, "")

    # Update
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\01update") as key:
        winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, "Обновление")
        winreg.SetValueEx(key, "SubCommands", 0, winreg.REG_SZ, "")
    for fmt in formats:
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\01update\shell\{fmt}") as key:
            winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, f"в формат {fmt}")
            with winreg.CreateKey(key, "command") as cmdKey:
                winreg.SetValueEx(cmdKey, "", 0, winreg.REG_SZ, f'"{app_path}" --action update --format {fmt} "%1"')

    # Unpack
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\02unpack") as key:
        winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, "Распаковка")
        with winreg.CreateKey(key, "command") as cmdKey:
            winreg.SetValueEx(cmdKey, "", 0, winreg.REG_SZ, f'"{app_path}" --action unpack "%1"')

    # Pack
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\03pack") as key:
        winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, "Упаковка")
        winreg.SetValueEx(key, "SubCommands", 0, winreg.REG_SZ, "")
    for fmt in formats:
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\03pack\shell\{fmt}") as key:
            winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, f"в формат {fmt}")
            with winreg.CreateKey(key, "command") as cmdKey:
                winreg.SetValueEx(cmdKey, "", 0, winreg.REG_SZ, f'"{app_path}" --action pack --format {fmt} "%1"')

    # Convert
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\04convert") as key:
        winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, "Конвертация")
        winreg.SetValueEx(key, "SubCommands", 0, winreg.REG_SZ, "")
    for fmt in formats:
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\04convert\shell\{fmt}") as key:
            winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, f"в формат {fmt}")
            with winreg.CreateKey(key, "command") as cmdKey:
                winreg.SetValueEx(cmdKey, "", 0, winreg.REG_SZ, f'"{app_path}" --action convert --format {fmt} "%1"')

    # Multi
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\05multi") as key:
        winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, "Мульти-контент")
        winreg.SetValueEx(key, "SubCommands", 0, winreg.REG_SZ, "")
    for fmt in formats:
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, fr"{base_path}\shell\05multi\shell\{fmt}") as key:
            winreg.SetValueEx(key, "MUIVerb", 0, winreg.REG_SZ, f"в формат {fmt}")
            with winreg.CreateKey(key, "command") as cmdKey:
                winreg.SetValueEx(cmdKey, "", 0, winreg.REG_SZ, f'"{app_path}" --action multi --format {fmt} "%1"')

print("Context Menus fixed!")
