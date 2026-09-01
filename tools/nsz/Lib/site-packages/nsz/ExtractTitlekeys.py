from pathlib import Path
from nsz.nut import Print
from nsz.Fs import factory
from nsz.PathTools import expandFiles, isNspNsz
import json


def extractTitlekeys(argsFile):
    titlekeysDict = {}
    if Path("titlekeys.txt").is_file():
        with open("titlekeys.txt", "r", encoding="utf-8") as titlekeysFile:
            for line in titlekeysFile:
                (rightsId, titleKey, name) = line.rstrip().split("|")
                titlekeysDict[rightsId[0:16]] = (rightsId, titleKey, name)
                # Print.info("Read: {0}|{1}|{2}".format(rightsId, titleKey, name))
    for f_str in argsFile:
        for filePath in expandFiles(Path(f_str)):
            if not isNspNsz(filePath):
                continue
            f = factory(filePath)
            f.open(str(filePath), "rb")
            ticket = f.ticket()
            if ticket is None:
                # This ticket conditional was added to prevent the following exception from occurring when parsing a ticketless dump file:
                # NUT exception: 'NoneType' object has no attribute 'getRightsId'
                Print.info("Skipped ticketless {0}".format(filePath.stem))
            else:
                rightsId = format(ticket.getRightsId(), "x").zfill(32)
                titleId = rightsId[0:16]
                if titleId not in titlekeysDict:
                    titleKey = format(ticket.getTitleKeyBlock(), "x").zfill(32)
                    titlekeysDict[titleId] = (rightsId, titleKey, filePath.stem)
                    Print.info(
                        "Found: {0}|{1}|{2}".format(rightsId, titleKey, filePath.stem)
                    )
                else:
                    Print.info("Skipped already existing {0}".format(rightsId))
            f.close()
    Print.info("\ntitlekeys.txt:")
    with open("titlekeys.txt", "w", encoding="utf-8") as titlekeysFile:
        for titleId in sorted(titlekeysDict.keys()):
            (rightsId, titleKey, name) = titlekeysDict[titleId]
            titleDBLine = "{0}|{1}|{2}".format(rightsId, titleKey, name)
            titlekeysFile.write(titleDBLine + "\n")
            Print.info(titleDBLine)
    if not Path("./titledb/").is_dir():
        return
    Print.info("\n\ntitledb:")
    Print.info("========")
    titleDbPath = Path("titledb").resolve()
    excludeJson = {
        "cheats.json",
        "cnmts.json",
        "languages.json",
        "ncas.json",
        "versions.json",
    }
    for filePath in expandFiles(titleDbPath):
        if filePath.suffix == ".json":
            fileName = filePath.name
            if fileName in excludeJson:
                continue
            saveTrigger = False
            Print.info("Reading {0}".format(fileName))
            with open(str(filePath)) as json_file:
                data = json.load(json_file)
            for key, value in data.items():
                titleId = value["id"]
                if titleId is None:
                    continue
                if titleId in titlekeysDict:
                    (rightsId, titleKey, name) = titlekeysDict[titleId]
                    if value["rightsId"] is None:
                        value["rightsId"] = rightsId
                        saveTrigger = True
                    # elif value["rightsId"] != rightsId:
                    # Print.info("Warn: {0} != {1}".format(value["rightsId"], rightsId))
                    if value["key"] is None:
                        Print.info("{0}: Writing key to {1}".format(fileName, titleId))
                        value["key"] = titleKey
                        saveTrigger = True
            if saveTrigger:
                Print.info("Saving {0}".format(fileName))
                with open(str(filePath), "w") as outfile:
                    json.dump(data, outfile, indent=4, sort_keys=True)
                Print.info("{0} saved!".format(fileName))
            Print.info("")
