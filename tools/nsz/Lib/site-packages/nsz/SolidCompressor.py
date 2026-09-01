import sys
from traceback import format_exc
from nsz.SectionFs import isNcaPacked, sortedFs
from nsz.Fs import Pfs0, Hfs0, Nca, Nsp, Type, Xci
from nsz.nut import Keys, Print
from zstandard import (
    FLUSH_FRAME,
    ZstdCompressionParameters,
    ZstdCompressor,
)

INCOMPRESSIBLE_HEADER_SIZE = 0x4000
CHUNK_SZ = 0x1000000


def _validateSectionCryptoData(section, sourcePath):
    if not isinstance(section.cryptoKey, (bytes, bytearray)):
        raise ValueError(
            "NCA cannot be compressed: missing title key for {0} (invalid section key).".format(
                sourcePath
            )
        )
    if len(section.cryptoKey) != 0x10:
        raise ValueError(
            "NCA cannot be compressed: invalid section key length for {0}.".format(
                sourcePath
            )
        )
    if not isinstance(section.cryptoCounter, (bytes, bytearray)):
        raise ValueError(
            "NCA cannot be compressed: invalid section counter for {0}.".format(
                sourcePath
            )
        )
    if len(section.cryptoCounter) != 0x10:
        raise ValueError(
            "NCA cannot be compressed: invalid section counter length for {0}.".format(
                sourcePath
            )
        )


def solidCompress(
    filePath,
    compressionLevel,
    keep,
    fixPadding,
    useLongDistanceMode,
    outputDir,
    threads,
    statusReport,
    id,
    pleaseNoPrint,
):
    if filePath.suffix == ".nsp":
        return solidCompressNsp(
            filePath,
            compressionLevel,
            keep,
            fixPadding,
            useLongDistanceMode,
            outputDir,
            threads,
            statusReport,
            id,
            pleaseNoPrint,
        )
    elif filePath.suffix == ".xci":
        return solidCompressXci(
            filePath,
            compressionLevel,
            keep,
            fixPadding,
            useLongDistanceMode,
            outputDir,
            threads,
            statusReport,
            id,
            pleaseNoPrint,
        )


