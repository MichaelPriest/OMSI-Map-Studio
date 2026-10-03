#!/usr/bin/env python3
"""Build a readable PDF user manual from the repository Markdown source."""

from __future__ import annotations

import argparse
import html
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfbase import pdfmetrics
from reportlab.platypus import HRFlowable, ListFlowable, ListItem, Paragraph, SimpleDocTemplate, Spacer


def normalize_text(value: str) -> str:
    replacements = {
        "\u2192": "->",
        "\u2190": "<-",
        "\u2191": "subir",
        "\u2193": "baixar",
        "\u2013": "-",
        "\u2014": "-",
        "\u201c": '"',
        "\u201d": '"',
        "\u2018": "'",
        "\u2019": "'",
        "\u00a0": " ",
        "\u2022": "-",
        "\ufe0f": "",
        "\u26a0": "ATENCAO",
        "\u2705": "OK",
        "\u274c": "ERRO",
    }
    for source, target in replacements.items():
        value = value.replace(source, target)
    return value


def inline_markup(value: str) -> str:
    value = normalize_text(value.strip())
    value = html.escape(value, quote=False)
    value = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", value)
    value = re.sub(r"(?<!\*)\*([^*]+?)\*(?!\*)", r"<i>\1</i>", value)
    value = re.sub(r"\x60([^\x60]+?)\x60", r"<font name='Courier'>\1</font>", value)
    value = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"<u>\1</u>", value)
    return value


def choose_font() -> tuple[str, str]:
    candidates = [
        (
            Path("C:/Windows/Fonts/segoeui.ttf"),
            Path("C:/Windows/Fonts/seguisb.ttf"),
            "SegoeUI",
            "SegoeUISemibold",
        ),
        (
            Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
            Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
            "DejaVu",
            "DejaVuBold",
        ),
    ]
    for regular, bold, regular_name, bold_name in candidates:
        if regular.exists() and bold.exists():
            pdfmetrics.registerFont(TTFont(regular_name, str(regular)))
            pdfmetrics.registerFont(TTFont(bold_name, str(bold)))
            pdfmetrics.registerFontFamily(
                regular_name,
                normal=regular_name,
                bold=bold_name,
                italic=regular_name,
                boldItalic=bold_name,
            )
            return regular_name, bold_name
    return "Helvetica", "Helvetica-Bold"


def build_styles():
    regular, bold = choose_font()
    samples = getSampleStyleSheet()
    body = ParagraphStyle(
        "Body",
        parent=samples["BodyText"],
        fontName=regular,
        fontSize=9.7,
        leading=14,
        textColor=colors.HexColor("#263442"),
        spaceAfter=5,
    )
    return {
        "body": body,
        "title": ParagraphStyle(
            "Title",
            parent=body,
            fontName=bold,
            fontSize=25,
            leading=30,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#123B56"),
            spaceAfter=12,
        ),
        "subtitle": ParagraphStyle(
            "Subtitle",
            parent=body,
            fontSize=10.5,
            leading=15,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#5B6B77"),
            spaceAfter=18,
        ),
        "h1": ParagraphStyle(
            "H1",
            parent=body,
            fontName=bold,
            fontSize=16,
            leading=20,
            textColor=colors.HexColor("#0C5B86"),
            spaceBefore=12,
            spaceAfter=7,
            keepWithNext=True,
        ),
        "h2": ParagraphStyle(
            "H2",
            parent=body,
            fontName=bold,
            fontSize=12.8,
            leading=16,
            textColor=colors.HexColor("#174B68"),
            spaceBefore=9,
            spaceAfter=5,
            keepWithNext=True,
        ),
        "h3": ParagraphStyle(
            "H3",
            parent=body,
            fontName=bold,
            fontSize=11,
            leading=14,
            textColor=colors.HexColor("#345A6F"),
            spaceBefore=7,
            spaceAfter=4,
            keepWithNext=True,
        ),
        "note": ParagraphStyle(
            "Note",
            parent=body,
            leftIndent=8 * mm,
            rightIndent=4 * mm,
            borderColor=colors.HexColor("#66A9C9"),
            borderWidth=0.8,
            borderPadding=6,
            backColor=colors.HexColor("#F1F8FB"),
            textColor=colors.HexColor("#36515F"),
            spaceBefore=4,
            spaceAfter=8,
        ),
        "code": ParagraphStyle(
            "Code",
            parent=body,
            fontName="Courier",
            fontSize=8.5,
            leading=11.5,
            leftIndent=5 * mm,
            rightIndent=5 * mm,
            backColor=colors.HexColor("#F3F5F7"),
            borderColor=colors.HexColor("#D3DCE2"),
            borderWidth=0.5,
            borderPadding=6,
            spaceBefore=4,
            spaceAfter=7,
        ),
        "bullet": ParagraphStyle(
            "Bullet",
            parent=body,
            leftIndent=5 * mm,
            spaceAfter=2,
        ),
        "regular": regular,
    }


