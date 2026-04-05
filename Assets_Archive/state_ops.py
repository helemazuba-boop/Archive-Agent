from __future__ import annotations

import json
import shutil
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from content_extractor import extract_preview
    from llm_client import LLMClassificationResult

DEFAULT_BACKEND_CONFIG = {
    "default_operation": "organize",
    "overwrite_existing": False,
    "keyword_rules": [],
    "embedding_provider": "keyword",   # keyword | llm | hybrid
    "embedding_model_path": None,
    "embedding_api_endpoint": None,
    "embedding_api_key": None,
    "embedding_model_name": None,
    "embedding_similarity_threshold": 0.7,
    "match_mode": "word",
    # LLM 分类配置
    "llm_enabled": False,
    "llm_api_endpoint": None,
    "llm_api_key": None,
    "llm_model_name": None,
    "llm_fallback_threshold": 0.5,    # 当 LLM confidence >= 此值时使用 LLM 结果
    "llm_include_content": False,
    "llm_content_max_chars": 2048,
]

DEFAULT_STATE = {
    "operation_history": [],
    "undo_records": [],
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
    # LLM 字段归一化
    config["llm_enabled"] = bool(raw.get("llm_enabled", False))
    config["llm_api_endpoint"] = normalize_optional_text(raw.get("llm_api_endpoint"))
    config["llm_api_key"] = normalize_optional_text(raw.get("llm_api_key"))
    config["llm_model_name"] = normalize_optional_text(raw.get("llm_model_name"))
    config["llm_fallback_threshold"] = max(0.0, min(1.0, float(raw.get("llm_fallback_threshold", 0.5))))
    config["llm_include_content"] = bool(raw.get("llm_include_content", False))
    config["llm_content_max_chars"] = max(128, min(8192, int(raw.get("llm_content_max_chars", 2048))))
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
        "match_mode": normalize_match_mode(rule.get("match_mode")),
        "priority": int(rule.get("priority", 0)),
    }