def processContainer(
    readContainer,
    writeContainer,
    compressionLevel,
    keep,
    useLongDistanceMode,
    threads,
    statusReport,
    id,
    pleaseNoPrint,
):
    for nspf in readContainer:
        if not keep:
            if isinstance(nspf, Nca.Nca) and nspf.header is not None:
                if nspf.header.contentType == Type.Content.DATA:
                    Print.info(
                        "Delta fragment {0}".format(nspf._path),
                        "SKIPPED",
                        pleaseNoPrint=pleaseNoPrint,
                    )
                    continue
        if (
            isinstance(nspf, Nca.Nca)
            and nspf.header is not None
            and nspf.size is not None
            and (
                nspf.header.contentType == Type.Content.PROGRAM
                or nspf.header.contentType == Type.Content.PUBLICDATA
            )
            and nspf.size > INCOMPRESSIBLE_HEADER_SIZE
        ):
            if isNcaPacked(nspf):
                assert nspf._path is not None
                offsetFirstSection = sortedFs(nspf)[0].offset
                newFileName = nspf._path[0:-1] + "z"

                def reportProgress(written, total):
                    Print.progress(
                        "Progress",
                        {
                            "sourceSize": total,
                            "processed": written,
                            "read": nspf.tell(),
                            "readSize": nspf.size,
                            "step": "Compressing",
                            "filePath": newFileName,
                        },
                    )

                with writeContainer.add(newFileName, nspf.size, pleaseNoPrint) as f:
                    start = f.tell()

                    nspf.seek(0)
                    f.write(nspf.read(INCOMPRESSIBLE_HEADER_SIZE))

                    sections = []
                    for fs in sortedFs(nspf):
                        sections += fs.getEncryptionSections()

                    if len(sections) == 0:
                        raise Exception("NCA can't be decrypted. Outdated keys.txt?")

                    header = b"NCZSECTN"
                    header += len(sections).to_bytes(8, "little")

                    i = 0
                    for fs in sections:
                        i += 1
                        _validateSectionCryptoData(fs, nspf._path)
                        header += fs.offset.to_bytes(8, "little")
                        header += fs.size.to_bytes(8, "little")
                        header += fs.cryptoType.to_bytes(8, "little")
                        header += b"\x00" * 8
                        header += fs.cryptoKey
                        header += fs.cryptoCounter

                    f.write(header)

                    decompressedBytes = INCOMPRESSIBLE_HEADER_SIZE

                    statusReport[id] = [0, 0, nspf.size, "Compressing"]
                    reportProgress(0, nspf.size)

                    partitions = []
                    if offsetFirstSection - INCOMPRESSIBLE_HEADER_SIZE > 0:
                        partitions.append(
                            nspf.partition(
                                offset=INCOMPRESSIBLE_HEADER_SIZE,
                                size=offsetFirstSection - INCOMPRESSIBLE_HEADER_SIZE,
                                cryptoType=Type.Crypto.CTR.NONE,
                                autoOpen=True,
                            )
                        )
                    for section in sections:
                        # Print.info('offset: %x\t\tsize: %x\t\ttype: %d\t\tiv%s' % (section.offset, section.size, section.cryptoType, str(hx(section.cryptoCounter))), pleaseNoPrint)
                        partitions.append(
                            nspf.partition(
                                offset=section.offset,
                                size=section.size,
                                cryptoType=section.cryptoType,
                                cryptoKey=section.cryptoKey,
                                cryptoCounter=bytearray(section.cryptoCounter),
                                autoOpen=True,
                            )
                        )
                    if INCOMPRESSIBLE_HEADER_SIZE - offsetFirstSection > 0:
                        partitions[0].seek(
                            INCOMPRESSIBLE_HEADER_SIZE - offsetFirstSection
                        )

                    partNr = 0
                    statusReport[id] = [nspf.tell(), f.tell(), nspf.size, "Compressing"]
                    reportProgress(f.tell(), nspf.size)
                    if threads > 1:
                        params = ZstdCompressionParameters.from_level(
                            compressionLevel,
                            enable_ldm=useLongDistanceMode,
                            threads=threads,
                        )
                        cctx = ZstdCompressor(compression_params=params)
                    else:
                        params = ZstdCompressionParameters.from_level(
                            compressionLevel, enable_ldm=useLongDistanceMode
                        )
                        cctx = ZstdCompressor(compression_params=params)

                    compressor = cctx.stream_writer(f)

                    while True:
                        buffer = partitions[partNr].read(CHUNK_SZ)
                        while len(buffer) < CHUNK_SZ and partNr < len(partitions) - 1:
                            partitions[partNr].close()
                            partitions[partNr] = None
                            partNr += 1
                            buffer += partitions[partNr].read(CHUNK_SZ - len(buffer))
                        if len(buffer) == 0:
                            break
                        compressor.write(buffer)

                        decompressedBytes += len(buffer)
                        statusReport[id] = [
                            nspf.tell(),
                            f.tell(),
                            nspf.size,
                            "Compressing",
                        ]
                        reportProgress(f.tell(), nspf.size)
                        sys.stdout.flush()
                    partitions[partNr].close()
                    partitions[partNr] = None

                    compressor.flush(FLUSH_FRAME)
                    written = f.tell() - start
                    statusReport[id] = [nspf.tell(), f.tell(), nspf.size, "Compressing"]
                    reportProgress(written, written)

                    Print.info(
                        "Compressed {0}% {1} -> {2}  - {3}".format(
                            written * 100 / nspf.size,
                            decompressedBytes,
                            written,
                            nspf._path,
                        ),
                        pleaseNoPrint=pleaseNoPrint,
                    )
                    writeContainer.resize(newFileName, written)
                    continue
            else:
                Print.info("Skipping not packed {0}".format(nspf._path))

        with writeContainer.add(nspf._path, nspf.size, pleaseNoPrint) as f:
            nspf.seek(0)
            while not nspf.eof():
                buffer = nspf.read(CHUNK_SZ)
                f.write(buffer)


