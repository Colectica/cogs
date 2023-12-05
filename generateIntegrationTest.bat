
rd /s /q "generated"

RMDIR /S /Q "generated"

RD /S /Q "generated"

dotnet Cogs.Console\bin\Debug\net6.0\cogs.dll validate "cogsburger"
dotnet Cogs.Console\bin\Debug\net6.0\cogs.dll publish-xsd  "cogsburger" "generated"
dotnet Cogs.Console\bin\Debug\net6.0\cogs.dll publish-cs  "cogsburger" "generated" --csproj --nullable
dotnet Cogs.Console\bin\Debug\net6.0\cogs.dll publish-json  "cogsburger" "generated"
dotnet Cogs.Console\bin\Debug\net6.0\cogs.dll publish-owl  "cogsburger" "generated"

@pause