from nsz.Fs import Pfs0, Nca, Nsp, Type, Xci
from os import remove
from pathlib import Path
from re import search
from nsz.nut import Print
from nsz.PathTools import changeExtension, expandFiles, isGame, isXciXcz, targetExtension


def ExtractHashes(container):
    fileHashes = set()
    for nspf in container:
        if (
            isinstance(nspf, Nca.Nca)
            and nspf.header is not None
            and nspf.header.contentType == Type.Content.META
        ):
            for section in nspf:
                if isinstance(section, Pfs0.Pfs0):
                    Cnmt = section.getCnmt()
                    for entry in Cnmt.contentEntries:
                        fileHashes.add(entry.hash.hex())
    return fileHashes


def ExtractTitleIDAndVersion(gamePath, args=None):
    titleId = ""
    version = -1
    gameName = Path(gamePath).name
    titleIdResult = search(r"0100[0-9A-Fa-f]{12}", gameName)
    if args is None or not args.alwaysParseCnmt:
        if titleIdResult:
            titleId = titleIdResult.group().upper()
        versionResult = search(r"\[v\d+\]", gameName)
        if versionResult:
            version = int(versionResult.group()[2:-1])
        if titleId != "" and version > -1 and version % 65536 == 0:
            return (titleId, version)
    gamePath = Path(gamePath).resolve()
    if isXciXcz(gamePath):
        container = Xci.Xci()
        container.open(str(gamePath), "rb")
        assert container.hfs0 is not None
        iterContainer = container.hfs0["secure"]
    else:
        container = Nsp.Nsp()
        container.open(str(gamePath), "rb")
        iterContainer = container
    try:
        for nspf in iterContainer:
            if (
                isinstance(nspf, Nca.Nca)
                and nspf.header is not None
                and nspf.header.contentType == Type.Content.META
            ):
                for section in nspf:
                    if isinstance(section, Pfs0.Pfs0):
                        Cnmt = section.getCnmt()
                        assert Cnmt.titleId is not None and Cnmt.version is not None
                        titleId = Cnmt.titleId.upper()
                        version = Cnmt.version
    finally:
        container.close()
    if titleId != "" and version > -1 and version % 65536 == 0:
        return (titleId, version)
    return None


def CreateTargetDict(targetFolder, args, extension, filesAtTarget=None, alreadyExists=None):
    if filesAtTarget is None:
        filesAtTarget = {}
    if alreadyExists is None:
        alreadyExists = {}
    for filePath in expandFiles(targetFolder):
        try:
            filePath_str = str(filePath)
            if (
                isGame(filePath)
                or filePath.suffix == ".nspz"
                or filePath.suffix == ".nsx"
            ) and (extension is None or filePath.suffix == extension):
                Print.info("{0}".format(filePath), "CHECKING")
                Print.info("Extracting TitleID/Version from {0}".format(filePath.name), "EXTRACT")
                filesAtTarget[filePath.name.lower()] = filePath_str
                extractedIdVersion = ExtractTitleIDAndVersion(filePath, args)
                if extractedIdVersion is None:
                    Print.error(
                        300,
                        'Failed to extract TitleID/Version from both filename "{0}" and Cnmt - Outdated keys.txt?'.format(
                            Path(filePath).name
                        ),
                    )
                    continue
                titleID, version = extractedIdVersion
                titleIDEntry = alreadyExists.get(titleID)
                if titleIDEntry is None:
                    titleIDEntry = {version: [filePath_str]}
                elif version not in titleIDEntry:
                    titleIDEntry[version] = [filePath_str]
                else:
                    titleIDEntry[version].append(filePath_str)
                alreadyExists[titleID] = titleIDEntry
                Print.info("TitleId: {0} Version: {1}".format(titleID, version), "FOUND")
        except BaseException as e:
            Print.info("")
            Print.exception()
            Print.error(301, "Error: " + str(e))
    return (filesAtTarget, alreadyExists)


def AllowedToWriteOutfile(filePath, targetFileExtension, targetDict, args):
    (filesAtTarget, alreadyExists) = targetDict
    extractedIdVersion = ExtractTitleIDAndVersion(filePath, args)
    if extractedIdVersion is None:
        Print.error(
            300,
            'Failed to extract TitleID/Version from both filename "{0}" and Cnmt - Outdated keys.txt?'.format(
                Path(filePath).name
            ),
        )
        return fileNameCheck(
            filePath,
            targetFileExtension,
            filesAtTarget,
            args.rm_old_version,
            args.overwrite,
        )
    (titleIDExtracted, versionExtracted) = extractedIdVersion
    titleIDEntry = alreadyExists.get(titleIDExtracted)

    if titleIDEntry is not None:
        DuplicateEntriesToDelete = []
        OutdatedEntriesToDelete = []
        newerVersionEntry = None
        for versionEntry in titleIDEntry.keys():
            if versionEntry == versionExtracted:
                if args.overwrite:
                    DuplicateEntriesToDelete.append(versionEntry)
                else:
                    Print.warning("A file with the same output name already exists.")
                    return False
            elif versionEntry < versionExtracted:
                if args.rm_old_version:
                    OutdatedEntriesToDelete.append(versionEntry)
            else:  # versionEntry > versionExtracted
                if args.rm_old_version:
                    newerVersionEntry = versionEntry
        if newerVersionEntry is not None:
            Print.warning(
                "{0} with a the same ID and newer version already exists in the output directory.\n"
                "If you want to process it do not use --rm-old-version!".format(
                    titleIDEntry[newerVersionEntry]
                )
            )
            return False

        for versionEntry in DuplicateEntriesToDelete:
            for delFilePath in titleIDEntry[versionEntry]:
                Print.info("Delete duplicate: {0}".format(delFilePath))
                remove(delFilePath)
                del filesAtTarget[Path(delFilePath).name.lower()]
            del titleIDEntry[versionEntry]
        for versionEntry in OutdatedEntriesToDelete:
            for delFilePath in titleIDEntry[versionEntry]:
                Print.info("Delete outdated version: {0}".format(delFilePath))
                remove(delFilePath)
                del filesAtTarget[Path(delFilePath).name.lower()]
            del titleIDEntry[versionEntry]

    return fileNameCheck(
        filePath,
        targetFileExtension,
        filesAtTarget,
        args.rm_old_version,
        args.overwrite,
    )


def fileNameCheck(filePath, targetFileExtension, filesAtTarget, removeOld, overwrite):
    outFile = str(Path(changeExtension(filePath, targetFileExtension)).name).lower()
    filePath = filesAtTarget.get(outFile)
    if filePath is None:
        return True
    if overwrite:
        remove(filePath)
        return True
    Print.warning("A file with the same output name already exists.")
    return False


def delete_source_file(source_file_path, outFolder):
    filePath = Path(source_file_path)
    if filePath.exists():
        resultFile = Path(targetExtension(outFolder.joinpath(filePath.name)))
        if resultFile.exists():
            Print.info("Source file {0}".format(source_file_path), "DELETING")
            remove(source_file_path)
        else:
            Print.warning(
                "[WARNING]    Skipped deleting source file because target file doesn't exist"
            )
    else:
        Print.warning("[WARNING]    {0} was already removed.".format(source_file_path))
