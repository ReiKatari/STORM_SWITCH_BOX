from pathlib import Path
from nsz.FileExistingChecks import CreateTargetDict
from nsz.nut import Print
import os
import re


def isOnWhitelist(args, file):
    if not args.undupe_whitelist == "" and re.match(args.undupe_whitelist, file):
        # if args.undupe_dryrun:
        # Print.info("[DRYRUN] [WHITELISTED]: " + file)
        # else:
        # Print.info("[WHITELISTED]: " + file)
        return True
    return False


def undupe(args, argOutFolder):
    filesAtTarget = {}
    alreadyExists = {}
    for f_str in args.file:
        (filesAtTarget, alreadyExists) = CreateTargetDict(
            Path(f_str).absolute(), args, None, filesAtTarget, alreadyExists
        )
    Print.info("")

    for titleID_key, titleID_value in alreadyExists.items():
        maxVersion = max(titleID_value.keys())
        for version_key, version_value in titleID_value.items():
            if args.undupe_old_versions and version_key < maxVersion:
                for file in list(version_value):
                    if not isOnWhitelist(args, file):
                        if args.undupe_dryrun:
                            Print.info(file, "DRYRUN DELETE OLD_VERSION")
                        else:
                            os.remove(file)
                            Print.info(file, "DELETED OLD_VERSION")
                continue

            if not args.undupe_blacklist == "":
                for file in list(version_value):
                    if not isOnWhitelist(args, file) and re.match(
                        args.undupe_blacklist, file
                    ):
                        version_value.remove(file)
                        if args.undupe_dryrun:
                            Print.info(file, "DRYRUN DELETE BLACKLIST")
                        else:
                            os.remove(file)
                            Print.info(file, "DRYRUN DELETED BLACKLIST")

            if not args.undupe_prioritylist == "":
                for file in list(reversed(version_value)):
                    if (
                        len(version_value) > 1
                        and not isOnWhitelist(args, file)
                        and re.match(args.undupe_prioritylist, file)
                    ):
                        version_value.remove(file)
                        if args.undupe_dryrun:
                            Print.info(file, "DRYRUN DELETE PRIORITYLIST")
                        else:
                            os.remove(file)
                            Print.info(file, "DRYRUN DELETED PRIORITYLIST")

            firstDeleted = False
            for file in list(version_value[1:]):
                if not isOnWhitelist(args, file):
                    if args.undupe_dryrun:
                        Print.info(file, "DRYRUN DELETE DUPE")
                        Print.info("Keeping " + version_value[0])
                    else:
                        os.remove(file)
                        Print.info(file, "DELETED DUPE")
                        Print.info("Keeping " + version_value[0])
                elif not firstDeleted and not isOnWhitelist(args, version_value[0]):
                    firstDeleted = True
                    if args.undupe_dryrun:
                        Print.info(version_value[0], "DRYRUN DELETE DUPE")
                        Print.info("Keeping " + file)
                    else:
                        os.remove(version_value[0])
                        Print.info(version_value[0], "DELETED DUPE")
                        Print.info("Keeping " + file)
            if args.undupe_rename or args.undupe_hardlink:
                for file in version_value:
                    if not isOnWhitelist(args, file):
                        newName = str(
                            argOutFolder.joinpath(
                                "["
                                + titleID_key
                                + "][v"
                                + str(version_key)
                                + "]"
                                + Path(file).suffix
                            )
                        )
                        if args.undupe_hardlink:
                            if Path(newName).is_file():
                                if Path(file).samefile(Path(newName)):
                                    Print.info(newName, "HARDLINK SKIPPED")
                                else:
                                    Print.info(
                                        newName, "HARDLINK ERROR_ALREADY_EXIST"
                                    )
                            else:
                                if args.undupe_dryrun:
                                    Print.info(
                                        "os.link(" + file + ", " + newName,
                                        "DRYRUN HARDLINK",
                                    )
                                else:
                                    Print.info(
                                        "os.link(" + file + ", " + newName,
                                        "HARDLINK",
                                    )
                                    os.link(file, newName)
                        if args.undupe_rename:
                            if Path(newName).is_file():
                                if Path(file).samefile(Path(newName)):
                                    Print.info(newName, "RENAME SKIPPED")
                                else:
                                    Print.info(
                                        newName, "RENAME ERROR_ALREADY_EXIST"
                                    )
                            else:
                                if args.undupe_dryrun:
                                    Print.info(
                                        "os.rename(" + file + ", " + newName,
                                        "DRYRUN RENAME",
                                    )
                                else:
                                    Print.info(
                                        "os.rename(" + file + ", " + newName,
                                        "RENAME",
                                    )
                                    os.rename(file, newName)
