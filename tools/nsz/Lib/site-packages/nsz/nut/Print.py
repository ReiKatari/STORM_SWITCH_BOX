import sys
import time
import json
import threading
from sys import argv
from multiprocessing.process import current_process
from nsz.ParseArguments import ParseArguments
from traceback import print_exc

enableInfo = True
enableError = True
enableWarning = True
enableDebug = False
silent = False
# Turning on machine output will convert all levels to JSON.
machineReadableOutput = False
minimalOutput = False
lastProgress = ""
lastMinimalProgress = ""
lastMinimalProgressLength = 0
spinnerFrames = ["|", "/", "-", "\\"]
spinnerIndex = 0

# Guards stdout so the heartbeat thread can't interleave with a write from
# the main thread (or another process pausing via pleaseNoPrint) mid-line.
_stdoutLock = threading.Lock()

heartbeatIntervalSeconds = 5
_heartbeatThread = None
_heartbeatStop = None

if len(argv) > 1:
    # We must re-parse the command line parameters here because this module
    # is re-imported in multiple modules which resets the variables each import.
    args = ParseArguments.parse(for_nutPrint=True)

    # Does the user want machine readable output?
    if args.machine_readable:
        machineReadableOutput = True

    # Minimal output suppresses normal info logs. Errors and warnings are kept.
    if args.minimal_output:
        minimalOutput = True
        enableInfo = False


def _write(line):
    with _stdoutLock:
        sys.stdout.write(line + "\n")
        sys.stdout.flush()

def silly(s, action=None):
    if silent or not enableInfo:
        return

    if machineReadableOutput:
        return

    info(s, action)

def info(s, action=None, pleaseNoPrint=None):
    if silent or not enableInfo:
        return

    if machineReadableOutput:
        payload = {"type": "info", "message": s}
        if action is not None:
            payload["action"] = action
        line = json.dumps(payload)
    elif action is not None:
        line = f"[{action}] {s}"
    else:
        line = s

    if pleaseNoPrint is None:
        _write(line)
    else:
        while pleaseNoPrint.value() > 0:
            time.sleep(0.01)
        pleaseNoPrint.increment()
        _write(line)
        pleaseNoPrint.decrement()


def error(errorCode, s):
    if silent or not enableError:
        return
    if machineReadableOutput:
        s = json.dumps({"type": "error", "code": errorCode, "message": s})

    _write(s)


def warning(s):
    if silent or not enableWarning:
        return
    if machineReadableOutput:
        s = json.dumps({"type": "warning", "message": s})

    _write(s)


def summary(errors):
    """Final machine-readable status line emitted once processing has finished."""
    if not machineReadableOutput:
        return
    _write(
        json.dumps(
            {
                "type": "summary",
                "success": len(errors) == 0,
                "errorCount": len(errors),
                "errors": errors,
            }
        )
    )


def startHeartbeat(intervalSeconds=None):
    """Emit a periodic JSON heartbeat line so a wrapper can detect a hung process."""
    global _heartbeatThread, _heartbeatStop
    if not machineReadableOutput or _heartbeatThread is not None:
        return
    interval = heartbeatIntervalSeconds if intervalSeconds is None else intervalSeconds
    _heartbeatStop = threading.Event()

    def _run():
        while not _heartbeatStop.wait(interval):
            if silent:
                continue
            _write(json.dumps({"type": "heartbeat", "time": time.time()}))

    _heartbeatThread = threading.Thread(target=_run, daemon=True)
    _heartbeatThread.start()


def stopHeartbeat():
    global _heartbeatThread, _heartbeatStop
    if _heartbeatThread is None:
        return
    _heartbeatStop.set()
    _heartbeatThread.join(timeout=1)
    _heartbeatThread = None


def debug(s):
    if silent or not enableDebug:
        return
    if not machineReadableOutput:
        sys.stdout.write(s + "\n")


def exception():
    if not machineReadableOutput:
        print_exc()


def progress(job, s):
    global lastProgress
    global lastMinimalProgress
    global lastMinimalProgressLength
    global spinnerIndex

    if machineReadableOutput:
        data = s if isinstance(s, dict) else {}
        if job == "Complete":
            payload = {"type": "complete", "file": data.get("filePath", "")}
        else:
            payload = {"type": "progress", "step": data.get("step", job)}
            if data.get("filePath"):
                payload["file"] = data["filePath"]
            total = data.get("sourceSize")
            current = data.get("processed")
            if total is not None:
                payload["totalWrite"] = total
            if current is not None:
                payload["currentWrite"] = current
            if total is not None and current is not None and total > 0:
                payload["percent"] = round(current * 100 / total, 1)
            if "readSize" in data:
                payload["totalRead"] = data["readSize"]
            if "read" in data:
                payload["currentRead"] = data["read"]

        line = json.dumps(payload)
        if line != lastProgress:
            _write(line)
            lastProgress = line
        return

    if minimalOutput:
        # Keep minimal output stable by avoiding worker-process duplicates.
        if current_process().name != "MainProcess":
            return
        if job == "Complete":
            minimalLine = "100% Done"
        elif isinstance(s, dict) and "sourceSize" in s and "processed" in s:
            total = s["sourceSize"]
            processed = s["processed"]
            percentage = 0 if total <= 0 else min(100, int(processed * 100 / total))
            step = s["step"] if "step" in s else job
            spinner = spinnerFrames[spinnerIndex]
            spinnerIndex = (spinnerIndex + 1) % len(spinnerFrames)
            minimalLine = f"{spinner} {percentage}% {step}"
        else:
            return
        if job != "Complete" or minimalLine != lastMinimalProgress:
            padding = ""
            if lastMinimalProgressLength > len(minimalLine):
                padding = " " * (lastMinimalProgressLength - len(minimalLine))
            if job == "Complete":
                sys.stdout.write("\r" + minimalLine + padding + "\n")
                lastMinimalProgressLength = 0
                lastMinimalProgress = ""
            else:
                sys.stdout.write("\r" + minimalLine + padding)
                lastMinimalProgressLength = len(minimalLine)
                lastMinimalProgress = minimalLine
            sys.stdout.flush()
        return

    if job == "Complete":
        filePath = s.get("filePath", "") if isinstance(s, dict) else ""
        if filePath:
            sys.stdout.write("[DONE]       {0}\n".format(filePath))
        else:
            sys.stdout.write("[DONE]\n")
        sys.stdout.flush()


def isMinimalOutput():
    return minimalOutput
