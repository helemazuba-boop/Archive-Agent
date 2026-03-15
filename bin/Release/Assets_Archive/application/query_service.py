from __future__ import annotations

import time

from state_ops import Context, load_config, load_state


class QueryService:
    def __init__(self, runtime):
        self._runtime = runtime

    def health(self) -> dict:
        return {
            "status": "ok",
            "version": self._runtime.version,
            "uptime_seconds": round(time.monotonic() - self._runtime.started_at, 3),
            "data_dir": str(self._runtime.data_dir),
        }

    def engine_info(self) -> dict:
        return {
            "engine": "Archive-Agent Backend",
            "version": self._runtime.version,
            "supported_operations": ["organize", "sort", "move", "copy", "delete"],
            "supported_embedding_providers": ["keyword", "local", "remote", "hybrid"],
            "current_runtime_mode": "local_fastapi_ipc",
        }

    def get_config(self, trace_id: str | None = None, request_source: str = "api") -> dict:
        context = Context(
            self._runtime.data_dir,
            logger=self._runtime.logger,
            trace_id=trace_id or self._runtime.new_trace_id(),
            request_source=request_source,
        )
        self._runtime.logger.info("get_config trace=%s source=%s", context.trace_id, request_source)
        return load_config(context)

    def get_snapshot(self, trace_id: str | None = None, request_source: str = "api") -> dict:
        context = Context(
            self._runtime.data_dir,
            logger=self._runtime.logger,
            trace_id=trace_id or self._runtime.new_trace_id(),
            request_source=request_source,
        )
        self._runtime.logger.info("get_snapshot trace=%s source=%s", context.trace_id, request_source)
        return {
            "config": load_config(context),
            "state": load_state(context.paths["state"]),
        }
