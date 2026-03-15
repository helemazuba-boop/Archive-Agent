from __future__ import annotations

from state_ops import Context, build_match_result, execute_archive_run, patch_config


class CommandService:
    def __init__(self, runtime):
        self._runtime = runtime

    def update_config(self, patch_payload, trace_id: str | None = None, request_source: str = "api"):
        context = Context(
            self._runtime.data_dir,
            logger=self._runtime.logger,
            trace_id=trace_id or self._runtime.new_trace_id(),
            request_source=request_source,
        )
        self._runtime.logger.info("update_config trace=%s source=%s", context.trace_id, request_source)
        return patch_config(context, patch_payload)

    def match_file(self, file_path: str, trace_id: str | None = None, request_source: str = "api"):
        context = Context(
            self._runtime.data_dir,
            logger=self._runtime.logger,
            trace_id=trace_id or self._runtime.new_trace_id(),
            request_source=request_source,
        )
        self._runtime.logger.info("match_file trace=%s source=%s file=%s", context.trace_id, request_source, file_path)
        return build_match_result(context, file_path)

    def run_archive(self, payload, trace_id: str | None = None, request_source: str = "api"):
        context = Context(
            self._runtime.data_dir,
            logger=self._runtime.logger,
            trace_id=trace_id or self._runtime.new_trace_id(),
            request_source=request_source,
        )
        self._runtime.logger.info("run_archive trace=%s source=%s", context.trace_id, request_source)
        return execute_archive_run(context, payload)
