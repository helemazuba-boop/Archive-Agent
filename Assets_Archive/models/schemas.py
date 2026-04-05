from __future__ import annotations

from typing import Optional

from pydantic import BaseModel, ConfigDict


class KeywordRuleModel(BaseModel):
    model_config = ConfigDict(extra="forbid")

    id: str = ""
    keyword: str = ""
    target_directory: str = ""
    is_enabled: bool = True
    match_file_name: bool = True
    match_extension: bool = False
    match_mode: str = "word"  # prefix | suffix | exact | word | substring
    priority: int = 0


class ArchiveBackendConfigPatch(BaseModel):
    model_config = ConfigDict(extra="forbid")

    default_operation: Optional[str] = None
    overwrite_existing: Optional[bool] = None
    keyword_rules: Optional[list[KeywordRuleModel]] = None
    embedding_provider: Optional[str] = None
    embedding_model_path: Optional[str] = None
    embedding_api_endpoint: Optional[str] = None
    embedding_api_key: Optional[str] = None
    embedding_model_name: Optional[str] = None
    embedding_similarity_threshold: Optional[float] = None
    llm_enabled: Optional[bool] = None
    llm_api_endpoint: Optional[str] = None
    llm_api_key: Optional[str] = None
    llm_model_name: Optional[str] = None
    llm_fallback_threshold: Optional[float] = None
    llm_include_content: Optional[bool] = None
    llm_content_max_chars: Optional[int] = None


class ArchiveMatchRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    file_path: str


class ArchiveRunRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    operation: str
    target_path: str