def normalize_match_mode(value: Any) -> str:
    normalized = str(value or "").strip().lower()
    if normalized in {"prefix", "suffix", "exact", "word", "substring"}:
        return normalized
    return "word"


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

    if matched_rule:
        return {
            "keywords": keywords,
            "recommended_directory": matched_rule["target_directory"],
            "confidence": 1.0,
            "matched_rule_id": matched_rule["id"],
            "matched_keyword": matched_rule["keyword"],
            "match_source": "keyword",
        }

    if config.get("llm_enabled"):
        llm_result = _llm_classify(source, config)
        return {
            "keywords": keywords,
            "recommended_directory": llm_result.recommended_directory,
            "confidence": llm_result.confidence,
            "matched_rule_id": None,
            "matched_keyword": llm_result.reasoning,
            "match_source": "llm",
        }

    return {
        "keywords": keywords,
        "recommended_directory": None,
        "confidence": 0.0,
        "matched_rule_id": None,
        "matched_keyword": None,
        "match_source": None,
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

    _append_undo_record(source, destination, operation)

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

    if matched_rule:
        return {
            "keywords": extract_keywords(file_name),
            "recommended_directory": matched_rule["target_directory"],
            "confidence": 1.0,
            "matched_rule_id": matched_rule["id"],
            "matched_keyword": matched_rule["keyword"],
            "match_source": "keyword",
        }

    # 关键字未命中时，尝试 LLM 分类
    if config.get("llm_enabled"):
        llm_result = _llm_classify(source, config)
        return {
            "keywords": extract_keywords(file_name),
            "recommended_directory": llm_result.recommended_directory,
            "confidence": llm_result.confidence,
            "matched_rule_id": None,
            "matched_keyword": llm_result.reasoning,
            "match_source": "llm",
        }

    return {
        "keywords": extract_keywords(file_name),
        "recommended_directory": None,
        "confidence": 0.0,
        "matched_rule_id": None,
        "matched_keyword": None,
        "match_source": None,
    }


def _llm_classify(source: Path, config: dict[str, Any]) -> "LLMClassificationResult":
    """使用 LLM 对文件进行智能分类（兜底）"""
    try:
        from content_extractor import extract_preview
        from llm_client import create_llm_client
        client = create_llm_client(config)
        if not client.is_configured:
            return LLMClassificationResult(None, 0.0, error="LLM 未配置")

        existing_dirs = _collect_existing_directories(config)

        file_content = None
        if config.get("llm_include_content", False):
            max_chars = config.get("llm_content_max_chars", 2048)
            file_content = extract_preview(str(source), max_chars=max_chars)

        return client.classify(
            file_name=source.name,
            extension=source.suffix,
            existing_dirs=existing_dirs,
            file_content=file_content,
        )
    except Exception as exc:
        return LLMClassificationResult(None, 0.0, error=str(exc))


def _collect_existing_directories(config: dict[str, Any]) -> list[str]:
    """从已配置的规则中收集已有的目标目录"""
    dirs = set()
    for rule in config.get("keyword_rules", []):
        td = rule.get("target_directory", "").strip()
        if td:
            dirs.add(td)
    return sorted(dirs)


def find_matching_rule(config: dict[str, Any], file_name: str, extension: str) -> dict[str, Any] | None:
    enabled_rules = [rule for rule in config.get("keyword_rules", []) if rule.get("is_enabled")]
    enabled_rules.sort(key=lambda item: int(item.get("priority", 0)), reverse=True)
    for rule in enabled_rules:
        keyword = str(rule.get("keyword") or "").strip()
        if not keyword:
            continue

        if rule.get("match_extension"):
            # Extension matching: treat keyword as a full extension string (with or without dot)
            # Normalize both to lower-case and ensure dot prefix
            ext_key = keyword.lower()
            if not ext_key.startswith("."):
                ext_key = "." + ext_key
            ext_target = extension.lower()
            if ext_key == ext_target:
                return rule

        if rule.get("match_file_name"):
            match_mode = str(rule.get("match_mode") or "word")
            if _match_keyword_in_filename(file_name.lower(), keyword.lower(), match_mode):
                return rule

    return None


def _match_keyword_in_filename(file_name_lower: str, keyword_lower: str, match_mode: str) -> bool:
    if not keyword_lower:
        return False

    match match_mode:
        case "prefix":
            return file_name_lower.startswith(keyword_lower)

        case "suffix":
            # Strip extension before checking suffix for cleaner UX
            stem = file_name_lower
            if "." in stem:
                stem = stem.rsplit(".", 1)[0]
            return stem.endswith(keyword_lower)

        case "exact":
            stem = file_name_lower
            if "." in stem:
                stem = stem.rsplit(".", 1)[0]
            return stem == keyword_lower

        case "word":
            # Tokenize the file name stem (exclude extension) and check if keyword is a whole word
            stem = file_name_lower
            if "." in stem:
                stem = stem.rsplit(".", 1)[0]
            return _is_word_match(stem, keyword_lower)

        case "substring":
            return keyword_lower in file_name_lower

        case _:
            # Default to word mode for backwards compatibility
            stem = file_name_lower
            if "." in stem:
                stem = stem.rsplit(".", 1)[0]
            return _is_word_match(stem, keyword_lower)


def _is_word_match(text: str, keyword: str) -> bool:
    """
    Check if `keyword` appears in `text` as a whole word.
    A whole word means it is surrounded by word boundaries (non-alphanumeric characters or string edges).
    """
    import re
    # Escape special regex characters in keyword
    escaped = re.escape(keyword)
    pattern = r"(?<![A-Za-z0-9])" + escaped + r"(?![A-Za-z0-9])"
    return bool(re.search(pattern, text))


def extract_keywords(file_name: str) -> list[str]:
    stem = Path(file_name).stem
    for separator in ("_", "-", ".", "(", ")", "[", "]"):
        stem = stem.replace(separator, " ")
    return [part for part in stem.split() if len(part) >= 2]


def _get_raw_config() -> dict[str, Any]:
    data_dir = Path(__file__).parent.resolve()
    config_path = data_dir / "config.json"
    if config_path.exists():
        return json.loads(config_path.read_text(encoding="utf-8-sig"))
    return dict(DEFAULT_BACKEND_CONFIG)


def _save_raw_config(raw: dict[str, Any]):
    data_dir = Path(__file__).parent.resolve()
    config_path = data_dir / "config.json"
    config_path.parent.mkdir(parents=True, exist_ok=True)
    save_json_atomic(config_path, raw)


def _append_undo_record(source: Path, target: Path, operation: str):
    raw = _get_raw_config()
    records = raw.get("undo_records", [])
    records.insert(0, {
        "source": str(source),
        "target": str(target),
        "operation": operation,
        "timestamp": datetime.now(timezone.utc).isoformat(),
    })
    raw["undo_records"] = records[:50]
    _save_raw_config(raw)


def undo_operation(target_path: str, operation: str = "organize") -> dict[str, Any]:
    """
    撤销最近一次指定目标路径的整理操作。
    支持 move/organize -> 反向移动; copy -> 删除复制的文件。
    返回 {"success": bool, "message": str}
    """
    raw = _get_raw_config()
    records = raw.get("undo_records", [])
    for rec in records:
        if rec.get("target") == target_path:
            source = Path(rec["source"])
            target = Path(rec["target"])
            op = rec["operation"]
            if not source.exists():
                return {"success": False, "message": f"源文件不存在: {source}"}
            if op in ("move", "organize"):
                source.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(target), str(source))
            elif op == "copy":
                if target.exists():
                    target.unlink()
            records.remove(rec)
            raw["undo_records"] = records
            _save_raw_config(raw)
            return {"success": True, "message": f"已撤销: {target} -> {source}"}
    return {"success": False, "message": "未找到可撤销的记录"}
