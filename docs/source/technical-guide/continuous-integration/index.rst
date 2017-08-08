Continuous Integration
======================

Since a COGS model is just plain text, many people can collaborate on the 
same model and synchronize their work using version control software like
subversion or git. Outputs can automatically be built whenever the model
changes using a continuous integration system. This allows for a 
transparent development process and fast iterations.

Scenario
----------------

Whenever a change is made to the model, the model should be validated and
all outputs should automatically be generated and uploaded to a 
staging site where they can be reviewed.


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

