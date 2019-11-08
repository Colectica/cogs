
rd /s /q "generated"

RMDIR /S /Q "generated"

RD /S /Q "generated"


dotnet Cogs.Console\bin\Debug\netcoreapp2.1\cogs.dll publish-xsd  "cogsburger" "generated"
dotnet Cogs.Console\bin\Debug\netcoreapp2.1\cogs.dll publish-cs  "cogsburger" "generated"
dotnet Cogs.Console\bin\Debug\netcoreapp2.1\cogs.dll publish-json  "cogsburger" "generated"

@pause