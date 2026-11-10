"""Build the Russian project guide. Run from any directory with Python + ReportLab.

Windows fonts: Arial and Consolas from C:/Windows/Fonts.
Linux fonts: DejaVu Sans/Mono from /usr/share/fonts/truetype/dejavu.
Set HELPDESK_FONT_DIR to a directory containing either family to override.
The API itself does not require Python or this script.
"""
from __future__ import annotations

import html
import os
import re
import textwrap
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, PageBreak, CondPageBreak, Table, TableStyle,
    Flowable, Preformatted, KeepTogether,
)
from reportlab.platypus.tableofcontents import TableOfContents

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/pdf/HelpDesk-Development-Guide-RU.pdf'
NAVY = colors.HexColor('#16324F')
TEAL = colors.HexColor('#007F86')
PALE = colors.HexColor('#EDF5F7')
INK = colors.HexColor('#243447')
GREY = colors.HexColor('#60758A')
WIDTH = A4[0] - 100


def register_fonts():
    dirs = [os.environ.get('HELPDESK_FONT_DIR', ''), 'C:/Windows/Fonts',
            '/usr/share/fonts/truetype/dejavu']
    families = [('arial.ttf', 'arialbd.ttf', 'ariali.ttf', 'consola.ttf'),
                ('DejaVuSans.ttf', 'DejaVuSans-Bold.ttf', 'DejaVuSans-Oblique.ttf', 'DejaVuSansMono.ttf')]
    for directory in dirs:
        for names in families:
            paths = [Path(directory) / name for name in names]
            if all(p.is_file() for p in paths):
                for name, path in zip(['Body', 'BodyBold', 'BodyItalic', 'Code'], paths):
                    pdfmetrics.registerFont(TTFont(name, str(path)))
                pdfmetrics.registerFontFamily('Body', normal='Body', bold='BodyBold', italic='BodyItalic', boldItalic='BodyBold')
                return
    raise SystemExit('Install Arial/Consolas or DejaVu Sans/Mono; or set HELPDESK_FONT_DIR.')


register_fonts()
styles = getSampleStyleSheet()
styles.add(ParagraphStyle('TextRu', fontName='Body', fontSize=10.5, leading=15.5,
                          textColor=INK, spaceAfter=7, alignment=TA_LEFT, allowWidows=0))
styles.add(ParagraphStyle('ChapterRu', fontName='BodyBold', fontSize=22, leading=28,
                          textColor=NAVY, spaceAfter=16, keepWithNext=True))
styles.add(ParagraphStyle('SubRu', fontName='BodyBold', fontSize=13, leading=18,
                          textColor=TEAL, spaceBefore=10, spaceAfter=6, keepWithNext=True))
styles.add(ParagraphStyle('CellRu', parent=styles['TextRu'], fontSize=8.5, leading=12, spaceAfter=0))
styles.add(ParagraphStyle('CodeRu', fontName='Code', fontSize=7.9, leading=11,
                          backColor=PALE, borderPadding=9, spaceBefore=4, spaceAfter=12))
styles.add(ParagraphStyle('BulletRu', parent=styles['TextRu'], leftIndent=12, firstLineIndent=-9))
styles.add(ParagraphStyle('LabelRu', parent=styles['TextRu'], keepWithNext=True))
styles.add(ParagraphStyle('TocRu', fontName='Body', fontSize=10, leading=14,
                          textColor=NAVY, spaceBefore=3, rightIndent=20))


def inline(value):
    value = html.escape(value)
    value = re.sub(r'\[([^\]]+)\]\((https?://[^)]+)\)',
                   r'<link href="\2" color="#007F86"><u>\1</u></link>', value)
    value = re.sub(r'\*\*([^*]+)\*\*', r'<b>\1</b>', value)
    value = re.sub(r'`([^`]+)`', r'<font name="Code" size="9">\1</font>', value)
    # Permit wrapping of long repository paths without inserting visible characters.
    return value


