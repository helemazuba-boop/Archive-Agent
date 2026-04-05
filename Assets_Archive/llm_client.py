"""
LLM 客户端 — 为文件分类提供大语言模型推理能力。

支持任意 OpenAI 兼容 API 端点（包括 OpenAI、Azure OpenAI、Ollama、SiliconFlow、DeepSeek 等）。
"""

from __future__ import annotations

import json
import time
import urllib.request
import urllib.error
from dataclasses import dataclass, field
from pathlib import Path


# ---------------------------------------------------------------------------
# 数据结构
# ---------------------------------------------------------------------------

@dataclass
class LLMClassificationResult:
    """LLM 分类结果"""
    recommended_directory: str | None
    confidence: float
    reasoning: str | None = None
    error: str | None = None


# ---------------------------------------------------------------------------
# Prompt 工程
# ---------------------------------------------------------------------------

DEFAULT_CLASSIFICATION_PROMPT = """你是一个文件整理助手。请根据文件名**和内容**判断这个文件应该归属哪个分类目录。

**文件名**: {file_name}
**文件扩展名**: {extension}
**已有目录**: {existing_dirs}
{file_content_section}

请从以下方式中选择最合适的一种来推荐目录：
1. 如果文件名能明确反映文件类型或内容，直接推荐对应目录（如"图片"→Images，"视频"→Videos）
2. 如果文件名包含项目/日期/作者等信息，使用这些信息（如"MyProject/docs"）
3. 如果不确定，选择最通用的合适目录（如 Documents）
4. 如果完全没有线索，返回 None

**要求**：
- 只返回 JSON 格式，不要包含任何解释文字
- JSON 字段：recommended_directory（字符串目录名或null），confidence（0.0-1.0），reasoning（简短推理，英文或中文均可）

**示例**：
- 文件名："vacation_2024.jpg" → {{"recommended_directory": "Photos/Vacation", "confidence": 0.9, "reasoning": "Vacation photos"}}
- 文件名："report_final.docx" → {{"recommended_directory": "Documents/Reports", "confidence": 0.85, "reasoning": "Document file"}}
- 文件名："data_abc" → {{"recommended_directory": "Others", "confidence": 0.4, "reasoning": "Ambiguous"}}
"""


def _build_prompt(file_name: str, extension: str, existing_dirs: list[str], file_content: str | None = None) -> str:
    dirs_str = ", ".join(existing_dirs) if existing_dirs else "（无，请自行判断）"
    if file_content:
        file_content_section = f"""
**文件内容预览（开头）**:
```
{file_content}
```
"""
    else:
        file_content_section = ""
    return DEFAULT_CLASSIFICATION_PROMPT.format(
        file_name=file_name,
        extension=extension,
        existing_dirs=dirs_str,
        file_content_section=file_content_section,
    )


# ---------------------------------------------------------------------------
# LLM 客户端
# ---------------------------------------------------------------------------

