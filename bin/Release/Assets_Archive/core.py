#!/usr/bin/env python3

from __future__ import annotations

import argparse
import os
import signal
import socket
import sys
import threading
import time
import traceback
from contextlib import asynccontextmanager
from pathlib import Path

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from fastapi import BackgroundTasks, FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware

from routers import archive, config
from runtime import create_runtime

import uvicorn


@asynccontextmanager
async def lifespan(app: FastAPI):
    print(f"[Lifespan] Archive backend starting in {os.getcwd()}", flush=True)
    yield
    print("[Lifespan] Archive backend shutting down", flush=True)


app = FastAPI(title="Archive-Agent IPC Engine", version="1.0.0", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(config.router)
app.include_router(archive.router)


@app.get("/")
async def root(request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        return {"status": "running", "engine": "Archive-Agent FastAPI", "version": "1.0.0"}
    payload = runtime.query_service.health()
    payload["engine"] = "Archive-Agent FastAPI"
    return payload


@app.get("/health")
async def health(request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        return {"status": "ok", "version": "1.0.0"}
    return runtime.query_service.health()


@app.get("/engine/info")
async def engine_info(request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        return {"engine": "Archive-Agent Backend", "version": "1.0.0"}
    return runtime.query_service.engine_info()


@app.post("/shutdown")
async def shutdown(background_tasks: BackgroundTasks):
    def exit_process():
        time.sleep(0.5)
        print("[Server] Manual shutdown triggered. Exiting...", flush=True)
        os.kill(os.getpid(), signal.SIGTERM)

    background_tasks.add_task(exit_process)
    return {"status": "shutting down"}


def monitor_parent_process():
    parent_pid = os.getppid()
    if parent_pid <= 1:
        return

    if os.name == "nt":
        import ctypes
        import ctypes.wintypes

        synchronize = 0x00100000
        wait_object_0 = 0x00000000
        infinite = 0xFFFFFFFF

        kernel32 = ctypes.windll.kernel32
        handle = kernel32.OpenProcess(synchronize, False, parent_pid)
        if not handle:
            print(f"[Lifecycle] Cannot open parent PID {parent_pid}, exiting.", flush=True)
            os._exit(1)

        print(f"[Lifecycle] Windows SuicideWatch active for parent PID: {parent_pid}", flush=True)
        try:
            result = kernel32.WaitForSingleObject(ctypes.wintypes.HANDLE(handle), infinite)
            if result == wait_object_0:
                print(f"[Lifecycle] Parent {parent_pid} exited. Shutting down self...", flush=True)
                os._exit(0)
        finally:
            kernel32.CloseHandle(handle)
    else:
        print(f"[Lifecycle] Posix SuicideWatch active for parent PID: {parent_pid}", flush=True)
        while True:
            try:
                os.kill(parent_pid, 0)
            except OSError:
                print(f"[Lifecycle] Parent {parent_pid} lost. Shutting down self...", flush=True)
                os._exit(0)
            time.sleep(2)


def main():
    parser = argparse.ArgumentParser(description="Archive-Agent backend")
    parser.add_argument("--data-dir", type=str, default="data")
    parser.add_argument("--server", action="store_true", help="Run in HTTP server mode")
    parser.add_argument("--port", type=int, default=0, help="Port to listen on (0 for random)")
    args = parser.parse_args()

    data_dir = Path(args.data_dir).resolve()
    data_dir.mkdir(parents=True, exist_ok=True)

    if not args.server:
        raise RuntimeError("Archive-Agent backend only supports --server mode.")

    app.state.runtime = create_runtime(data_dir)

    actual_port = args.port
    if actual_port == 0:
        temp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        temp_sock.bind(("127.0.0.1", 0))
        actual_port = temp_sock.getsockname()[1]
        temp_sock.close()

    print(f"__ARCHIVE_SERVER_PORT__:{actual_port}", flush=True)

    watch_thread = threading.Thread(target=monitor_parent_process, daemon=True, name="SuicideWatch")
    watch_thread.start()

    uvicorn.run(app, host="127.0.0.1", port=actual_port, log_level="warning")


def audit_environment():
    print("--- Start-up Audit ---", flush=True)
    print(f"Exec: {sys.executable}", flush=True)
    print(f"CWD: {os.getcwd()}", flush=True)
    print(f"Python: {sys.version.split()[0]}", flush=True)
    try:
        import fastapi  # noqa: F401
        import uvicorn  # noqa: F401

        print("FastAPI and Uvicorn available", flush=True)
    except ImportError as exc:
        print(f"CRITICAL: Missing dependency: {exc}", file=sys.stderr, flush=True)
        sys.exit(1)
    print("---------------------", flush=True)


if __name__ == "__main__":
    try:
        audit_environment()
        main()
    except Exception:
        print("\n!!! CRITICAL STARTUP ERROR !!!", file=sys.stderr, flush=True)
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)
