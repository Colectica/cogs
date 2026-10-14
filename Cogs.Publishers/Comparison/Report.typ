// Self-contained comparison report. Model text is passed only as string data.
#set document(title: "Model changelog")
#set page(paper: "a4", margin: (x: 22mm, y: 20mm), numbering: "1")
#set text(font: "Libertinus Serif", size: 10.5pt, fill: rgb("243247"))
#set par(justify: false, leading: 0.65em)
#set heading(numbering: none)
#show heading.where(level: 1): set text(size: 22pt)
#show heading.where(level: 2): set text(size: 16pt, fill: rgb("365573"))
#show heading.where(level: 3): set text(size: 13pt, fill: rgb("365573"))
#show heading.where(level: 4): set text(size: 12pt)
#show heading.where(level: 5): set text(size: 11pt, fill: rgb("365573"))
#show heading.where(level: 6): set text(size: 10.5pt)
#let summary(rows) = table(
  columns: (1fr, auto, auto, auto), stroke: none, inset: (x: 8pt, y: 6pt),
  fill: (_, row) => if row == 0 { rgb("e8eef4") } else if calc.rem(row, 2) == 1 { rgb("f5f7f9") },
  align: (column, _) => if column == 0 { left } else { right },
  table.header([*Definition*], [*Added*], [*Removed*], [*Modified*]),
  ..rows.flatten().map(cell => text(str(cell))))
#let prose(value) = {
  for (index, line) in value.split("\n").enumerate() {
    if index > 0 { linebreak() }
    text(line)
  }
}
#let value(label, content) = block(above: 2pt, below: 2pt,
  table(columns: (29%, 71%), stroke: none, inset: (x: 5pt, y: 3pt),
    fill: (column, row) => if column == 0 { rgb("f0f3f6") },
    text(size: 9.5pt, weight: "semibold", label), prose(content)))
#let removed(content) = text(fill: rgb("932d35"), strike(prose(content)))
#let added(content) = text(fill: rgb("186644"), underline(prose(content)))
