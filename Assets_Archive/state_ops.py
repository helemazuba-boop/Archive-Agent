from __future__ import annotations

import json
import shutil
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

DEFAULT_BACKEND_CONFIG = {
    "default_operation": "organize",
    "overwrite_existing": False,
    "keyword_rules": [],
    "embedding_provider": "keyword",
    "embedding_model_path": None,
    "embedding_api_endpoint": None,
    "embedding_api_key": None,
    "embedding_model_name": None,
    "embedding_similarity_threshold": 0.7,
}

DEFAULT_STATE = {
    "operation_history": [],
    "last_processed_at": None,
    "last_error_message": None,
}


class Context:
    def __init__(self, data_dir: Path, logger=None, trace_id: str | None = None, request_source: str = "api"):
        self.data_dir = Path(data_dir).resolve()
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self.logger = logger
        self.trace_id = trace_id or "trace"
        self.request_source = request_source
        self.paths = {
            "config": self.data_dir / "config.json",
            "state": self.data_dir / "state.json",
        }
        ensure_files(self)


def ensure_files(context: Context) -> None:
    if not context.paths["config"].exists():
        save_json_atomic(context.paths["config"], DEFAULT_BACKEND_CONFIG)
    if not context.paths["state"].exists():
        save_json_atomic(context.paths["state"], DEFAULT_STATE)


def save_json_atomic(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, dir=path.parent) as temp_file:
        json.dump(payload, temp_file, ensure_ascii=False, indent=2)
        temp_file.flush()
        temp_name = temp_file.name
    Path(temp_name).replace(path)


def load_json(path: Path, default_payload: Any) -> Any:
    if not path.exists():
        return json.loads(json.dumps(default_payload, ensure_ascii=False))
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return json.loads(json.dumps(default_payload, ensure_ascii=False))


def normalize_backend_config(payload: dict[str, Any] | None) -> dict[str, Any]:
    raw = payload or {}
    config = dict(DEFAULT_BACKEND_CONFIG)
    config["default_operation"] = normalize_operation(raw.get("default_operation"))
    config["overwrite_existing"] = bool(raw.get("overwrite_existing", False))
    config["keyword_rules"] = [normalize_rule(rule) for rule in raw.get("keyword_rules", []) if isinstance(rule, dict)]
    config["embedding_provider"] = normalize_provider(raw.get("embedding_provider"))
    config["embedding_model_path"] = normalize_optional_text(raw.get("embedding_model_path"))
    config["embedding_api_endpoint"] = normalize_optional_text(raw.get("embedding_api_endpoint"))
    config["embedding_api_key"] = normalize_optional_text(raw.get("embedding_api_key"))
    config["embedding_model_name"] = normalize_optional_text(raw.get("embedding_model_name"))
    config["embedding_similarity_threshold"] = max(0.0, min(1.0, float(raw.get("embedding_similarity_threshold", 0.7))))
    return config


def normalize_state(payload: dict[str, Any] | None) -> dict[str, Any]:
    raw = payload or {}
    state = dict(DEFAULT_STATE)
    history = raw.get("operation_history", [])
    if isinstance(history, list):
        state["operation_history"] = [item for item in history if isinstance(item, dict)][-200:]
    state["last_processed_at"] = raw.get("last_processed_at")
    state["last_error_message"] = raw.get("last_error_message")
    return state


def normalize_rule(rule: dict[str, Any]) -> dict[str, Any]:
    return {
        "id": normalize_optional_text(rule.get("id")) or "",
        "keyword": normalize_optional_text(rule.get("keyword")) or "",
        "target_directory": normalize_optional_text(rule.get("target_directory")) or "",
        "is_enabled": bool(rule.get("is_enabled", True)),
        "match_file_name": bool(rule.get("match_file_name", True)),
        "match_extension": bool(rule.get("match_extension", False)),
        "priority": int(rule.get("priority", 0)),
    }


def normalize_optional_text(value: Any) -> str | None:
    text = str(value or "").strip()
    return text or None


def normalize_operation(value: Any) -> str:
    normalized = str(value or "").strip().lower()
    if normalized in {"sort", "move", "copy", "delete"}:
        return normalized
    return "organize"


def normalize_provider(value: Any) -> str:
    normalized = str(value or "").strip().lower()
    if normalized in {"local", "remote", "hybrid"}:
        return normalized
    return "keyword"


def load_config(context: Context) -> dict[str, Any]:
    return normalize_backend_config(load_json(context.paths["config"], DEFAULT_BACKEND_CONFIG))


def patch_config(context: Context, patch_payload: dict[str, Any] | None) -> dict[str, Any]:
    config = load_config(context)
    if patch_payload:
        for key, value in patch_payload.items():
            if key in DEFAULT_BACKEND_CONFIG:
                config[key] = value
    config = normalize_backend_config(config)
    save_json_atomic(context.paths["config"], config)
    return config


def load_state(path: Path) -> dict[str, Any]:
    return normalize_state(load_json(path, DEFAULT_STATE))


def save_state(path: Path, state: dict[str, Any]) -> None:
    save_json_atomic(path, normalize_state(state))


def build_match_result(context: Context, file_path: str) -> dict[str, Any]:
    config = load_config(context)
    source = Path(file_path).expanduser()
    file_name = source.name
    extension = source.suffix
    keywords = extract_keywords(file_name)
    matched_rule = find_matching_rule(config, file_name, extension)
    return {
        "keywords": keywords,
        "recommended_directory": matched_rule["target_directory"] if matched_rule else None,
        "confidence": 1.0 if matched_rule else 0.0,
        "matched_rule_id": matched_rule["id"] if matched_rule else None,
        "matched_keyword": matched_rule["keyword"] if matched_rule else None,
    }