def footer(canvas, doc, title: str, font_name: str) -> None:
    canvas.saveState()
    width, _ = A4
    canvas.setStrokeColor(colors.HexColor("#D9E2E8"))
    canvas.setLineWidth(0.5)
    canvas.line(18 * mm, 14 * mm, width - 18 * mm, 14 * mm)
    canvas.setFont(font_name, 7.5)
    canvas.setFillColor(colors.HexColor("#75838D"))
    canvas.drawString(18 * mm, 8.5 * mm, title[:70])
    canvas.drawRightString(width - 18 * mm, 8.5 * mm, f"Pagina {doc.page}")
    canvas.restoreState()


def build_pdf(source: Path, output: Path, language: str) -> None:
    styles = build_styles()
    lines = source.read_text(encoding="utf-8").splitlines()
    doc_title = (
        "OMSI Map Studio - Manual do Usuario"
        if language.lower().startswith("pt")
        else "OMSI Map Studio - User Manual"
    )

    output.parent.mkdir(parents=True, exist_ok=True)
    doc = SimpleDocTemplate(
        str(output),
        pagesize=A4,
        rightMargin=18 * mm,
        leftMargin=18 * mm,
        topMargin=17 * mm,
        bottomMargin=19 * mm,
        title=doc_title,
        author="OMSI Map Studio",
        subject="User manual",
    )

    story = []
    paragraph_lines = []
    list_items = []
    code_lines = []
    in_code = False
    seen_title = False

    def flush_paragraph():
        nonlocal paragraph_lines
        if paragraph_lines:
            text = " ".join(x.strip() for x in paragraph_lines if x.strip())
            if text:
                story.append(Paragraph(inline_markup(text), styles["body"]))
            paragraph_lines = []

    def flush_list():
        nonlocal list_items
        if list_items:
            ordered = all(flag for _, flag in list_items)
            entries = [
                ListItem(Paragraph(inline_markup(text), styles["bullet"]))
                for text, _ in list_items
            ]
            story.append(
                ListFlowable(
                    entries,
                    bulletType="1" if ordered else "bullet",
                    leftIndent=7 * mm,
                    bulletFontName=styles["regular"],
                    bulletFontSize=8,
                    bulletColor=colors.HexColor("#0C5B86"),
                    spaceAfter=5,
                )
            )
            list_items = []

    def flush_code():
        nonlocal code_lines
        if code_lines:
            encoded = "<br/>".join(
                html.escape(normalize_text(line), quote=False).replace(" ", "&nbsp;")
                for line in code_lines
            )
            story.append(Paragraph(encoded or "&nbsp;", styles["code"]))
            code_lines = []

    for line in lines:
        stripped = line.strip()

        if stripped.startswith(chr(96) * 3):
            flush_paragraph()
            flush_list()
            if in_code:
                flush_code()
            in_code = not in_code
            continue

        if in_code:
            code_lines.append(line.rstrip())
            continue

        if not stripped:
            flush_paragraph()
            flush_list()
            continue

        if stripped == "---":
            flush_paragraph()
            flush_list()
            story.append(
                HRFlowable(
                    width="100%",
                    thickness=0.6,
                    color=colors.HexColor("#CAD5DC"),
                    spaceBefore=5,
                    spaceAfter=8,
                )
            )
            continue

        heading = re.match(r"^(#{1,3})\s+(.+)$", stripped)
        if heading:
            flush_paragraph()
            flush_list()
            level = len(heading.group(1))
            heading_text = heading.group(2)
            if level == 1 and not seen_title:
                story.append(Spacer(1, 16 * mm))
                story.append(Paragraph(inline_markup(heading_text), styles["title"]))
                story.append(
                    Paragraph(
                        "WinUI 3 + Direct3D 11 | Alpha de desenvolvimento"
                        if language.lower().startswith("pt")
                        else "WinUI 3 + Direct3D 11 | Development Alpha",
                        styles["subtitle"],
                    )
                )
                story.append(
                    HRFlowable(
                        width="65%",
                        thickness=1.2,
                        color=colors.HexColor("#28A5DA"),
                        hAlign="CENTER",
                        spaceAfter=12,
                    )
                )
                seen_title = True
            else:
                style = styles["h1"] if level == 1 else styles["h2"] if level == 2 else styles["h3"]
                story.append(Paragraph(inline_markup(heading_text), style))
            continue

        if stripped.startswith(">"):
            flush_paragraph()
            flush_list()
            story.append(Paragraph(inline_markup(stripped[1:].strip()), styles["note"]))
            continue

        unordered = re.match(r"^[-*]\s+(.+)$", stripped)
        ordered = re.match(r"^\d+[.)]\s+(.+)$", stripped)
        if unordered or ordered:
            flush_paragraph()
            match = unordered or ordered
            list_items.append((match.group(1), ordered is not None))
            continue

        flush_list()
        paragraph_lines.append(stripped)

    flush_paragraph()
    flush_list()
    flush_code()

    doc.build(
        story,
        onFirstPage=lambda canvas, d: footer(canvas, d, doc_title, styles["regular"]),
        onLaterPages=lambda canvas, d: footer(canvas, d, doc_title, styles["regular"]),
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--language", default="pt-BR")
    args = parser.parse_args()
    build_pdf(args.source, args.output, args.language)


if __name__ == "__main__":
    main()
