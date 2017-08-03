
rd /s /q "generated"

RMDIR /S /Q "generated"

RD /S /Q "generated"


dotnet Cogs.Console\bin\Debug\netcoreapp2.0\Cogs.Console.dll publish-xsd  "cogsburger" "generated"
dotnet Cogs.Console\bin\Debug\netcoreapp2.0\Cogs.Console.dll publish-cs  "cogsburger" "generated"
dotnet Cogs.Console\bin\Debug\netcoreapp2.0\Cogs.Console.dll publish-json  "cogsburger" "generated"

@pause