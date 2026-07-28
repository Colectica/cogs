@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "COGS=%ROOT%Cogs.Console\bin\Debug\net10.0\cogs.dll"
set "GENERATED=%ROOT%generated"

if not exist "%COGS%" (
    echo COGS Debug CLI not found: "%COGS%" 1>&2
    echo Run dotnet build Cogs.Console.sln --configuration Debug first. 1>&2
    exit /b 1
)

rem GENERATED is a fixed child of the repository containing this script.
if /I "%GENERATED%"=="%ROOT%" (
    echo Refusing to remove the repository root. 1>&2
    exit /b 1
)
if exist "%GENERATED%" rd /s /q "%GENERATED%"
if exist "%GENERATED%" (
    echo Could not remove generated output: "%GENERATED%" 1>&2
    exit /b 1
)

pushd "%ROOT%" || exit /b 1

call :cogs validate "cogsburger" || goto :fail
call :cogs publish-xsd --overwrite "cogsburger" "generated\xsd" || goto :fail
call :cogs publish-cs --overwrite --csproj --nullable "cogsburger" "generated\src" || goto :fail
call :cogs publish-py --overwrite "cogsburger" "generated\python" || goto :fail
call :cogs publish-ts --overwrite "cogsburger" "generated\typescript" || goto :fail
call :cogs publish-json --overwrite "cogsburger" "generated\json" || goto :fail
call :cogs publish-owl --overwrite "cogsburger" "generated\owl" || goto :fail
call :cogs publish-linkml --overwrite "cogsburger" "generated\linkml" || goto :fail
call :cogs publish-dctap --overwrite "cogsburger" "generated\dctap" || goto :fail
call :cogs publish-graphql --overwrite "cogsburger" "generated\graphql" || goto :fail
call :cogs publish-uml --overwrite --mode normative "cogsburger" "generated\uml-normative" || goto :fail
call :cogs publish-uml --overwrite --mode ea "cogsburger" "generated\uml-ea" || goto :fail
call :cogs publish-dot --overwrite --format dot --all --inheritance --composite "cogsburger" "generated\dot" || goto :fail
call :cogs publish-sphinx --overwrite "cogsburger" "generated\sphinx" || goto :fail

call :npm_install || goto :fail
if defined COGS_NPM (
    call "%COGS_NPM%" --prefix "generated\typescript" run build || goto :fail
    call "%COGS_NPM%" pack "%GENERATED%\typescript" --dry-run || goto :fail
) else (
    call npm --prefix "generated\typescript" run build || goto :fail
    call npm pack "%GENERATED%\typescript" --dry-run || goto :fail
)

if defined COGS_PYTHON (
    "%COGS_PYTHON%" -m compileall -q "generated\python" || goto :fail
) else (
    where python3 >nul 2>&1 && (python3 -m compileall -q "generated\python" || goto :fail) || (
        where python >nul 2>&1 && (python -m compileall -q "generated\python" || goto :fail) || (
            where py >nul 2>&1 && (py -3 -m compileall -q "generated\python" || goto :fail) || (
                echo No Python interpreter found. Set COGS_PYTHON. 1>&2
                goto :fail
            )
        )
    )
)

dotnet restore "generated\src\CogsBurger.Model.csproj" --verbosity minimal || goto :fail

popd
exit /b 0

:cogs
dotnet "%COGS%" %*
exit /b %ERRORLEVEL%

:npm_install
pushd "generated\typescript" || exit /b 1
if defined COGS_NPM (
    call "%COGS_NPM%" install --ignore-scripts --no-package-lock
) else (
    call npm install --ignore-scripts --no-package-lock
)
set "NPM_RESULT=%ERRORLEVEL%"
popd
exit /b %NPM_RESULT%

:fail
set "RESULT=%ERRORLEVEL%"
if "%RESULT%"=="0" set "RESULT=1"
popd
exit /b %RESULT%
