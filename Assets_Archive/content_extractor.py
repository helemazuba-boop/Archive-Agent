"""
文件内容提取器 — 从常见格式文件中提取文本预览。

支持格式：
- 纯文本格式（.txt/.md/.json 等）：直接读取 UTF-8 文本
- .docx：解析 word/document.xml 提取 <w:t> 标签文本
- .xlsx：解析 xl/sharedStrings.xml 提取 <t> 标签文本
- .pptx：解析 ppt/slides/*.xml 提取 <a:t> 标签文本
- 其他格式：返回 None（兜底）
"""

from __future__ import annotations

import re
import zipfile
from pathlib import Path


# ---------------------------------------------------------------------------
# Dispatch 表
# ---------------------------------------------------------------------------

TEXT_EXTENSIONS: frozenset[str] = frozenset({
    # 文档类
    ".txt", ".md", ".rst",
    # 数据/配置类
    ".json", ".jsonl", ".xml", ".xaml", ".svg",
    # Web
    ".html", ".htm", ".xhtml",
    # 样式表
    ".css", ".scss", ".sass", ".less",
    # 配置
    ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".log",
    # 数据
    ".csv", ".tsv",
    # 代码（按语言列出主要扩展名）
    ".py", ".pyw",
    ".js", ".mjs", ".cjs", ".jsx",
    ".ts", ".tsx",
    ".cs",
    ".java", ".kt", ".swift",
    ".c", ".cpp", ".cxx", ".h", ".hpp",
    ".go", ".rs",
    ".rb", ".php",
    ".sh", ".bash", ".zsh",
    ".ps1", ".bat", ".cmd",
    ".sql",
    ".graphql", ".gql",
    # 其他
    ".srt", ".vtt",
    ".tex",
    ".vim", ".vimrc",
    ".editorconfig",
    ".gitignore", ".gitattributes",
    ".dockerfile",
    ".env", ".env.example",
    ".properties",
    ".gradle",
    ".applescript",
    ".lua",
    ".r",
    ".scala",
})


def extract_preview(file_path: str, max_chars: int = 2048) -> str | None:
    """
    提取文件开头文本内容（最多 max_chars 字符），失败返回 None。

    Args:
        file_path: 文件路径
        max_chars: 最大提取字符数

    Returns:
        提取的文本内容，或 None（不支持的格式或提取失败）
    """
    path = Path(file_path)

    if not path.exists() or not path.is_file():
        return None

    ext = path.suffix.lower()

    # 纯文本格式
    if ext in TEXT_EXTENSIONS:
        return _extract_text(path, max_chars)

    # Office 套件
    if ext == ".docx":
        return _extract_docx(path, max_chars)
    if ext == ".xlsx":
        return _extract_xlsx(path, max_chars)
    if ext == ".pptx":
        return _extract_pptx(path, max_chars)

    # 不支持的二进制格式
    return None


def _extract_text(path: Path, max_chars: int) -> str | None:
    """直接读取 UTF-8 纯文本文件"""
    try:
        content = path.read_text(encoding="utf-8")
        return content[:max_chars]
    except Exception:
        return None


def _extract_docx(path: Path, max_chars: int) -> str | None:
    """从 .docx 中提取 <w:t> 文本"""
    try:
        with zipfile.ZipFile(path, "r") as zf:
            with zf.open("word/document.xml") as f:
                xml = f.read().decode("utf-8", errors="replace")
        # 提取 <w:t ...>...<.../w:t> 文本
        matches = re.findall(r"<w:t[^>]*>([^<]*)</w:t>", xml)
        text = "".join(matches)
        return text[:max_chars]
    except Exception:
        return None


def _extract_xlsx(path: Path, max_chars: int) -> str | None:
    """从 .xlsx 中提取 xl/sharedStrings.xml 的 <t> 文本"""
    try:
        with zipfile.ZipFile(path, "r") as zf:
            try:
                with zf.open("xl/sharedStrings.xml") as f:
                    xml = f.read().decode("utf-8", errors="replace")
            except KeyError:
                return None
        # 取前 80 个 <t> 标签文本，用 " | " 连接
        matches = re.findall(r"<t[^>]*>([^<]*)</t>", xml)
        snippets = matches[:80]
        text = " | ".join(snippets)
        return text[:max_chars]
    except Exception:
        return None


def _extract_pptx(path: Path, max_chars: int) -> str | None:
    """从 .pptx 中提取前 5 张幻灯片的 <a:t> 文本"""
    try:
        with zipfile.ZipFile(path, "r") as zf:
            slide_files = sorted([n for n in zf.namelist() if re.match(r"ppt/slides/slide\d+\.xml", n)])
            slide_files = slide_files[:5]  # 最多 5 张

        parts = []
        for slide_file in slide_files:
            try:
                with zipfile.ZipFile(path, "r") as zf:
                    with zf.open(slide_file) as f:
                        xml = f.read().decode("utf-8", errors="replace")
                matches = re.findall(r"<a:t[^>]*>([^<]*)</a:t>", xml)
                parts.append("".join(matches))
            except Exception:
                continue

        if not parts:
            return None

        return ("\n---\n".join(parts))[:max_chars]
    except Exception:
        return None
