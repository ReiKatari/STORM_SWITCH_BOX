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

    # Initialize libtorrent session with high-throughput settings
    ses = lt.session()
    settings = {
        'enable_outgoing_utp': True,
        'enable_incoming_utp': True,
        'enable_dht': True,
        'enable_lsd': True,
        'enable_upnp': True,
        'enable_natpmp': True,
        'announce_to_all_trackers': True,
        'announce_to_all_tiers': True,
        'mixed_mode_algorithm': 0, # prefer_tcp
        'connection_speed': 100,
        'connections_limit': 800,
        'peer_fingerprint': '-qB5000-',
        'user_agent': 'qBittorrent/5.0.0',
        'aio_threads': 8,
        'dht_bootstrap_nodes': 'router.bittorrent.com:6881,dht.transmissionbt.com:6881,router.utorrent.com:6881,dht.aelitis.com:6881,dht.libtorrent.org:25401',
    }
    ses.apply_settings(settings)

    # Add bootstrap DHT routers
    try:
        ses.add_dht_router('router.bittorrent.com', 6881)
        ses.add_dht_router('dht.transmissionbt.com', 6881)
        ses.add_dht_router('router.utorrent.com', 6881)
        ses.add_dht_router('dht.aelitis.com', 6881)
    except Exception:
        pass

    skip_set = set()
    if args.skip_indices:
        for part in args.skip_indices.split(','):
            part = part.strip()
            if part.isdigit():
                skip_set.add(int(part))

    # Add torrent
    if args.torrent and os.path.exists(args.torrent):
        info = lt.torrent_info(args.torrent)
        atp = lt.add_torrent_params()
        atp.ti = info
        atp.save_path = args.save_dir
        handle = ses.add_torrent(atp)
    else:
        atp = lt.parse_magnet_uri(args.magnet)
        atp.save_path = args.save_dir
        handle = ses.add_torrent(atp)

    file_prio_applied = False
    connected_peers = set()
    last_peers_read = 0.0
    was_paused = False

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
            "is_meta": bool(is_meta)
        }
        sys.stdout.write(json.dumps(payload) + "\n")
        sys.stdout.flush()

    try:
        while handle.is_valid():
            # Check control file (PAUSE/RESUME/STOP)
            if args.control_file and os.path.exists(args.control_file):
                try:
                    with open(args.control_file, "r", encoding="utf-8") as cf:
                        cmd = cf.read().strip().upper()
                    if cmd == "STOP":
                        emit_status(0, 0, 0, 0, 0, 0, 0, "stopped")
                        break
                    elif cmd == "PAUSE" and not was_paused:
                        was_paused = True
                        handle.pause()
                    elif cmd == "RESUME" and was_paused:
                        was_paused = False
                        handle.resume()
                except Exception:
                    pass

            if was_paused:
                time.sleep(0.3)
                continue

            # Read and inject peers from peers file
            now = time.time()
            if args.peers_file and os.path.exists(args.peers_file) and (now - last_peers_read >= 1.0):
                last_peers_read = now
                try:
                    with open(args.peers_file, "r", encoding="utf-8") as pf:
                        for line in pf:
                            line = line.strip()
                            if line and ":" in line and line not in connected_peers:
                                connected_peers.add(line)
                                parts = line.split(":")
                                if len(parts) == 2 and parts[1].isdigit():
                                    ip = parts[0]
                                    port = int(parts[1])
                                    if port > 0:
                                        handle.connect_peer((ip, port))
                except Exception:
                    pass

            st = handle.status()

            # Metadata acquisition
            if not handle.has_metadata():
                emit_status(0.0, st.download_rate, st.num_seeds, st.num_peers,
                            st.num_complete + st.num_incomplete, 0, 0, "downloading", is_meta=True)
                time.sleep(0.3)
                continue

            # Apply selective file priorities once metadata is available
            if not file_prio_applied:
                file_prio_applied = True
                ti = handle.torrent_file()
                num_files = ti.num_files()
                for i in range(num_files):
                    if i in skip_set:
                        handle.file_priority(i, 0)
                    else:
                        handle.file_priority(i, 1)

            total_wanted = st.total_wanted
            total_wanted_done = st.total_wanted_done

            if total_wanted > 0:
                pct = min(100.0, (total_wanted_done / float(total_wanted)) * 100.0)
            else:
                pct = min(100.0, st.progress * 100.0)

            swarm = max(st.num_seeds + st.num_peers, st.num_complete + st.num_incomplete)
            is_finished = st.is_finished or (total_wanted > 0 and total_wanted_done >= total_wanted)

            if is_finished:
                emit_status(100.0, 0, st.num_seeds, st.num_peers, swarm, total_wanted, total_wanted, "finished")
                break

            emit_status(pct, st.download_rate, st.num_seeds, st.num_peers, swarm,
                        total_wanted_done, total_wanted, "downloading")

            time.sleep(0.3)

    except KeyboardInterrupt:
        pass
    except Exception as ex:
        sys.stderr.write(f"Libtorrent error: {ex}\n")
        sys.exit(2)

    sys.exit(0)

if __name__ == "__main__":
    main()
