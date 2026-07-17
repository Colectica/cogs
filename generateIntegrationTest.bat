
rd /s /q "generated"

RMDIR /S /Q "generated"

RD /S /Q "generated"

dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll validate "cogsburger"
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-xsd --overwrite "cogsburger" "generated\xsd"
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-cs --overwrite --csproj --nullable "cogsburger" "generated\src"
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-py --overwrite "cogsburger" "generated\python"
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-ts --overwrite "cogsburger" "generated\typescript"
if defined COGS_NPM (
    call "%COGS_NPM%" --prefix "generated\typescript" install --ignore-scripts
    if errorlevel 1 exit /b 1
    call "%COGS_NPM%" --prefix "generated\typescript" run build
    if errorlevel 1 exit /b 1
) else (
    call npm --prefix "generated\typescript" install --ignore-scripts
    if errorlevel 1 exit /b 1
    call npm --prefix "generated\typescript" run build
    if errorlevel 1 exit /b 1
)
dotnet restore "generated\src\CogsBurger.Model.csproj"
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-json --overwrite "cogsburger" "generated\json"
dotnet Cogs.Console\bin\Debug\net10.0\cogs.dll publish-owl --overwrite "cogsburger" "generated\owl"