class LLMClassificationClient:
    """
    通用的 OpenAI 兼容格式 LLM 客户端。

    支持以下 API 格式：
    - OpenAI 官方（api.openai.com）
    - Azure OpenAI（*.openai.azure.com）
    - Ollama（localhost:11434）
    - SiliconFlow、DeepSeek 等兼容 OpenAI 格式的 API
    - vLLM、LocalAI 等
    """

    def __init__(
        self,
        api_endpoint: str | None,
        api_key: str | None,
        model_name: str | None,
        timeout_seconds: int = 30,
        max_retries: int = 2,
    ):
        self._api_endpoint = api_endpoint
        self._api_key = api_key
        self._model_name = model_name
        self._timeout = timeout_seconds
        self._max_retries = max_retries

    @property
    def is_configured(self) -> bool:
        """是否已正确配置"""
        return bool(self._api_endpoint and self._model_name)

    def classify(
        self,
        file_name: str,
        extension: str,
        existing_dirs: list[str],
        file_content: str | None = None,
    ) -> LLMClassificationResult:
        """
        使用 LLM 对文件进行分类。

        Returns:
            LLMClassificationResult: 包含推荐目录、置信度和推理过程
        """
        if not self.is_configured:
            return LLMClassificationResult(
                recommended_directory=None,
                confidence=0.0,
                error="LLM 未配置（api_endpoint 或 model_name 为空）",
            )

        prompt = _build_prompt(file_name, extension, existing_dirs, file_content)
        payload = self._build_payload(prompt)

        for attempt in range(self._max_retries + 1):
            try:
                response_body = self._send_request(payload)
                return self._parse_response(response_body)
            except LLMError as exc:
                if attempt == self._max_retries:
                    return LLMClassificationResult(
                        recommended_directory=None,
                        confidence=0.0,
                        error=f"LLM 请求失败（已重试 {self._max_retries + 1} 次）: {exc}",
                    )
                time.sleep(0.5 * (attempt + 1))

        return LLMClassificationResult(
            recommended_directory=None,
            confidence=0.0,
            error="LLM 请求未知错误",
        )

    # ------------------------------------------------------------------
    # 子类可覆盖的接口
    # ------------------------------------------------------------------

    def _build_payload(self, prompt: str) -> dict:
        """
        构建请求体。子类（如 Azure 客户端）可覆盖此方法。
        """
        messages = [{"role": "user", "content": prompt}]
        payload: dict[str, object] = {
            "model": self._model_name or "gpt-4o-mini",
            "messages": messages,
            "temperature": 0.1,
            "response_format": {"type": "json_object"},
        }
        return payload

    def _send_request(self, payload: dict) -> str:
        """发送 HTTP 请求"""
        endpoint = self._resolve_endpoint()
        body = json.dumps(payload).encode("utf-8")

        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json",
        }
        if self._api_key:
            # 部分 API（如 OpenAI、SiliconFlow）使用 Bearer 认证
            # 部分 API（如 Ollama）不使用认证
            auth_key = self._api_key.strip()
            if auth_key.lower() not in ("", "none", "null"):
                headers["Authorization"] = f"Bearer {auth_key}"

        req = urllib.request.Request(
            endpoint,
            data=body,
            headers=headers,
            method="POST",
        )

        try:
            with urllib.request.urlopen(req, timeout=self._timeout) as resp:
                return resp.read().decode("utf-8")
        except urllib.error.HTTPError as exc:
            error_body = exc.read().decode("utf-8", errors="replace")
            raise LLMError(f"HTTP {exc.code}: {error_body[:300]}")
        except urllib.error.URLError as exc:
            raise LLMError(f"网络错误: {exc.reason}")
        except TimeoutError:
            raise LLMError(f"请求超时（{self._timeout}s）")
        except Exception as exc:
            raise LLMError(str(exc))

    def _resolve_endpoint(self) -> str:
        """解析 API 端点"""
        ep = self._api_endpoint or ""
        if ep.rstrip("/").endswith("/chat/completions"):
            return ep.rstrip("/")
        return ep.rstrip("/") + "/chat/completions"

    def _parse_response(self, response_body: str) -> LLMClassificationResult:
        """从响应体解析分类结果"""
        try:
            data = json.loads(response_body)
        except json.JSONDecodeError as exc:
            raise LLMError(f"响应非 JSON: {exc}")

        # 兼容 OpenAI / Azure / Ollama 格式
        choices = (
            data.get("choices")
            or data.get("data", {}).get("choices")
            or [{}]
        )
        if not choices or not isinstance(choices, list):
            raise LLMError(f"响应缺少 choices 字段: {response_body[:200]}")

        raw_message = (
            choices[0].get("message", {})
            .get("content", "")
            or choices[0].get("text", "")
        )

        # 尝试从 JSON 内容中提取
        content = raw_message.strip()
        try:
            result = json.loads(content)
        except json.JSONDecodeError:
            # 尝试去除 markdown 代码块
            if content.startswith("```"):
                lines = content.splitlines()
                content = "\n".join(lines[1:-1] if lines[-1].strip() == "```" else lines[1:])
            try:
                result = json.loads(content.strip())
            except json.JSONDecodeError:
                raise LLMError(f"LLM 返回内容非 JSON: {content[:200]}")

        rec_dir = result.get("recommended_directory")
        if rec_dir is not None and not isinstance(rec_dir, str):
            rec_dir = None
        if isinstance(rec_dir, str):
            rec_dir = rec_dir.strip() or None

        confidence = float(result.get("confidence", 0.0))
        confidence = max(0.0, min(1.0, confidence))

        return LLMClassificationResult(
            recommended_directory=rec_dir,
            confidence=confidence,
            reasoning=result.get("reasoning"),
        )


class LLMError(Exception):
    """LLM 请求相关的错误"""
    pass


# ---------------------------------------------------------------------------
# 工厂函数
# ---------------------------------------------------------------------------

def create_llm_client(config: dict[str, Any]) -> LLMClassificationClient:
    """
    根据配置创建 LLM 客户端。

    从 config 中读取 llm_ 相关字段。
    """
    api_endpoint = config.get("llm_api_endpoint") or config.get("embedding_api_endpoint")
    api_key = config.get("llm_api_key") or config.get("embedding_api_key")
    model_name = config.get("llm_model_name") or config.get("embedding_model_name")

    return LLMClassificationClient(
        api_endpoint=api_endpoint,
        api_key=api_key,
        model_name=model_name,
    )


def is_llm_available(config: dict[str, Any]) -> bool:
    """检查 LLM 是否已配置"""
    client = create_llm_client(config)
    return client.is_configured
