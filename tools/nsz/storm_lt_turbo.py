#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
STORM SWITCH BOX - Embedded Native C++ libtorrent-rasterbar Turbo Engine (Tier 2)
Provides full C++ throughput (uTP BEP-29, BEP-55 holepunch, HTTP/UDP/DHT peer injection,
and fast BEP-9 ut_metadata resolution) without requiring external qBittorrent.
"""

import argparse
import ctypes
import json
import os
import queue
import re
import shutil
import socket
import sys
import tempfile
import threading
import time
import libtorrent as lt

try:
    import warnings
    warnings.filterwarnings("ignore", category=DeprecationWarning)
except Exception:
    pass

DEFAULT_PUBLIC_TRACKERS = [
    "udp://tracker.opentrackr.org:1337/announce",
    "udp://open.stealth.si:80/announce",
    "udp://tracker.torrent.eu.org:451/announce",
    "udp://exodus.desync.com:6969/announce",
    "udp://open.demonii.com:1337/announce",
    "udp://tracker.dler.org:6969/announce",
    "udp://open.dstud.io:6969/announce",
    "udp://explodie.org:6969/announce",
    "udp://p4p.arenabg.com:1337/announce",
    "udp://opentor.org:2710/announce",
    "udp://tracker.theoks.net:6969/announce",
    "udp://retracker.hotplug.ru:2710/announce",
    "udp://bt1.archive.org:6969/announce",
    "udp://bt2.archive.org:6969/announce",
    "udp://tracker.bittor.pw:1337/announce",
    "udp://tracker.tiny-vps.com:6969/announce",
    "https://tracker.opentrackr.org:443/announce",
    "https://tracker.gbitt.info:443/announce",
    "https://tracker.tamersunion.org:443/announce",
    "https://tr.burnabyhighstar.com:443/announce",
]


def build_turbo_settings(listen_port=44859, metadata_only=False):
    default_keys = set(lt.default_settings().keys())
    base = {}
    try:
        base = dict(lt.high_performance_seed())
    except Exception:
        base = {}

    port = int(listen_port) if listen_port and 1024 <= int(listen_port) <= 65535 else 44859
    desired = {
        "listen_interfaces": f"0.0.0.0:{port},[::]:{port}",
        "enable_outgoing_utp": True,
        "enable_incoming_utp": True,
        "enable_outgoing_tcp": True,
        "enable_incoming_tcp": True,
        "enable_dht": True,
        "enable_lsd": True,
        "enable_upnp": not metadata_only,
        "enable_natpmp": not metadata_only,
        "announce_to_all_trackers": True,
        "announce_to_all_tiers": True,
        "mixed_mode_algorithm": 1,  # peer_proportional: never throttle uTP seeds
        "choking_algorithm": 0,
        "seed_choking_algorithm": 1,
        "smooth_connects": False,
        "connection_speed": 2000,
        "torrent_connect_boost": 255,
        "connections_limit": 10000,
        "max_peerlist_size": 10000,
        "active_downloads": -1,
        "active_seeds": -1,
        "active_limit": -1,
        "active_checking": -1,
        "unchoke_slots_limit": -1,
        "max_out_request_queue": 8000,
        "max_allowed_in_request_queue": 8000,
        "request_queue_time": 5,
        "strict_end_game_mode": False,
        "whole_pieces_threshold": 20,
        "send_socket_buffer_size": 0,
        "recv_socket_buffer_size": 0,
        "utp_gain_factor": 5000,
        "utp_target_delay": 200,
        "allowed_fast_set_size": 5,
        "suggest_mode": 0,
        "download_rate_limit": 0,
        "upload_rate_limit": 0,
        "peer_turnover": 0,
        "piece_extent_affinity": True,
        "coalesce_reads": True,
        "coalesce_writes": True,
        "rate_limit_ip_overhead": False,
        "max_queued_disk_bytes": 512 * 1024 * 1024,
        "send_buffer_watermark": 67108864,
        "send_buffer_low_watermark": 8388608,
        "send_buffer_watermark_factor": 200,
        "allow_multiple_connections_per_ip": True,
        "min_reconnect_time": 1,
        "max_failcount": 10,
        "peer_connect_timeout": 6,
        "request_timeout": 25,
        "peer_timeout": 45,
        "in_enc_policy": 1,
        "out_enc_policy": 1,
        "prefer_rc4": True,
        "peer_fingerprint": "-qB5000-",
        "user_agent": "qBittorrent/5.0.0",
        "aio_threads": 16,
        "hashing_threads": 8,
        "dht_bootstrap_nodes": (
            "router.bittorrent.com:6881,"
            "dht.transmissionbt.com:6881,"
            "router.utorrent.com:6881,"
            "dht.aelitis.com:6881,"
            "dht.libtorrent.org:25401"
        ),
    }
    base.update(desired)
    return {k: v for k, v in base.items() if k in default_keys}


def is_parent_alive(parent_pid):
    if not parent_pid or parent_pid <= 0:
        return True
    if os.name == "nt":
        try:
            SYNCHRONIZE = 0x00100000
            WAIT_TIMEOUT = 0x00000102
            kernel32 = ctypes.windll.kernel32
            h = kernel32.OpenProcess(SYNCHRONIZE, False, int(parent_pid))
            if not h:
                return False
            try:
                res = kernel32.WaitForSingleObject(h, 0)
                return res == WAIT_TIMEOUT
            finally:
                kernel32.CloseHandle(h)
        except Exception:
            return True
    else:
        try:
            os.kill(int(parent_pid), 0)
            return True
        except OSError:
            return False


def is_valid_ip(host_part):
    try:
        socket.inet_pton(socket.AF_INET, host_part)
        return True
    except OSError:
        pass
    try:
        socket.inet_pton(socket.AF_INET6, host_part)
        return True
    except OSError:
        return False


def parse_peer_endpoint(entry):
    entry = (entry or "").lstrip("\ufeff").strip()
    if not entry:
        return None
    if entry.startswith("["):
        m = re.match(r"^\[([^\]]+)\]:(\d+)$", entry)
        if not m:
            return None
        host_part, port_str = m.group(1).strip(), m.group(2)
        if not is_valid_ip(host_part):
            return None
        port_num = int(port_str)
        return (host_part, port_num) if 0 < port_num <= 65535 else None

    if ":" not in entry:
        return None

    # Avoid confusing raw IPv6 without port with host:port
    if entry.count(":") > 1 and is_valid_ip(entry):
        return None

    host_part, port_part = entry.rsplit(":", 1)
    host_part = host_part.lstrip("\ufeff").strip()
    if not host_part or not port_part.isdigit():
        return None
    port_num = int(port_part)
    if not (0 < port_num <= 65535):
        return None
    if not is_valid_ip(host_part):
        return None
    return (host_part, port_num)


def flush_torrent_cache(ses, handle, timeout_sec=5.0):
    try:
        ses.set_alert_mask(lt.alert.category_t.storage_notification)
    except Exception:
        pass
    try:
        handle.flush_cache()
    except Exception:
        return
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        try:
            alerts = ses.pop_alerts()
            for a in alerts:
                if type(a).__name__ == "cache_flushed_alert":
                    return
        except Exception:
            return
        time.sleep(0.05)


def merge_trackers(existing_trackers):
    merged = []
    seen = set()
    has_tru = False
    for tr in (existing_trackers or []):
        url = str(tr.url if hasattr(tr, "url") else tr).strip()
        if not url:
            continue
        low = url.lower()
        if "retracker.local" in low:
            continue
        if "t-ru.org" in low:
            has_tru = True
        if url not in seen:
            seen.add(url)
            merged.append(url)
            if len(merged) >= 36:
                return merged

    if not has_tru:
        for tr in (
            "http://bt.t-ru.org/ann?magnet",
            "http://bt2.t-ru.org/ann?magnet",
            "http://bt3.t-ru.org/ann?magnet",
            "http://bt4.t-ru.org/ann?magnet",
        ):
            if tr not in seen:
                seen.add(tr)
                merged.append(tr)

    for tr in DEFAULT_PUBLIC_TRACKERS:
        if tr not in seen:
            seen.add(tr)
            merged.append(tr)
            if len(merged) >= 36:
                break
    return merged[:36]


def load_dht_state_only(ses, dht_path):
    if not dht_path or not os.path.isfile(dht_path):
        return
    try:
        with open(dht_path, "rb") as df:
            raw = lt.bdecode(df.read())
        if isinstance(raw, dict):
            dht_only = {b"dht state": raw[b"dht state"]} if b"dht state" in raw else raw
            if hasattr(ses, "load_state"):
                try:
                    ses.load_state(dht_only, lt.save_state_flags_t.save_dht_state)
                except TypeError:
                    ses.load_state(dht_only)
    except Exception:
        pass


def save_dht_state_only(ses, dht_path):
    if not dht_path:
        return
    try:
        state = ses.save_state(lt.save_state_flags_t.save_dht_state)
        if not isinstance(state, dict) or b"dht state" not in state:
            return
        blob = lt.bencode(state)
        if not blob or len(blob) <= 16:
            return
        parent = os.path.dirname(dht_path)
        if parent:
            os.makedirs(parent, exist_ok=True)
        tmp_path = f"{dht_path}.{os.getpid()}.tmp"
        with open(tmp_path, "wb") as df:
            df.write(blob)
        os.replace(tmp_path, dht_path)
    except Exception:
        pass


def apply_clean_trackers(handle, trackers):
    if not trackers:
        return
    try:
        entries = [lt.announce_entry(u) for u in trackers if u]
        if entries:
            handle.replace_trackers(entries)
    except Exception:
        pass


def save_torrent_info_file(ti, out_path):
    if ti is None or not out_path:
        return False
    try:
        tparent = os.path.dirname(out_path)
        if tparent:
            os.makedirs(tparent, exist_ok=True)
        if hasattr(ti, "info_section"):
            raw_tor = b"d4:info" + bytes(ti.info_section()) + b"e"
        else:
            ct = lt.create_torrent(ti)
            raw_tor = lt.bencode(ct.generate())
        tmp_tor = f"{out_path}.{os.getpid()}.tmp"
        with open(tmp_tor, "wb") as tf:
            tf.write(raw_tor)
        os.replace(tmp_tor, out_path)
        return True
    except Exception:
        return False


def _handle_info_hash_hex(handle):
    try:
        if hasattr(handle, "info_hashes"):
            ihs = handle.info_hashes()
            if ihs.has_v1():
                return str(ihs.v1).lower()
            if ihs.has_v2():
                return str(ihs.v2).lower()
    except Exception:
        pass
    try:
        return str(handle.info_hash()).lower()
    except Exception:
        return ""


def _resolve_seed_save_dir(ti, save_dir, skip_set):
    if ti is None or not save_dir or not os.path.isdir(save_dir):
        return save_dir
    try:
        fs = ti.files()
        num_files = fs.num_files()
        if num_files <= 0:
            return save_dir

        probe_idx = next((i for i in range(num_files) if i not in skip_set), 0)
        rel = fs.file_path(probe_idx).replace("\\", "/").lstrip("/")
        direct_candidate = os.path.join(save_dir, rel.replace("/", os.sep))
        if os.path.exists(direct_candidate):
            return save_dir

        if "/" in rel:
            top_prefix, stripped = rel.split("/", 1)
            stripped_candidate = os.path.join(save_dir, stripped.replace("/", os.sep))
            if os.path.exists(stripped_candidate):
                norm_save = os.path.normpath(save_dir)
                if os.path.basename(norm_save).lower() == top_prefix.lower():
                    return os.path.dirname(norm_save)
                # Files live flat inside save_dir without the torrent root folder prefix: remap paths
                for i in range(num_files):
                    orig_p = fs.file_path(i).replace("\\", "/").lstrip("/")
                    if orig_p.lower().startswith(top_prefix.lower() + "/"):
                        new_p = orig_p.split("/", 1)[1]
                        try:
                            ti.rename_file(i, new_p)
                        except Exception:
                            pass
                return save_dir
    except Exception:
        pass
    return save_dir


def merge_seed_trackers(existing_trackers):
    merged = []
    seen = set()
    has_tru = False
    for tr in (existing_trackers or []):
        url = str(tr.url if hasattr(tr, "url") else tr).strip()
        if not url:
            continue
        low = url.lower()
        if "retracker.local" in low:
            continue
        if "t-ru.org" in low:
            has_tru = True
        if url not in seen:
            seen.add(url)
            merged.append(url)
            if len(merged) >= 32:
                return merged

    if not has_tru:
        for tr in (
            "http://bt.t-ru.org/ann?magnet",
            "http://bt2.t-ru.org/ann?magnet",
            "http://bt3.t-ru.org/ann?magnet",
            "http://bt4.t-ru.org/ann?magnet",
        ):
            if tr not in seen:
                seen.add(tr)
                merged.append(tr)

    for tr in DEFAULT_PUBLIC_TRACKERS:
        if tr not in seen:
            seen.add(tr)
            merged.append(tr)
            if len(merged) >= 32:
                break
    return merged[:32]


def run_seed_daemon(ses, args):
    seed_cfg = build_turbo_settings(args.listen_port, metadata_only=False)
    seed_cfg["enable_lsd"] = True
    seed_cfg["enable_dht"] = True
    seed_cfg["enable_upnp"] = True
    seed_cfg["enable_natpmp"] = True
    seed_cfg["active_downloads"] = -1
    seed_cfg["active_seeds"] = -1
    seed_cfg["active_limit"] = -1
    seed_cfg["unchoke_slots_limit"] = -1
    seed_cfg["connections_limit"] = 10000
    seed_cfg["upload_rate_limit"] = 0
    seed_cfg["download_rate_limit"] = 0
    seed_cfg["seed_choking_algorithm"] = 1
    try:
        seed_cfg["alert_mask"] = (
            lt.alert.category_t.storage_notification
            | lt.alert.category_t.status_notification
        )
    except Exception:
        pass

    load_dht_state_only(ses, args.dht_state)
    ses.apply_settings(seed_cfg)

    resume_dir = args.resume_dir
    if resume_dir:
        os.makedirs(resume_dir, exist_ok=True)

    cmd_queue = queue.Queue()
    stdin_eof = threading.Event()

    def stdin_reader():
        try:
            while True:
                line = sys.stdin.readline()
                if not line:
                    break
                s = line.strip()
                if not s:
                    continue
                try:
                    obj = json.loads(s)
                    if isinstance(obj, dict):
                        cmd_queue.put(obj)
                except Exception:
                    pass
        except Exception:
            pass
        finally:
            stdin_eof.set()

    reader_thread = threading.Thread(target=stdin_reader, daemon=True)
    reader_thread.start()

    tracked = {} # info_hash_lower -> dict

    def write_resume_atomic(ih, params_obj):
        if not resume_dir or not ih:
            return
        try:
            buf = lt.write_resume_data_buf(params_obj)
            res_path = os.path.join(resume_dir, f"{ih}.resume")
            tmp_path = f"{res_path}.{os.getpid()}.tmp"
            with open(tmp_path, "wb") as rf:
                rf.write(buf)
            os.replace(tmp_path, res_path)
        except Exception:
            pass

    def drain_alerts(wait_hashes=None, wait_timeout=0.0):
        deadline = time.time() + wait_timeout
        pending = set(wait_hashes) if wait_hashes else set()
        while True:
            try:
                alerts = ses.pop_alerts()
            except Exception:
                alerts = []
            for a in alerts:
                try:
                    if isinstance(a, lt.save_resume_data_alert):
                        ih = _handle_info_hash_hex(a.handle)
                        if not ih:
                            for k, rec in tracked.items():
                                if rec.get("handle") == a.handle:
                                    ih = k
                                    break
                        if ih:
                            write_resume_atomic(ih, a.params)
                            pending.discard(ih)
                    elif hasattr(lt, "save_resume_data_failed_alert") and isinstance(a, lt.save_resume_data_failed_alert):
                        ih = _handle_info_hash_hex(a.handle)
                        pending.discard(ih)
                except Exception:
                    pass
            if not pending or time.time() >= deadline:
                break
            time.sleep(0.05)

    def remove_entry(ih, delete_resume=False):
        rec = tracked.pop(ih, None)
        if rec is None:
            return
        h = rec.get("handle")
        if h is not None:
            try:
                if h.is_valid():
                    ses.remove_torrent(h)
            except Exception:
                pass
        if delete_resume and resume_dir:
            res_path = os.path.join(resume_dir, f"{ih}.resume")
            try:
                if os.path.exists(res_path):
                    os.remove(res_path)
            except Exception:
                pass

    def add_entry(cmd):
        ih = str(cmd.get("hash") or "").strip().lower()
        torrent_path = str(cmd.get("torrent_path") or "").strip()
        magnet = str(cmd.get("magnet") or "").strip()
        save_dir = str(cmd.get("save_dir") or "").strip()
        skip_set = set(int(x) for x in (cmd.get("skip_indices") or []))
        paused = bool(cmd.get("paused", False))

        if ih and ih in tracked:
            remove_entry(ih, delete_resume=False)

        if not save_dir or not os.path.isdir(save_dir):
            if ih:
                tracked[ih] = {
                    "handle": None,
                    "paused": paused,
                    "skip_set": skip_set,
                    "file_prio_applied": True,
                    "torrent_path": torrent_path,
                    "torrent_saved": bool(torrent_path and os.path.isfile(torrent_path)),
                    "last_resume_at": time.time(),
                    "missing_dir": True,
                    "error": "missing_dir",
                }
            return

        ti = None
        if torrent_path and os.path.isfile(torrent_path):
            try:
                ti = lt.torrent_info(torrent_path)
            except Exception:
                ti = None

        atp = None
        if resume_dir and ih:
            res_path = os.path.join(resume_dir, f"{ih}.resume")
            if os.path.isfile(res_path):
                try:
                    with open(res_path, "rb") as rf:
                        atp = lt.read_resume_data(rf.read())
                except Exception:
                    atp = None

        if atp is None:
            if ti is not None:
                atp = lt.add_torrent_params()
                atp.ti = ti
            elif magnet:
                atp = lt.parse_magnet_uri(magnet)
            else:
                if ih:
                    tracked[ih] = {
                        "handle": None,
                        "paused": paused,
                        "skip_set": skip_set,
                        "file_prio_applied": True,
                        "torrent_path": torrent_path,
                        "torrent_saved": False,
                        "last_resume_at": time.time(),
                        "missing_dir": False,
                        "error": "missing_torrent_source",
                    }
                return
        else:
            if ti is not None:
                atp.ti = ti
            elif getattr(atp, "ti", None) is None and magnet:
                try:
                    mag_atp = lt.parse_magnet_uri(magnet)
                    atp.info_hashes = mag_atp.info_hashes
                    if getattr(mag_atp, "trackers", None):
                        atp.trackers = mag_atp.trackers
                except Exception:
                    pass

        eff_ti = ti or getattr(atp, "ti", None)
        eff_save_dir = _resolve_seed_save_dir(eff_ti, save_dir, skip_set)
        atp.save_path = eff_save_dir

        existing_tr = eff_ti.trackers() if eff_ti is not None else getattr(atp, "trackers", None)
        clean_trackers = merge_seed_trackers(existing_tr)
        try:
            atp.trackers = clean_trackers
        except Exception:
            pass

        if eff_ti is not None:
            try:
                num_f = eff_ti.num_files()
                atp.file_priorities = [0 if idx in skip_set else 4 for idx in range(num_f)]
            except Exception:
                pass

        atp.flags |= lt.torrent_flags.upload_mode
        if paused:
            atp.flags |= lt.torrent_flags.paused
            atp.flags &= ~lt.torrent_flags.auto_managed
        else:
            atp.flags &= ~lt.torrent_flags.paused
            atp.flags |= lt.torrent_flags.auto_managed

        handle = ses.add_torrent(atp)
        apply_clean_trackers(handle, clean_trackers)
        resolved_ih = ih or _handle_info_hash_hex(handle)

        if paused:
            try:
                handle.unset_flags(lt.torrent_flags.auto_managed)
                handle.pause()
            except Exception:
                pass
        else:
            try:
                handle.set_upload_mode(True)
                handle.set_flags(lt.torrent_flags.auto_managed)
                handle.resume()
                handle.force_reannounce()
                handle.force_dht_announce()
            except Exception:
                pass

        tracked[resolved_ih] = {
            "handle": handle,
            "paused": paused,
            "skip_set": skip_set,
            "file_prio_applied": eff_ti is not None,
            "torrent_path": torrent_path,
            "torrent_saved": bool(torrent_path and os.path.isfile(torrent_path) and os.path.getsize(torrent_path) > 64),
            "last_resume_at": time.time(),
            "missing_dir": False,
            "error": "",
        }

    def emit_snapshot():
        torrents_map = {}
        now_t = time.time()
        for ih, rec in list(tracked.items()):
            h = rec.get("handle")
            paused = bool(rec.get("paused", False))
            if rec.get("missing_dir"):
                torrents_map[ih] = {
                    "state": "missing_files",
                    "up_bps": 0,
                    "session_up": 0,
                    "leechs": 0,
                    "seeds": 0,
                    "swarm": 0,
                    "done": 0,
                    "wanted": 0,
                    "error": "",
                }
                continue
            if h is None or not h.is_valid():
                torrents_map[ih] = {
                    "state": "error",
                    "up_bps": 0,
                    "session_up": 0,
                    "leechs": 0,
                    "seeds": 0,
                    "swarm": 0,
                    "done": 0,
                    "wanted": 0,
                    "error": rec.get("error") or "invalid_handle",
                }
                continue

            st = h.status()
            has_meta = bool(getattr(st, "has_metadata", False))
            if not paused:
                if not st.upload_mode:
                    try:
                        h.set_upload_mode(True)
                    except Exception:
                        pass
                if has_meta and not rec.get("file_prio_applied"):
                    rec["file_prio_applied"] = True
                    try:
                        ti = st.torrent_file if hasattr(st, "torrent_file") and st.torrent_file is not None else h.torrent_file()
                        if ti is not None:
                            skip_set = rec.get("skip_set") or set()
                            if skip_set:
                                for idx in range(ti.num_files()):
                                    h.file_priority(idx, 0 if idx in skip_set else 4)
                            if not rec.get("torrent_saved") and rec.get("torrent_path"):
                                rec["torrent_saved"] = save_torrent_info_file(ti, rec["torrent_path"])
                    except Exception:
                        pass

            err_code = 0
            err_msg = ""
            try:
                if st.errc:
                    err_code = int(st.errc.value())
                    if err_code != 0:
                        err_msg = str(st.errc.message() or st.error or "")
            except Exception:
                pass
            if err_code == 0 and getattr(st, "error", ""):
                err_msg = str(st.error)

            is_checking = st.state in (
                lt.torrent_status.checking_files,
                lt.torrent_status.checking_resume_data,
            )
            if paused:
                state_str = "paused"
            elif err_code != 0 or err_msg:
                state_str = "error"
            elif is_checking:
                state_str = "checking"
            elif not has_meta:
                state_str = "metadata"
            else:
                state_str = "seeding"

            num_seeds = int(st.num_seeds)
            num_peers = int(st.num_peers)
            leechs = max(0, num_peers - num_seeds)
            swarm = max(num_seeds + leechs, int(st.num_complete) + int(st.num_incomplete))
            up_bps = int(getattr(st, "upload_payload_rate", 0) or st.upload_rate)
            session_up = int(getattr(st, "total_payload_upload", 0) or st.total_upload)

            torrents_map[ih] = {
                "state": state_str,
                "up_bps": max(0, up_bps) if not paused else 0,
                "session_up": max(0, session_up),
                "leechs": leechs if not paused else 0,
                "seeds": num_seeds if not paused else 0,
                "swarm": swarm,
                "done": int(st.total_wanted_done),
                "wanted": int(st.total_wanted),
                "error": err_msg,
            }

            if resume_dir and not paused and state_str == "seeding" and (now_t - rec.get("last_resume_at", 0.0)) >= 60.0:
                rec["last_resume_at"] = now_t
                try:
                    h.save_resume_data(lt.save_resume_flags_t.save_info_dict)
                except Exception:
                    pass

        sys.stdout.write(json.dumps({"event": "stats", "torrents": torrents_map}, ensure_ascii=False) + "\n")
        sys.stdout.flush()

    def shutdown_all():
        pending_hashes = []
        for ih, rec in tracked.items():
            h = rec.get("handle")
            if h is not None and h.is_valid():
                try:
                    h.pause()
                    if resume_dir:
                        h.save_resume_data(lt.save_resume_flags_t.save_info_dict)
                        pending_hashes.append(ih)
                except Exception:
                    pass
        if pending_hashes:
            drain_alerts(wait_hashes=pending_hashes, wait_timeout=1.0)
        if args.dht_state:
            try:
                dht_dir = os.path.dirname(args.dht_state)
                if dht_dir:
                    os.makedirs(dht_dir, exist_ok=True)
                raw_state = lt.bencode(ses.save_state())
                tmp_dht = f"{args.dht_state}.{os.getpid()}.tmp"
                with open(tmp_dht, "wb") as df:
                    df.write(raw_state)
                os.replace(tmp_dht, args.dht_state)
            except Exception:
                pass
        sys.stdout.write(json.dumps({"event": "shutdown_ok"}) + "\n")
        sys.stdout.flush()

    last_emit = 0.0
    last_parent_check = 0.0
    while True:
        now_t = time.time()
        if stdin_eof.is_set() or (args.parent_pid and (now_t - last_parent_check) >= 1.0):
            last_parent_check = now_t
            if stdin_eof.is_set() or not is_parent_alive(args.parent_pid):
                shutdown_all()
                return 0

        did_cmd = False
        shutdown_requested = False
        while True:
            try:
                cmd = cmd_queue.get_nowait()
            except queue.Empty:
                break
            did_cmd = True
            act = str(cmd.get("cmd") or "").strip().lower()
            ih = str(cmd.get("hash") or "").strip().lower()
            if act == "add":
                add_entry(cmd)
            elif act == "pause" and ih in tracked:
                rec = tracked[ih]
                rec["paused"] = True
                h = rec.get("handle")
                if h is not None and h.is_valid():
                    try:
                        h.unset_flags(lt.torrent_flags.auto_managed)
                        h.pause()
                        if resume_dir:
                            h.save_resume_data(lt.save_resume_flags_t.save_info_dict)
                    except Exception:
                        pass
            elif act == "resume" and ih in tracked:
                rec = tracked[ih]
                rec["paused"] = False
                h = rec.get("handle")
                if h is not None and h.is_valid():
                    try:
                        h.set_upload_mode(True)
                        h.set_flags(lt.torrent_flags.auto_managed)
                        h.resume()
                        h.force_reannounce()
                        h.force_dht_announce()
                    except Exception:
                        pass
            elif act == "reannounce" and ih in tracked:
                h = tracked[ih].get("handle")
                if h is not None and h.is_valid():
                    try:
                        h.force_reannounce()
                        h.force_dht_announce()
                    except Exception:
                        pass
            elif act == "remove" and ih in tracked:
                remove_entry(ih, delete_resume=True)
            elif act == "shutdown":
                shutdown_requested = True
                break

        if shutdown_requested:
            shutdown_all()
            return 0

        drain_alerts()
        if did_cmd or (now_t - last_emit) >= 1.0:
            last_emit = now_t
            emit_snapshot()

        time.sleep(0.2)


def run_metadata_only(ses, args):
    tmp_meta_dir = None if args.save_dir else tempfile.mkdtemp(prefix="storm_lt_meta_")
    save_dir = args.save_dir or tmp_meta_dir
    os.makedirs(save_dir, exist_ok=True)
    handle = None

    try:
        atp = lt.parse_magnet_uri(args.magnet)
        atp.save_path = save_dir
        atp.flags |= lt.torrent_flags.upload_mode
        clean_trackers = merge_trackers(getattr(atp, "trackers", None))
        atp.trackers = clean_trackers
        handle = ses.add_torrent(atp)
        apply_clean_trackers(handle, clean_trackers)

        try:
            handle.force_reannounce()
            handle.force_dht_announce()
        except Exception:
            pass

        connected_set = set()
        ordered_peers = []
        last_peers_read = 0.0
        deadline = time.time() + max(0.5, float(args.timeout or 4.5))

        def pump_peers(force=False):
            nonlocal last_peers_read
            if not args.peers_file or not os.path.exists(args.peers_file):
                return
            now_t = time.time()
            if not force and (now_t - last_peers_read) < 0.1:
                return
            last_peers_read = now_t
            try:
                with open(args.peers_file, "r", encoding="utf-8", errors="ignore") as pf:
                    lines = pf.readlines()
                for raw in lines:
                    entry = raw.lstrip("\ufeff").strip()
                    if not entry or entry in connected_set:
                        continue
                    connected_set.add(entry)
                    parsed = parse_peer_endpoint(entry)
                    if parsed is not None:
                        ordered_peers.append(parsed)
                        try:
                            handle.connect_peer(parsed)
                        except Exception:
                            pass
            except Exception:
                pass

        pump_peers(force=True)
        last_parent_check = 0.0
        started_meta_at = time.time()
        did_retry_meta = False
        got_meta = False

        while time.time() < deadline and handle.is_valid():
            now_t = time.time()
            if args.parent_pid and (now_t - last_parent_check) >= 0.8:
                last_parent_check = now_t
                if not is_parent_alive(args.parent_pid):
                    break

            pump_peers(force=False)
            if not did_retry_meta and (now_t - started_meta_at) >= 2.2 and ordered_peers:
                did_retry_meta = True
                for parsed in ordered_peers:
                    try:
                        handle.connect_peer(parsed)
                    except Exception:
                        pass
            st = handle.status()
            if bool(getattr(st, "has_metadata", False)):
                got_meta = True
                break
            time.sleep(0.1)

        if got_meta:
            try:
                ti = st.torrent_file if hasattr(st, "torrent_file") and st.torrent_file is not None else handle.torrent_file()
            except Exception:
                ti = handle.torrent_file()

            if ti is not None:
                if args.save_torrent:
                    save_torrent_info_file(ti, args.save_torrent)

                fs = ti.files()
                files_list = [
                    {"path": fs.file_path(i), "size": int(fs.file_size(i))}
                    for i in range(fs.num_files())
                ]
                sys.stdout.write(json.dumps({"status": "ok", "files": files_list}, ensure_ascii=False) + "\n")
                sys.stdout.flush()
                save_dht_state_only(ses, args.dht_state)
                if tmp_meta_dir:
                    shutil.rmtree(tmp_meta_dir, ignore_errors=True)
                os._exit(0)

        sys.stdout.write(json.dumps({"status": "timeout"}) + "\n")
        sys.stdout.flush()
        save_dht_state_only(ses, args.dht_state)
        if tmp_meta_dir:
            shutil.rmtree(tmp_meta_dir, ignore_errors=True)
        os._exit(0)
    finally:
        try:
            if handle is not None and handle.is_valid():
                ses.remove_torrent(handle)
        except Exception:
            pass
        if tmp_meta_dir:
            shutil.rmtree(tmp_meta_dir, ignore_errors=True)


def main():
    parser = argparse.ArgumentParser(description="STORM SWITCH BOX Embedded Libtorrent Turbo Engine")
    parser.add_argument("--torrent", type=str, default=None, help="Path to .torrent file")
    parser.add_argument("--magnet", type=str, default=None, help="Magnet URI")
    parser.add_argument("--save-dir", type=str, required=False, default=None, help="Directory to save downloaded files")
    parser.add_argument("--skip-indices", type=str, default=None, help="Comma-separated 0-based file indices to ignore")
    parser.add_argument("--peers-file", type=str, default=None, help="Path to text file with harvested ip:port peers")
    parser.add_argument("--control-file", type=str, default=None, help="Path to command control file (PAUSE/RESUME/STOP)")
    parser.add_argument("--parent-pid", type=int, default=None, help="Parent process PID to auto-exit if parent dies")
    parser.add_argument("--listen-port", type=int, default=44859, help="TCP/UDP listen port matching tracker announce")
    parser.add_argument("--dht-state", type=str, default=None, help="Path to cached DHT routing table state")
    parser.add_argument("--metadata-only", action="store_true", help="Resolve BEP-9 metadata only without downloading")
    parser.add_argument("--seed-daemon", action="store_true", help="Run persistent multi-torrent seeding daemon controlled via JSON stdin")
    parser.add_argument("--resume-dir", type=str, default=None, help="Directory for FastResume files in seed-daemon mode")
    parser.add_argument("--save-torrent", type=str, default=None, help="Save resolved .torrent file path")
    parser.add_argument("--timeout", type=float, default=4.5, help="Metadata-only timeout in seconds")
    args = parser.parse_args()

    if not args.seed_daemon:
        if not args.torrent and not args.magnet:
            sys.stderr.write("Error: Either --torrent or --magnet must be specified.\n")
            sys.exit(1)

        if not args.metadata_only and not args.save_dir:
            sys.stderr.write("Error: --save-dir is required unless --metadata-only is set.\n")
            sys.exit(1)

    ses = lt.session()
    if args.dht_state and not args.seed_daemon:
        load_dht_state_only(ses, args.dht_state)
    ses.apply_settings(build_turbo_settings(args.listen_port, metadata_only=args.metadata_only))

    if args.seed_daemon:
        rc = run_seed_daemon(ses, args)
        save_dht_state_only(ses, args.dht_state)
        os._exit(rc)

    if args.metadata_only:
        if not args.magnet:
            sys.stderr.write("Error: --magnet is required with --metadata-only.\n")
            sys.exit(1)
        rc = run_metadata_only(ses, args)
        save_dht_state_only(ses, args.dht_state)
        sys.exit(rc)

    os.makedirs(args.save_dir, exist_ok=True)
    try:
        ses.set_alert_mask(lt.alert.category_t.storage_notification)
    except Exception:
        pass

    skip_set = set()
    if args.skip_indices:
        for part in args.skip_indices.split(","):
            part = part.strip()
            if part.isdigit():
                skip_set.add(int(part))

    if args.torrent and os.path.exists(args.torrent):
        info = lt.torrent_info(args.torrent)
        atp = lt.add_torrent_params()
        atp.ti = info
        atp.save_path = args.save_dir
        try:
            clean_trackers = merge_trackers(info.trackers())
        except Exception:
            clean_trackers = merge_trackers(None)
        try:
            atp.trackers = clean_trackers
        except Exception:
            pass
        if skip_set:
            try:
                num_f = info.num_files()
                atp.file_priorities = [0 if idx in skip_set else 4 for idx in range(num_f)]
            except Exception:
                pass
        handle = ses.add_torrent(atp)
        apply_clean_trackers(handle, clean_trackers)
    elif args.magnet:
        atp = lt.parse_magnet_uri(args.magnet)
        atp.save_path = args.save_dir
        try:
            clean_trackers = merge_trackers(getattr(atp, "trackers", None))
        except Exception:
            clean_trackers = merge_trackers(None)
        try:
            atp.trackers = clean_trackers
        except Exception:
            pass
        handle = ses.add_torrent(atp)
        apply_clean_trackers(handle, clean_trackers)
    else:
        sys.stderr.write("Error: Torrent file does not exist and no valid --magnet was provided.\n")
        sys.exit(1)

    for _fn, _val in (("set_max_connections", -1), ("set_max_uploads", -1), ("set_download_limit", -1), ("set_upload_limit", -1)):
        try:
            getattr(handle, _fn)(_val)
        except Exception:
            pass

    try:
        handle.force_reannounce()
        handle.force_dht_announce()
    except Exception:
        pass

    file_prio_applied = False
    connected_peers = set()
    ordered_peers = []
    reinject_cursor = 0
    last_reinject_at = time.time()
    last_peers_read = 0.0
    started_at = time.time()
    was_paused = False
    last_pct = 0.0
    last_swarm = 0
    last_done = 0
    last_total = 0
    last_is_meta = False

    def emit_status(pct, speed_bps, seeds, peers, swarm, done_bytes, total_bytes, state, is_meta=False):
        payload = {
            "pct": round(float(pct), 2),
            "speed_bps": int(speed_bps),
            "seeds": int(seeds),
            "peers": int(peers),
            "swarm": int(swarm),
            "done_bytes": int(done_bytes),
            "total_bytes": int(total_bytes),
            "state": state,
            "is_meta": bool(is_meta),
        }
        sys.stdout.write(json.dumps(payload) + "\n")
        sys.stdout.flush()

    def inject_new_peers(force_now=False, current_rate=0, current_seeds=0):
        nonlocal last_peers_read, last_reinject_at, reinject_cursor
        now_t = time.time()
        if args.peers_file and os.path.exists(args.peers_file):
            poll_interval = 0.25 if (now_t - started_at) < 20.0 else 1.0
            if force_now or (now_t - last_peers_read) >= poll_interval:
                last_peers_read = now_t
                try:
                    with open(args.peers_file, "r", encoding="utf-8", errors="ignore") as pf:
                        lines = pf.readlines()
                    for raw in lines:
                        entry = raw.lstrip("\ufeff").strip()
                        if not entry or entry in connected_peers:
                            continue
                        connected_peers.add(entry)
                        parsed = parse_peer_endpoint(entry)
                        if parsed is not None:
                            ordered_peers.append(parsed)
                            try:
                                handle.connect_peer(parsed)
                            except Exception:
                                pass
                except Exception:
                    pass

        # Periodically retry existing peers aggressively when seeds are low or missing
        reinject_cooldown = 3.0 if current_seeds == 0 else 8.0
        if (now_t - last_reinject_at) >= reinject_cooldown and ordered_peers and (current_seeds == 0 or (current_rate < (50 * 1024 * 1024) and current_seeds < 50)):
            last_reinject_at = now_t
            batch_size = min(250 if current_seeds == 0 else 200, len(ordered_peers))
            total_len = len(ordered_peers)
            for _ in range(batch_size):
                parsed = ordered_peers[reinject_cursor % total_len]
                reinject_cursor += 1
                try:
                    handle.connect_peer(parsed)
                except Exception:
                    pass

    last_parent_check = 0.0
    last_tracker_reannounce = time.time()
    try:
        inject_new_peers(force_now=True)
        while handle.is_valid():
            now_check = time.time()
            if args.parent_pid and (now_check - last_parent_check) >= 1.0:
                last_parent_check = now_check
                if not is_parent_alive(args.parent_pid):
                    try:
                        handle.pause()
                    except Exception:
                        pass
                    flush_torrent_cache(ses, handle)
                    break

            if args.control_file and os.path.exists(args.control_file):
                try:
                    with open(args.control_file, "r", encoding="utf-8", errors="ignore") as cf:
                        cmd = cf.read().strip().upper()
                    if cmd == "STOP":
                        try:
                            handle.pause()
                        except Exception:
                            pass
                        flush_torrent_cache(ses, handle)
                        emit_status(last_pct, 0, 0, 0, last_swarm, last_done, last_total, "stopped", is_meta=last_is_meta)
                        break
                    elif cmd == "PAUSE" and not was_paused:
                        was_paused = True
                        try:
                            handle.unset_flags(lt.torrent_flags.auto_managed)
                        except Exception:
                            pass
                        handle.pause()
                    elif cmd == "RESUME" and was_paused:
                        was_paused = False
                        handle.resume()
                        try:
                            handle.force_reannounce()
                        except Exception:
                            pass
                except Exception:
                    pass

            if was_paused:
                emit_status(last_pct, 0, 0, 0, last_swarm, last_done, last_total, "paused", is_meta=last_is_meta)
                time.sleep(0.35)
                continue

            st = handle.status()
            reannounce_interval = 12.0 if st.num_seeds == 0 else 35.0
            if (now_check - last_tracker_reannounce) >= reannounce_interval and (st.num_seeds == 0 or (st.download_rate < (30 * 1024 * 1024) and st.num_seeds < 30)):
                last_tracker_reannounce = now_check
                try:
                    handle.force_reannounce()
                    handle.force_dht_announce()
                except Exception:
                    pass
            inject_new_peers(force_now=False, current_rate=st.download_rate, current_seeds=st.num_seeds)

            has_meta = bool(getattr(st, "has_metadata", False))
            non_seed_peers = max(0, int(st.num_peers) - int(st.num_seeds))

            if not has_meta:
                last_is_meta = True
                last_swarm = max(st.num_seeds + non_seed_peers, st.num_complete + st.num_incomplete)
                emit_status(
                    0.0,
                    st.download_rate,
                    st.num_seeds,
                    non_seed_peers,
                    last_swarm,
                    0,
                    0,
                    "downloading",
                    is_meta=True,
                )
                time.sleep(0.25)
                continue

            last_is_meta = False
            if not file_prio_applied:
                file_prio_applied = True
                try:
                    ti = st.torrent_file if hasattr(st, "torrent_file") and st.torrent_file is not None else handle.torrent_file()
                    if ti is not None:
                        num_files = ti.num_files()
                        for i in range(num_files):
                            handle.file_priority(i, 0 if i in skip_set else 4)
                        if args.save_torrent and (not os.path.exists(args.save_torrent) or os.path.getsize(args.save_torrent) <= 64):
                            save_torrent_info_file(ti, args.save_torrent)
                except Exception:
                    pass

            total_wanted = st.total_wanted
            total_wanted_done = st.total_wanted_done

            if total_wanted > 0:
                pct = min(100.0, (total_wanted_done / float(total_wanted)) * 100.0)
            else:
                pct = min(100.0, st.progress * 100.0)

            swarm = max(st.num_seeds + non_seed_peers, st.num_complete + st.num_incomplete)
            last_pct = pct
            last_swarm = swarm
            last_done = total_wanted_done
            last_total = total_wanted

            is_finished = st.is_finished or (total_wanted > 0 and total_wanted_done >= total_wanted)
            if is_finished:
                flush_torrent_cache(ses, handle)
                emit_status(100.0, 0, st.num_seeds, non_seed_peers, swarm, total_wanted, total_wanted, "finished")
                break

            emit_status(pct, st.download_rate, st.num_seeds, non_seed_peers, swarm, total_wanted_done, total_wanted, "downloading")
            time.sleep(0.3)

    except KeyboardInterrupt:
        flush_torrent_cache(ses, handle)
    except Exception as ex:
        sys.stderr.write(f"Libtorrent error: {ex}\n")
        save_dht_state_only(ses, args.dht_state)
        sys.exit(2)

    save_dht_state_only(ses, args.dht_state)
    sys.stdout.flush()
    os._exit(0)


if __name__ == "__main__":
    main()
