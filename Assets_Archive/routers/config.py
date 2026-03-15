from __future__ import annotations

from fastapi import APIRouter, HTTPException, Request

try:
    from models.schemas import ArchiveBackendConfigPatch
except ImportError:
    from ..models.schemas import ArchiveBackendConfigPatch

router = APIRouter(prefix="/api/v1", tags=["Config"])


def _resolve_request_meta(request: Request, runtime) -> tuple[str, str]:
    trace_id = (request.headers.get("X-Archive-Trace-Id") or "").strip() or runtime.new_trace_id()
    request_source = (request.headers.get("X-Archive-Request-Source") or "").strip() or "api"
    return trace_id, request_source


@router.get("/config")
async def get_config(request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        raise HTTPException(status_code=503, detail="Runtime is not initialized.")
    trace_id, request_source = _resolve_request_meta(request, runtime)
    return runtime.query_service.get_config(trace_id=trace_id, request_source=request_source)


@router.patch("/config")
async def patch_config(config_patch: ArchiveBackendConfigPatch, request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        raise HTTPException(status_code=503, detail="Runtime is not initialized.")
    trace_id, request_source = _resolve_request_meta(request, runtime)
    return runtime.command_service.update_config(
        config_patch.model_dump(exclude_none=True, exclude_unset=True),
        trace_id=trace_id,
        request_source=request_source,
    )


@router.get("/snapshot")
async def get_snapshot(request: Request):
    runtime = getattr(request.app.state, "runtime", None)
    if runtime is None:
        raise HTTPException(status_code=503, detail="Runtime is not initialized.")
    trace_id, request_source = _resolve_request_meta(request, runtime)
    return runtime.query_service.get_snapshot(trace_id=trace_id, request_source=request_source)