def execute_archive_run(context: Context, payload: dict[str, Any] | None) -> dict[str, Any]:
    request = payload or {}
    operation = normalize_operation(request.get("operation"))
    target_path = str(request.get("target_path") or "").strip()
    if not target_path:
        return {
            "success": False,
            "message": "target_path cannot be empty.",
            "operation": operation,
            "target_path": "",
            "processed_files": [],
        }

    target = Path(target_path).expanduser()
    config = load_config(context)
    state = load_state(context.paths["state"])

    processed_files: list[dict[str, Any]] = []
    try:
        if operation == "delete":
            processed_files.append(process_delete(target))
        elif target.is_file():
            processed_files.append(process_archive_file(target, config, operation))
        elif target.is_dir():
            for child in sorted(target.iterdir()):
                if child.is_file():
                    processed_files.append(process_archive_file(child, config, operation))
        else:
            return {
                "success": False,
                "message": f"Target path does not exist: {target}",
                "operation": operation,
                "target_path": str(target),
                "processed_files": [],
            }
    except Exception as exc:
        state["last_error_message"] = str(exc)
        save_state(context.paths["state"], state)
        raise

    now_text = datetime.now(timezone.utc).isoformat()
    state["last_processed_at"] = now_text
    state["last_error_message"] = None if all(item["success"] for item in processed_files) else "One or more files failed."
    for item in processed_files:
        state["operation_history"].append(
            {
                "occurred_at": now_text,
                "operation": operation,
                "source_path": item.get("source_path", ""),
                "target_path": item.get("target_path"),
                "success": bool(item.get("success")),
                "message": item.get("message", ""),
                "matched_keyword": item.get("matched_keyword"),
            }
        )
    state["operation_history"] = state["operation_history"][-200:]
    save_state(context.paths["state"], state)

    success_count = sum(1 for item in processed_files if item["success"])
    failure_count = len(processed_files) - success_count
    message = f"Processed {len(processed_files)} file(s): {success_count} succeeded"
    if failure_count:
        message += f", {failure_count} failed"

    return {
        "success": failure_count == 0 and len(processed_files) > 0,
        "message": message,
        "operation": operation,
        "target_path": str(target),
        "processed_files": processed_files,
    }


def process_delete(target: Path) -> dict[str, Any]:
    if not target.exists():
        return {
            "source_path": str(target),
            "target_path": None,
            "success": False,
            "message": f"Target path does not exist: {target}",
            "matched_keyword": None,
        }

    if target.is_dir():
        shutil.rmtree(target)
    else:
        target.unlink()
    return {
        "source_path": str(target),
        "target_path": None,
        "success": True,
        "message": "Deleted successfully.",
        "matched_keyword": None,
    }


def process_archive_file(source: Path, config: dict[str, Any], operation: str) -> dict[str, Any]:
    file_name = source.name
    match_result = build_match_result_for_config(config, source)
    target_directory = match_result["recommended_directory"]
    if not target_directory:
        return {
            "source_path": str(source),
            "target_path": None,
            "success": False,
            "message": "No matching rule found.",
            "matched_keyword": match_result["matched_keyword"],
        }

    destination_dir = Path(target_directory).expanduser()
    if not destination_dir.is_absolute():
        destination_dir = source.parent / destination_dir
    destination_dir.mkdir(parents=True, exist_ok=True)

    destination = destination_dir / file_name
    if destination.resolve() == source.resolve():
        return {
            "source_path": str(source),
            "target_path": str(destination),
            "success": True,
            "message": "File is already in the target directory.",
            "matched_keyword": match_result["matched_keyword"],
        }

    if destination.exists() and not bool(config.get("overwrite_existing")):
        destination = destination_dir / build_unique_name(destination)
    elif destination.exists():
        if destination.is_dir():
            shutil.rmtree(destination)
        else:
            destination.unlink()

    if operation == "copy":
        shutil.copy2(source, destination)
    else:
        shutil.move(str(source), str(destination))

    return {
        "source_path": str(source),
        "target_path": str(destination),
        "success": True,
        "message": "Archived successfully.",
        "matched_keyword": match_result["matched_keyword"],
    }


def build_unique_name(destination: Path) -> str:
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    return f"{destination.stem}_{timestamp}{destination.suffix}"


def build_match_result_for_config(config: dict[str, Any], source: Path) -> dict[str, Any]:
    file_name = source.name
    extension = source.suffix
    matched_rule = find_matching_rule(config, file_name, extension)
    return {
        "keywords": extract_keywords(file_name),
        "recommended_directory": matched_rule["target_directory"] if matched_rule else None,
        "confidence": 1.0 if matched_rule else 0.0,
        "matched_rule_id": matched_rule["id"] if matched_rule else None,
        "matched_keyword": matched_rule["keyword"] if matched_rule else None,
    }


def find_matching_rule(config: dict[str, Any], file_name: str, extension: str) -> dict[str, Any] | None:
    enabled_rules = [rule for rule in config.get("keyword_rules", []) if rule.get("is_enabled")]
    enabled_rules.sort(key=lambda item: int(item.get("priority", 0)), reverse=True)
    for rule in enabled_rules:
        keyword = str(rule.get("keyword") or "")
        if not keyword:
            continue
        if rule.get("match_file_name") and keyword.lower() in file_name.lower():
            return rule
        if rule.get("match_extension") and keyword.lower() == extension.lower():
            return rule
    return None


def extract_keywords(file_name: str) -> list[str]:
    stem = Path(file_name).stem
    for separator in ("_", "-", ".", "(", ")", "[", "]"):
        stem = stem.replace(separator, " ")
    return [part for part in stem.split() if len(part) >= 2]
