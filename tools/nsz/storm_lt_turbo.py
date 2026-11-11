#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
STORM SWITCH BOX - Embedded Native C++ libtorrent-rasterbar Turbo Engine (Tier 2)
Provides full C++ throughput (uTP BEP-29, BEP-55 holepunch, HTTP tracker peer injection)
without requiring external qBittorrent installation.
"""

import argparse
import json
import os
import sys
import time
import libtorrent as lt

DEFAULT_PUBLIC_TRACKERS = [
    "udp://tracker.opentrackr.org:1337/announce",
    "udp://open.stealth.si:80/announce",
    "udp://tracker.torrent.eu.org:451/announce",
    "udp://exodus.desync.com:6969/announce",
]


def build_turbo_settings():
    default_keys = set(lt.default_settings().keys())
    desired = {
        "enable_outgoing_utp": True,
        "enable_incoming_utp": True,
        "enable_dht": True,
        "enable_lsd": True,
        "enable_upnp": True,
        "enable_natpmp": True,
        "announce_to_all_trackers": True,
        "announce_to_all_tiers": True,
        "mixed_mode_algorithm": 0,  # prefer_tcp for full fiber/broadband throughput
        "choking_algorithm": 0,
        "seed_choking_algorithm": 1,
        "connection_speed": 250,
        "torrent_connect_boost": 255,
        "connections_limit": 1200,
        "unchoke_slots_limit": 64,
        "max_out_request_queue": 1500,
        "max_allowed_in_request_queue": 2000,
        "send_buffer_watermark": 33554432,
        "send_buffer_low_watermark": 4194304,
        "send_buffer_watermark_factor": 150,
        "active_downloads": 32,
        "active_seeds": 32,
        "active_limit": 64,
        "allow_multiple_connections_per_ip": True,
        "min_reconnect_time": 2,
        "max_failcount": 1,
        "request_timeout": 12,
        "peer_timeout": 30,
        "in_enc_policy": 1,
        "out_enc_policy": 1,
        "prefer_rc4": False,
        "peer_fingerprint": "-qB5000-",
        "user_agent": "qBittorrent/5.0.0",
        "aio_threads": 8,
        "dht_bootstrap_nodes": (
            "router.bittorrent.com:6881,"
            "dht.transmissionbt.com:6881,"
            "router.utorrent.com:6881,"
            "dht.aelitis.com:6881,"
            "dht.libtorrent.org:25401"
        ),
    }
    return {k: v for k, v in desired.items() if k in default_keys}


def main():
    parser = argparse.ArgumentParser(description="STORM SWITCH BOX Embedded Libtorrent Turbo Engine")
    parser.add_argument("--torrent", type=str, default=None, help="Path to .torrent file")
    parser.add_argument("--magnet", type=str, default=None, help="Magnet URI")
    parser.add_argument("--save-dir", type=str, required=True, help="Directory to save downloaded files")
    parser.add_argument("--skip-indices", type=str, default=None, help="Comma-separated 0-based file indices to ignore")
    parser.add_argument("--peers-file", type=str, default=None, help="Path to text file with harvested ip:port peers")
    parser.add_argument("--control-file", type=str, default=None, help="Path to command control file (PAUSE/RESUME/STOP)")
    args = parser.parse_args()

    if not args.torrent and not args.magnet:
        sys.stderr.write("Error: Either --torrent or --magnet must be specified.\n")
        sys.exit(1)

    os.makedirs(args.save_dir, exist_ok=True)

    ses = lt.session()
    ses.apply_settings(build_turbo_settings())

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
        existing_tr = set(atp.trackers) if getattr(atp, "trackers", None) else set()
        merged_tr = list(atp.trackers) if getattr(atp, "trackers", None) else []
        for tr in DEFAULT_PUBLIC_TRACKERS:
            if tr not in existing_tr:
                merged_tr.append(tr)
        try:
            atp.trackers = merged_tr
        except Exception:
            pass
        if skip_set:
            try:
                num_f = info.num_files()
                atp.file_priorities = [0 if idx in skip_set else 4 for idx in range(num_f)]
            except Exception:
                pass
        handle = ses.add_torrent(atp)
    else:
        atp = lt.parse_magnet_uri(args.magnet)
        atp.save_path = args.save_dir
        existing_tr = set(atp.trackers) if getattr(atp, "trackers", None) else set()
        merged_tr = list(atp.trackers) if getattr(atp, "trackers", None) else []
        for tr in DEFAULT_PUBLIC_TRACKERS:
            if tr not in existing_tr:
                merged_tr.append(tr)
        try:
            atp.trackers = merged_tr
        except Exception:
            pass
        handle = ses.add_torrent(atp)

    try:
        handle.force_reannounce()
        handle.force_dht_announce()
    except Exception:
        pass

    file_prio_applied = False
    connected_peers = set()
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

    def inject_new_peers(force_now=False):
        nonlocal last_peers_read
        if not args.peers_file or not os.path.exists(args.peers_file):
            return
        now_t = time.time()
        poll_interval = 0.25 if (now_t - started_at) < 20.0 else 1.0
        if not force_now and (now_t - last_peers_read) < poll_interval:
            return
        last_peers_read = now_t
        try:
            with open(args.peers_file, "r", encoding="utf-8", errors="ignore") as pf:
                lines = pf.readlines()
            for raw in lines:
                entry = raw.strip()
                if not entry or entry in connected_peers or ":" not in entry:
                    continue
                connected_peers.add(entry)
                host_part, port_part = entry.rsplit(":", 1)
                host_part = host_part.strip("[] ")
                if port_part.isdigit():
                    port_num = int(port_part)
                    if 0 < port_num <= 65535 and host_part:
                        try:
                            handle.connect_peer((host_part, port_num))
                        except Exception:
                            pass
        except Exception:
            pass

    try:
        inject_new_peers(force_now=True)
        while handle.is_valid():
            if args.control_file and os.path.exists(args.control_file):
                try:
                    with open(args.control_file, "r", encoding="utf-8", errors="ignore") as cf:
                        cmd = cf.read().strip().upper()
                    if cmd == "STOP":
                        try:
                            handle.pause()
                        except Exception:
                            pass
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

            inject_new_peers(force_now=False)

            st = handle.status()
            has_meta = bool(getattr(st, "has_metadata", False))

            if not has_meta:
                last_is_meta = True
                last_swarm = max(st.num_seeds + st.num_peers, st.num_complete + st.num_incomplete)
                emit_status(
                    0.0,
                    st.download_rate,
                    st.num_seeds,
                    st.num_peers,
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

            swarm = max(st.num_seeds + st.num_peers, st.num_complete + st.num_incomplete)
            last_pct = pct
            last_swarm = swarm
            last_done = total_wanted_done
            last_total = total_wanted

            is_finished = st.is_finished or (total_wanted > 0 and total_wanted_done >= total_wanted)
            if is_finished:
                emit_status(100.0, 0, st.num_seeds, st.num_peers, swarm, total_wanted, total_wanted, "finished")
                break

            emit_status(pct, st.download_rate, st.num_seeds, st.num_peers, swarm, total_wanted_done, total_wanted, "downloading")
            time.sleep(0.3)

    except KeyboardInterrupt:
        pass
    except Exception as ex:
        sys.stderr.write(f"Libtorrent error: {ex}\n")
        sys.exit(2)

    sys.exit(0)


if __name__ == "__main__":
    main()
