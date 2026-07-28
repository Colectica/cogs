Continuous Integration
======================

Since a COGS model is just plain text, many people can collaborate on the 
same model and synchronize their work using version control software like
subversion or git. Outputs can automatically be built whenever the model
changes using a continuous integration system. This allows for a 
transparent development process and fast iterations.

This repository currently uses GitHub Actions for its own build automation, but
the same validate-and-publish workflow can be implemented in any CI system.

Scenario
----------------

Whenever a change is made to the model, the model should be validated and
all outputs should automatically be generated and uploaded to a 
staging site where they can be reviewed.

Repository conformance matrix
-----------------------------

COGS itself checks the same conformance corpus on Windows and Linux with .NET
10, Python 3.11, Node 22, the pinned tools in
``conformance/tools.json``, and every publisher target. The workflow builds all
three generated language packages, runs both cross-language runtime orders,
validates every JSON/XML boundary, exercises projection tools, compares a
second generation, and checks pinned downstream migration diagnostics. The
regeneration gate compares non-Turtle artifacts byte-for-byte and parses each
Turtle pair with dotNetRDF to require strict RDF graph isomorphism. The comparer
checks ground triples exactly and matches blank-node-connected components with
``Graph.Equals`` so large collections of independent restrictions remain
practical to compare. Blank-node labels, prefix aliases, statement order, and
formatting may differ without changing the standards-compliant semantic graph.

Generated TypeScript installation is platform-specific under npm 10. POSIX
uses a package ``--prefix`` for install and build. Windows runs
``npm install --ignore-scripts --no-package-lock`` inside the generated package,
then uses a prefixed build and ``npm pack <package-path> --dry-run``. CI hashes
``package.json`` before and after installation and fails if npm rewrites it.
See :doc:`/technical-guide/command-line/publish-ts` for exact commands.

Equivalent baseline executions have passed on Windows and an isolated Debian
12 source copy. Debian is not Ubuntu Noble, and a local baseline is not a
hosted GitHub Actions result. Release evidence must continue to identify the
actual operating system and runner instead of inferring it from the configured
matrix.


GitLab and AppVeyor Example
---------------------------

AppVeyor is a service that provides free continuous integration for open
source projects. For an example of how AppVeyor can be configured to perform
continuous integration for a COGS model, see the following files from the 
Structured Data Transform Language (SDTL) model.

AppVeyor Configuration and Initialization
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

The following two files provide the job configuration and dependency installation.

* https://gitlab.com/c2metadata/sdtl-cogs/blob/master/appveyor.yml
* https://gitlab.com/c2metadata/sdtl-cogs/blob/master/build/appveyor-install-dependencies.ps1

Build Script
~~~~~~~~~~~~

The following batch file executes all publishers and builds the Sphinx documentation for the model.

* https://gitlab.com/c2metadata/sdtl-cogs/blob/master/build/build-windows.bat

Deployment to Staging
~~~~~~~~~~~~~~~~~~~~~

The following PowerShell script deploys the generated documentation and artifacts to GitLab pages,
where it is immediately available on the Web.

* https://gitlab.com/c2metadata/sdtl-cogs/blob/master/build/deploy-gitlab-pages.ps1

