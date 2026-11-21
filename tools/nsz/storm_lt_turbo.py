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
import re
import shutil
import socket
import sys
import tempfile
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
        "enable_lsd": False,
        "enable_upnp": not metadata_only,
        "enable_natpmp": not metadata_only,
        "announce_to_all_trackers": True,
        "announce_to_all_tiers": True,
        "mixed_mode_algorithm": 1,  # peer_proportional: never throttle uTP seeds
        "choking_algorithm": 0,
        "seed_choking_algorithm": 1,
        "smooth_connects": False,
        "connection_speed": 1000,
        "torrent_connect_boost": 255,
        "connections_limit": max(3000, int(base.get("connections_limit", 8000))),
        "max_peerlist_size": 6000,
        "max_out_request_queue": 4000,
        "max_allowed_in_request_queue": 4000,
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
        "max_queued_disk_bytes": 256 * 1024 * 1024,
        "send_buffer_watermark": 33554432,
        "send_buffer_low_watermark": 4194304,
        "send_buffer_watermark_factor": 150,
        "allow_multiple_connections_per_ip": True,
        "min_reconnect_time": 2,
        "max_failcount": 5,
        "peer_connect_timeout": 8,
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
    for tr in DEFAULT_PUBLIC_TRACKERS:
        if tr not in seen:
            seen.add(tr)
            merged.append(tr)
    for tr in (existing_trackers or []):
        url = str(tr.url if hasattr(tr, "url") else tr).strip()
        if not url:
            continue
        low = url.lower()
        if "t-ru.org" in low or "retracker.local" in low:
            continue
        if url not in seen:
            seen.add(url)
            merged.append(url)
            if len(merged) >= 24:
                break
    return merged[:24]


def apply_clean_trackers(handle, trackers):
    if not trackers:
        return
    try:
        entries = [lt.announce_entry(u) for u in trackers if u]
        if entries:
            handle.replace_trackers(entries)
    except Exception:
        pass


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
                    try:
                        tparent = os.path.dirname(args.save_torrent)
                        if tparent:
                            os.makedirs(tparent, exist_ok=True)
                        if hasattr(ti, "info_section"):
                            raw_tor = b"d4:info" + bytes(ti.info_section()) + b"e"
                        else:
                            ct = lt.create_torrent(ti)
                            raw_tor = lt.bencode(ct.generate())
                        tmp_tor = f"{args.save_torrent}.{os.getpid()}.tmp"
                        with open(tmp_tor, "wb") as tf:
                            tf.write(raw_tor)
                        os.replace(tmp_tor, args.save_torrent)
                    except Exception:
                        pass

                fs = ti.files()
                files_list = [
                    {"path": fs.file_path(i), "size": int(fs.file_size(i))}
                    for i in range(fs.num_files())
                ]
                sys.stdout.write(json.dumps({"status": "ok", "files": files_list}, ensure_ascii=False) + "\n")
                sys.stdout.flush()
                if tmp_meta_dir:
                    shutil.rmtree(tmp_meta_dir, ignore_errors=True)
                os._exit(0)

        sys.stdout.write(json.dumps({"status": "timeout"}) + "\n")
        sys.stdout.flush()
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
    parser.add_argument("--save-torrent", type=str, default=None, help="Save resolved .torrent file path in metadata-only mode")
    parser.add_argument("--timeout", type=float, default=4.5, help="Metadata-only timeout in seconds")
    args = parser.parse_args()

    if not args.torrent and not args.magnet:
        sys.stderr.write("Error: Either --torrent or --magnet must be specified.\n")
        sys.exit(1)

    if not args.metadata_only and not args.save_dir:
        sys.stderr.write("Error: --save-dir is required unless --metadata-only is set.\n")
        sys.exit(1)

    ses = lt.session()
    ses.apply_settings(build_turbo_settings(args.listen_port, metadata_only=args.metadata_only))

    if args.metadata_only:
        if not args.magnet:
            sys.stderr.write("Error: --magnet is required with --metadata-only.\n")
            sys.exit(1)
        rc = run_metadata_only(ses, args)
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

        # Periodically retry existing peers if speed < 20 MB/s and seeds < 25
        if (now_t - last_reinject_at) >= 10.0 and ordered_peers and current_rate < (20 * 1024 * 1024) and current_seeds < 25:
            last_reinject_at = now_t
            batch_size = min(120, len(ordered_peers))
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
            if (now_check - last_tracker_reannounce) >= 45.0 and st.download_rate < (15 * 1024 * 1024) and st.num_seeds < 15:
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
        sys.exit(2)

    sys.stdout.flush()
    os._exit(0)


if __name__ == "__main__":
    main()