def solidCompressNsp(
    filePath,
    compressionLevel,
    keep,
    fixPadding,
    useLongDistanceMode,
    outputDir,
    threads,
    statusReport,
    id,
    pleaseNoPrint,
):
    filePath = filePath.resolve()
    container = Nsp.Nsp()
    container.open(str(filePath), "rb")
    nszPath = outputDir.joinpath(filePath.stem + ".nsz")

    Print.info(
        f"Solid compressing (level {compressionLevel}{' ldm' if useLongDistanceMode else ''}) {filePath} -> {nszPath}",
        pleaseNoPrint=pleaseNoPrint,
    )

    failed = False
    try:
        with Pfs0.Pfs0Stream(
            container.getPaddedHeaderSize()
            if fixPadding
            else container.getFirstFileOffset(),
            None if fixPadding else container.getStringTableSize(),
            str(nszPath),
        ) as nsp:
            processContainer(
                container,
                nsp,
                compressionLevel,
                keep,
                useLongDistanceMode,
                threads,
                statusReport,
                id,
                pleaseNoPrint,
            )

        Print.progress("Complete", {"filePath": str(nszPath)})
        sys.stdout.flush()
    except BaseException as ex:
        if isinstance(ex, Keys.MissingKeyError):
            raise
        failed = True
        if not isinstance(ex, KeyboardInterrupt):
            Print.error(500, format_exc())
        if nszPath.is_file():
            nszPath.unlink()

    container.close()
    if failed:
        return None
    return nszPath


def align0x200(n):
    return 0x200 - n % 0x200


def solidCompressXci(
    filePath,
    compressionLevel,
    keep,
    fixPadding,
    useLongDistanceMode,
    outputDir,
    threads,
    statusReport,
    id,
    pleaseNoPrint,
):
    filePath = filePath.resolve()
    container = Xci.Xci()
    container.open(str(filePath), "rb")
    xczPath = outputDir.joinpath(filePath.stem + ".xcz")

    Print.info(
        f"Solid compressing (level {compressionLevel}{' ldm' if useLongDistanceMode else ''}) {filePath} -> {xczPath}",
        pleaseNoPrint=pleaseNoPrint,
    )

    failed = False
    try:
        # need filepath to copy XCI container settings
        assert container.hfs0 is not None
        with Xci.XciStream(str(xczPath), originalXciPath=filePath) as xci:
            assert xci.hfs0 is not None
            for partitionIn in container.hfs0:
                xci.hfs0.written = False
                hfsPartitionOut = xci.hfs0.add(partitionIn._path, 0, pleaseNoPrint)
                with Hfs0.Hfs0Stream(hfsPartitionOut, xci.f) as partitionOut:
                    if keep or partitionIn._path == "secure":
                        processContainer(
                            partitionIn,
                            partitionOut,
                            compressionLevel,
                            keep,
                            useLongDistanceMode,
                            threads,
                            statusReport,
                            id,
                            pleaseNoPrint,
                        )
                    alignedSize = partitionOut.actualSize + align0x200(
                        partitionOut.actualSize
                    )
                    xci.hfs0.resize(partitionIn._path, alignedSize)
                    Print.info(
                        f"{partitionIn._path} to {hex(alignedSize)}"
                        "RESIZE"
                    )
                    xci.hfs0.addpos += alignedSize

        Print.progress("Complete", {"filePath": str(xczPath)})
        sys.stdout.flush()
    except BaseException as ex:
        if isinstance(ex, Keys.MissingKeyError):
            raise
        failed = True
        if not isinstance(ex, KeyboardInterrupt):
            Print.error(501, format_exc())
        if xczPath.is_file():
            xczPath.unlink()

    container.close()
    if failed:
        return None
    return xczPath
