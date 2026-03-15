from __future__ import annotations

import logging
import time
import uuid
from pathlib import Path

from application.command_service import CommandService
from application.query_service import QueryService

APP_VERSION = "1.0.0"


def _configure_logger(logs_dir: Path) -> logging.Logger:
    logs_dir.mkdir(parents=True, exist_ok=True)
    logger = logging.getLogger("archive_agent_backend")
    if logger.handlers:
        return logger

    logger.setLevel(logging.INFO)
    formatter = logging.Formatter("%(asctime)s [%(levelname)s] %(message)s")

    file_handler = logging.FileHandler(logs_dir / "archive-backend.log", encoding="utf-8")
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)

    stream_handler = logging.StreamHandler()
    stream_handler.setFormatter(formatter)
    logger.addHandler(stream_handler)

    return logger


class ArchiveRuntime:
    def __init__(self, data_dir: Path):
        self.data_dir = data_dir.resolve()
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self.logs_dir = self.data_dir.parent / "logs"
        self.version = APP_VERSION
        self.started_at = time.monotonic()
        self.logger = _configure_logger(self.logs_dir)
        self.command_service = CommandService(self)
        self.query_service = QueryService(self)

    def new_trace_id(self) -> str:
        return uuid.uuid4().hex


def create_runtime(data_dir: Path) -> ArchiveRuntime:
    return ArchiveRuntime(data_dir)