class Diagram(Flowable):
    def __init__(self, kind):
        super().__init__()
        self.kind = kind
        self.width = WIDTH
        self.height = {'architecture': 176, 'schema': 224, 'workflow': 106}[kind]

    def draw(self):
        c = self.canv
        def box(x, y, w, h, title, lines=()):
            c.setFillColor(PALE); c.setStrokeColor(TEAL)
            c.roundRect(x, y, w, h, 7, fill=1, stroke=1)
            c.setFillColor(NAVY); c.setFont('BodyBold', 10)
            c.drawCentredString(x+w/2, y+h-18, title)
            c.setFont('Body', 8.5)
            for i, line in enumerate(lines): c.drawString(x+10, y+h-34-i*13, line)
        def arrow(x1, y1, x2, y2, label=None):
            import math
            c.setStrokeColor(GREY); c.setFillColor(GREY)
            c.line(x1, y1, x2, y2)
            angle = math.atan2(y2-y1, x2-x1)
            p = c.beginPath(); p.moveTo(x2, y2)
            p.lineTo(x2-7*math.cos(angle-.4), y2-7*math.sin(angle-.4))
            p.lineTo(x2-7*math.cos(angle+.4), y2-7*math.sin(angle+.4)); p.close()
            c.drawPath(p, fill=1, stroke=0)
            if label:
                c.setFont('Body', 8); c.drawCentredString((x1+x2)/2, (y1+y2)/2+5, label)
        if self.kind == 'architecture':
            box(0, 112, 142, 55, 'Клиент / HTTP', ['Swagger, curl, тест'])
            box(176, 112, 142, 55, 'Controllers', ['Маршрут, DTO, ответ'])
            arrow(142, 138, 176, 138)
            box(176, 40, 142, 55, 'Application', ['Сценарий и доступ'])
            arrow(247, 112, 247, 95)
            box(353, 40, 142, 55, 'Domain', ['Правила Ticket'])
            arrow(318, 67, 353, 67)
            box(0, 0, 160, 55, 'Infrastructure', ['Store / EF / SQL Server'])
            arrow(176, 55, 160, 35)
            c.setFont('Body', 8.5); c.setFillColor(GREY)
            c.drawString(190, 18, 'ITicketStore отделяет сценарий от SQL')
        elif self.kind == 'schema':
            box(0, 113, 164, 101, 'AspNetUsers', ['Id (PK)', 'Email, PasswordHash', 'Роли через Identity'])
            box(285, 113, 210, 101, 'Tickets', ['Id (PK), OwnerId (FK)', 'AssigneeId (FK, nullable)', 'Title, Status, Version, даты'])
            box(285, 0, 210, 82, 'Comments', ['TicketId (FK), AuthorId (FK)', 'Body, CreatedAt'])
            arrow(164, 165, 285, 165, '1 : N, владелец')
            arrow(390, 113, 390, 82, '1 : N')
            arrow(82, 113, 285, 38, '1 : N, автор')
        else:
            box(0, 41, 125, 48, 'Open')
            box(185, 41, 125, 48, 'InProgress')
            box(370, 41, 125, 48, 'Closed')
            arrow(125, 65, 185, 65, 'take')
            arrow(310, 65, 370, 65, 'close')
            c.setStrokeColor(GREY)
            c.line(432, 41, 432, 12); c.line(432, 12, 63, 12)
            arrow(63, 12, 63, 41)
            c.setFont('Body', 8); c.setFillColor(GREY); c.drawCentredString(247, 17, 'reopen: снять исполнителя')


class GuideDoc(SimpleDocTemplate):
    def afterFlowable(self, flowable):
        if isinstance(flowable, Paragraph) and flowable.style.name == 'ChapterRu':
            title = flowable.getPlainText()
            key = 'chapter-' + title.split('.')[0]
            self.canv.bookmarkPage(key)
            self.canv.addOutlineEntry(title, key, level=0)
            self.notify('TOCEntry', (0, title, self.page, key))


def frame(canvas, doc):
    canvas.saveState()
    if doc.page > 1:
        canvas.setStrokeColor(colors.HexColor('#D9E5ED'))
        canvas.line(50, A4[1]-38, A4[0]-50, A4[1]-38)
        canvas.setFillColor(GREY); canvas.setFont('Body', 7.5)
        canvas.drawString(50, A4[1]-28, 'HELPDESK API / РУКОВОДСТВО РАЗРАБОТЧИКА')
        canvas.drawRightString(A4[0]-50, 28, str(doc.page))
        canvas.drawString(50, 28, 'C# / .NET 10 / ASP.NET Core / EF Core / SQL Server / xUnit')
    canvas.restoreState()


def expanded_source():
    text = (ROOT / 'docs/development-guide.md').read_text(encoding='utf-8')
    def include(match):
        file = ROOT / 'docs' / match[1]
        content = file.read_text(encoding='utf-8').splitlines()
        return '\n'.join(content[1:]) # Drop included document's top title.
    return re.sub(r'<!-- include:([\w.-]+) -->', include, text)


