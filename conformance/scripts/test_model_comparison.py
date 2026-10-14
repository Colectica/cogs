"""Real PDF/text conformance; deliberately no PDF byte-equality requirement."""
import argparse
import csv
import hashlib
import json
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

from pypdf import PdfReader


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cogs", type=Path, required=True)
    parser.add_argument("--typst", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    cogs, typst, output = args.cogs.resolve(), args.typst.resolve(), args.output.resolve()

    def run(*arguments):
        result = subprocess.run(["dotnet", str(cogs), *map(str, arguments)], text=True,
                                encoding="utf-8", capture_output=True, timeout=300)
        if result.returncode:
            raise AssertionError(result.stdout + result.stderr)

    def hashes(root):
        return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in root.rglob("*") if p.is_file()}

    with tempfile.TemporaryDirectory(prefix="cogs comparison ") as temporary:
        before = Path(temporary) / "before"
        after = Path(temporary) / "after"
        run("cogs-new", before)
        shutil.copytree(before, after)
        property_file = after / "ItemTypes/ItemTwo/ItemTwo.csv"
        with property_file.open(encoding="utf-8-sig", newline="") as source:
            reader = csv.DictReader(source)
            headers, rows = reader.fieldnames, list(reader)
        rows[0]["Description"] = 'Comparison sentinel: café, "quotes", #read("/not-code").\nA changed description.'
        # An extended report verifies pagination and that the final entry is not truncated.
        for i in range(30):
            row = dict.fromkeys(headers, "")
            row.update(Name=f"AddedField{i:02}", DataType="string", MinCardinality="0", MaxCardinality="1",
                       Description=f"Published addition {i:02}. " + "Readable explanatory text. " * 8)
            rows.append(row)
        with property_file.open("w", encoding="utf-8", newline="") as destination:
            writer = csv.DictWriter(destination, fieldnames=headers)
            writer.writeheader()
            writer.writerows(rows)
        source_hashes = hashes(before), hashes(after)
        arguments = ("compare", before, after, output, "--typst", typst, "--overwrite")
        run(*arguments)
        markdown = next(output.glob("*.changelog.md"))
        typst_source = next(output.glob("*.changelog.typ"))
        pdf = next(output.glob("*.changelog.pdf"))
        first_text = markdown.read_bytes(), typst_source.read_bytes()
        first_pdf = PdfReader(pdf)
        extracted = "\n".join(p.extract_text() for p in first_pdf.pages).replace("\u200b", "")
        assert len(first_pdf.pages) > 1
        for required in ("Model changelog", "AddedField29", "Published addition 29", "Comparison sentinel", "café"):
            assert required in extracted, f"PDF is missing {required!r}"
            assert required in markdown.read_text(encoding="utf-8"), f"Markdown is missing {required!r}"
        for i in range(30):
            assert f"AddedField{i:02}" in extracted
            assert f"Published addition {i:02}" in extracted
        assert first_pdf.trailer["/Root"].get("/StructTreeRoot"), "PDF must retain accessibility tags"
        md = markdown.read_text(encoding="utf-8")
        assert "| ItemTypes | 0 | 0 | 1 |" in md
        assert "| CompositeTypes | 0 | 0 | 0 |" in md
        assert "| Properties | 30 | 0 | 1 |" in md
        anchors = re.findall(r'<a id="([^"]+)"></a>', md)
        assert len(anchors) == len(set(anchors))
        links = re.findall(r'\]\(#([^)]+)\)', md)
        assert links and set(links) <= set(anchors)
        contents = md.split("## Table of contents\n", 1)[1].split('<a id="', 1)[0]
        assert "ItemTwo" in contents and "AddedField" not in contents
        assert "Table of contents" in extracted and "preparation:" not in extracted

        def destinations(entries):
            for entry in entries:
                if isinstance(entry, list):
                    yield from destinations(entry)
                else:
                    yield entry

        outline = list(destinations(first_pdf.outline))
        assert [entry.title for entry in outline] == ["Item types", "Modified", "ItemTwo"]
        for entry in outline:
            page = first_pdf.get_destination_page_number(entry)
            assert page is not None and 0 <= page < len(first_pdf.pages)
            assert entry.title in first_pdf.pages[page].extract_text()
        toc_pages = [page for page in first_pdf.pages if "Table of contents" in page.extract_text()]
        assert toc_pages and any(page.get("/Annots") for page in toc_pages), "Contents must contain navigation links"
        toc_text = "\n".join(page.extract_text() for page in toc_pages)
        assert str(first_pdf.get_destination_page_number(outline[-1]) + 1) in toc_text
        run(*arguments)
        assert first_text == (markdown.read_bytes(), typst_source.read_bytes())
        second_pdf = PdfReader(pdf)
        assert len(first_pdf.pages) == len(second_pdf.pages)
        assert extracted == "\n".join(p.extract_text() for p in second_pdf.pages).replace("\u200b", "")
        assert source_hashes == (hashes(before), hashes(after))
        (output / "verification.json").write_text(json.dumps({"pages": len(second_pdf.pages),
            "textAndSourceRepeatable": True, "sourceUnchanged": True}, indent=2) + "\n", encoding="utf-8")
        print(f"Model comparison PDF/text corpus passed ({len(second_pdf.pages)} pages).")


if __name__ == "__main__":
    main()
