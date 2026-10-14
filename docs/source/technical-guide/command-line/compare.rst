Model Comparison
================

``compare`` produces a definition-focused changelog as Markdown and a
publication-ready PDF. Both reports describe the same changes; the PDF uses
Typst, while Markdown-only comparison has no reporting-tool dependency.

Directory and Git inputs
------------------------

.. code-block:: powershell

   cogs compare D:\models\before D:\models\after D:\reports\changes
   cogs compare a1b2c3d d4e5f6a D:\reports\changes --git D:\models\current
   cogs compare before after reports --format markdown

The first input is the older model and the second is the newer model. With
``--git``, the first two arguments identify commits in the model's enclosing
repository. Both commits use the same repository-relative model directory.
Full or unambiguous abbreviated hashes and commit-resolving refs are accepted.
Uncommitted files are ignored. Git is discovered through ``COGS_GIT``, then
``PATH``; no checkout, fetch, index update, or repository hook is performed.
Missing shallow-clone history must be fetched separately by the user.
Published Git sources use the repository-relative model directory and resolved
full commit hash, not a local checkout path. A repository-root model uses the
repository directory name, for example ``ddimodel @ 573711d7075561ba27fd5aa825c3db32745d70fb``.
Directory-input reports retain their directory source labels.

Inputs are read into disposable snapshots. A legacy input without
``CogsVersion`` receives the existing mechanical COGS 2 upgrade in its snapshot
only. Semantic migration decisions, unsupported explicit format versions, and
validation errors stop comparison. Preparation metadata remains internal and
does not appear in published reports; neither original model is rewritten.
All inputs still receive complete normal
model validation, including documentation conventions.

The output directory must be disjoint from both original models. An existing
target requires ``--overwrite``. Publication is transactional: failure never
replaces an existing report with a partial bundle. Differences themselves are
successful results with exit code zero; usage and input/publication failures
use the usual CLI exit codes.

Coverage
--------

The report includes settings and extra settings, identification and mixins,
all item/composite definitions (including unused types), inheritance,
abstractness, primitive annotations, property datatypes, cardinalities,
ordering, subtype permission, descriptions, and facets. Property declaration
order is compared because it determines XML sequencing. Enumeration membership
and ordering changes are reported separately.

Topics, articles, additional documentation, generated headers, and the three
historical property columns are excluded. Type ``readme.markdown`` content is
included as the type's description. Parsed defaults, CSV quoting, flag casing,
and CRLF/LF differences do not create changes; meaningful description
whitespace is retained. Abstract-property subtype defaults follow the same
effective rules as other publishers.

Identification and inherited changes appear once at their declarations with
affected items/descendants listed. Added and removed types include their
complete property definitions. This is a factual change report, not an automatic compatibility
verdict, migration script, or model-version recommendation.

Report organization and counts
------------------------------

After model versions, sources, and the summary, a linked table of contents
lists sections, change groups, and individual types (not properties). The PDF
also supplies page references and bookmarks. Item types come first, followed
by composite types; each section lists Added, Removed, then Modified types in
ordinal name order. Type-level changes precede nested property additions,
removals, and modifications. Possible rename suggestions follow, when present.
Settings, including identification changes, are last. Empty sections are omitted.

The summary has ItemTypes, CompositeTypes, and Properties rows with Added,
Removed, and Modified counts. An existing owner counts as Modified once when
its own definition or any locally declared property changes. Descendants are
not counted just because they inherit a change. Added/removed owners stay in
those categories exclusively. Properties count once, including identification
fields and properties belonging to added/removed owners. Confirmed renames,
moves, and relative declaration-order changes count as Modified while their
specific change labels remain visible. Inserting a property does not mark
surviving properties as reordered.

A property move affects both existing owners, but counts as one modified
property. Its full change appears under the destination, with a cross-reference
under the source. Identical models produce an explicit ``No definition changes``
report with zero counts. Setup and snapshot-preparation prose is never included.

Confirming renames and moves
----------------------------

Model names do not provide persistent identities. Unmatched unique structural
matches are therefore suggestions only: they remain additions/removals until
confirmed. Ambiguous matches are not guessed. Review suggestions, then provide
an explicit JSON file through ``--renames``:

.. code-block:: json

   {
     "renames": [
       {"from": "ItemTypes/OldCommand", "to": "ItemTypes/Command"},
       {"from": "ItemTypes/OldCommand/OldLabel", "to": "ItemTypes/Command/Label"}
     ]
   }

Paths are exact-case logical model paths, not filesystem paths. Supported
forms are ``ItemTypes/Type``, ``CompositeTypes/Type``, either type path followed
by ``/Property``, and ``Identification/Property``. ``from`` always names an
older declaration and ``to`` a newer declaration. Endpoints must exist, be
one-to-one, and have the same entity kind. Types cannot change between item and
composite kinds through a rename mapping. A property mapping to a different
owner records a move and may also record a rename and field changes.

Confirmed mappings are applied before exact-name matching. References to a
confirmed renamed datatype or parent are accounted for without repeating the
rename as unrelated datatype/parent modifications.

Report files and Typst
----------------------

``--format both`` is the default and emits
``<new-slug>.changelog.md``, ``<new-slug>.changelog.typ``, and
``<new-slug>.changelog.pdf``. ``--format markdown`` emits only Markdown;
``--format pdf`` emits the PDF and editable, self-contained Typst source.
All filenames use the newer model's exact slug.

Install `Typst <https://github.com/typst/typst/releases>`_ 0.15.1 or later for
PDF output. Discovery uses ``--typst``, then ``COGS_TYPST``, then ``typst`` on
``PATH``. COGS never installs it automatically. A missing, failing, or outdated
compiler fails the requested PDF publication; use ``--format markdown`` when
only Markdown is needed. Compilation is limited to five minutes.
Compiler warnings are retained as publication warnings. For writing systems
not covered by the embedded fonts, provide additional fonts through Typst's
``TYPST_FONT_PATHS`` environment variable and review the rendered PDF.

The A4 report uses embedded compiler fonts, labeled additions/removals,
word-level description highlighting (line-level for multiline text), page
numbers, bookmarks, and PDF accessibility tags. Model text is data, never
executable Typst. Empty fields are grouped explicitly rather than omitted.
Markdown retains complete before/after text and displays text-diff markers
inside ordinary code blocks without requiring a rendering extension.

You can customize the retained source and recompile, for example to change
paper size or typography:

.. code-block:: console

   typst compile lifecycle40.changelog.typ lifecycle40.changelog.pdf

No network imports or external template assets are required. Markdown and
Typst sources are repeatable for the same inputs and provenance. PDF
verification compares extracted content and layout rather than file bytes;
compiler metadata and versions can change the PDF encoding.
