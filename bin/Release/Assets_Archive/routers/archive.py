from __future__ import annotations

from fastapi import APIRouter, HTTPException, Request

try:
    from models.schemas import ArchiveMatchRequest, ArchiveRunRequest
except ImportError:
    from ..models.schemas import ArchiveMatchRequest, ArchiveRunRequest

router = APIRouter(prefix="/api/v1/archive", tags=["Archive"])


def _resolve_request_meta(request: Request, runtime) -> tuple[str, str]:
    trace_id = (request.headers.get("X-Archive-Trace-Id") or "").strip() or runtime.new_trace_id()
    request_source = (request.headers.get("X-Archive-Request-Source") or "").strip() or "api"
    return trace_id, request_source


@router.post("/match")
async def match_file(match_request: ArchiveMatchRequest, request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        raise HTTPException(status_code=503, detail="Runtime is not initialized.")
    trace_id, request_source = _resolve_request_meta(request, runtime)
    return runtime.command_service.match_file(
        match_request.file_path,
        trace_id=trace_id,
        request_source=request_source,
    )


@router.post("/run")
async def run_archive(run_request: ArchiveRunRequest, request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        raise HTTPException(status_code=503, detail="Runtime is not initialized.")
    trace_id, request_source = _resolve_request_meta(request, runtime)
    return runtime.command_service.run_archive(
        run_request.model_dump(exclude_none=True, exclude_unset=True),
        trace_id=trace_id,
        request_source=request_source,
    )