def markdown_flowables(text):
    result, paragraph = [], []
    lines = text.splitlines()
    def flush():
        if paragraph:
            value = ' '.join(paragraph)
            style = styles['LabelRu'] if value.endswith(':') else styles['TextRu']
            result.append(Paragraph(inline(value), style))
            paragraph.clear()
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if not line:
            flush(); i += 1; continue
        if line.startswith('```'):
            flush(); code = []; i += 1
            while i < len(lines) and not lines[i].startswith('```'):
                code.extend(textwrap.wrap(lines[i], 92, replace_whitespace=False,
                                          drop_whitespace=False) or [''])
                i += 1
            block = [Preformatted('\n'.join(code), styles['CodeRu'])]
            if result and isinstance(result[-1], Paragraph) and result[-1].style.name == 'LabelRu':
                block.insert(0, result.pop())
            result.append(KeepTogether(block))
        elif line.startswith('# '):
            flush(); result.extend([CondPageBreak(320), Spacer(1, 16)])
            result.append(Paragraph(inline(line[2:]), styles['ChapterRu']))
        elif line.startswith('## '):
            flush(); result.append(Paragraph(inline(line[3:]), styles['SubRu']))
        elif line in ['<!-- architecture -->', '<!-- schema -->', '<!-- workflow -->']:
            flush(); result.append(Diagram(line.split()[1])); result.append(Spacer(1, 12))
        elif line.startswith('|'):
            flush(); rows = []
            while i < len(lines) and lines[i].strip().startswith('|'):
                cells = [c.strip() for c in lines[i].strip().strip('|').split('|')]
                if not all(re.fullmatch(r':?-+:?', c) for c in cells):
                    rows.append([Paragraph(inline(c), styles['CellRu']) for c in cells])
                i += 1
            cols = len(rows[0]); widths = ([120, WIDTH-120] if cols == 2 else [WIDTH/cols]*cols)
            table = Table(rows, colWidths=widths, repeatRows=1, hAlign='LEFT')
            table.setStyle(TableStyle([
                ('BACKGROUND', (0,0), (-1,0), PALE),
                ('VALIGN', (0,0), (-1,-1), 'TOP'),
                ('LINEBELOW', (0,0), (-1,0), .8, TEAL),
                ('LINEBELOW', (0,1), (-1,-1), .3, colors.HexColor('#DCE5EA')),
                ('LEFTPADDING',(0,0),(-1,-1),7), ('RIGHTPADDING',(0,0),(-1,-1),7),
                ('TOPPADDING',(0,0),(-1,-1),7), ('BOTTOMPADDING',(0,0),(-1,-1),7),
            ]))
            result.extend([table, Spacer(1, 12)]); continue
        elif line.startswith('- ') or re.match(r'^\d+\. ', line):
            flush(); value = ('- ' + line[2:]) if line.startswith('- ') else line
            result.append(Paragraph(inline(value), styles['BulletRu']))
        else:
            paragraph.append(line)
        i += 1
    flush()
    return result


def main():
    OUT.parent.mkdir(parents=True, exist_ok=True)
    doc = GuideDoc(str(OUT), pagesize=A4, rightMargin=50, leftMargin=50,
                   topMargin=55, bottomMargin=50, title='HelpDesk API - руководство разработчика',
                   author='HelpDesk Portfolio Project', pageCompression=1)
    cover = ParagraphStyle('Cover', fontName='BodyBold', fontSize=38, leading=44, textColor=NAVY, spaceAfter=20)
    subtitle = ParagraphStyle('CoverSub', fontName='Body', fontSize=18, leading=26, textColor=TEAL, spaceAfter=20)
    story = [Spacer(1, 95), Paragraph('HelpDesk API', cover),
             Paragraph('Учимся программировать<br/>на готовом проекте', subtitle),
             Spacer(1, 20), Paragraph('Учебник с нуля: от переменной до API', styles['SubRu']),
             Paragraph('Простые объяснения, первые запросы, чтение C# по шагам, упражнения с ответами и выбор инструментов.', styles['TextRu']),
             Spacer(1, 26), Diagram('workflow'), Spacer(1, 30),
             Paragraph('C# / .NET 10 / ASP.NET Core Web API<br/>EF Core / Microsoft SQL Server / xUnit', styles['TextRu']),
             Spacer(1, 20), Paragraph('Портфолио-проект · Русская версия · 10 ноября 2026', styles['TextRu']),
             PageBreak(), Paragraph('Содержание', styles['ChapterRu'])]
    toc = TableOfContents(); toc.levelStyles = [styles['TocRu']]
    # The unnumbered contents heading is excluded from notifications.
    story[-1].style = ParagraphStyle('ContentsTitle', parent=styles['ChapterRu'])
    story.append(toc)
    story.extend(markdown_flowables(expanded_source()))
    doc.multiBuild(story, onFirstPage=frame, onLaterPages=frame)
    print(OUT)


if __name__ == '__main__':
    main()
