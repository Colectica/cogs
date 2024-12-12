
rd /s /q "generated"

RMDIR /S /Q "generated"

RD /S /Q "generated"

dotnet Cogs.Console\bin\Debug\net9.0\cogs.dll validate "cogsburger"
dotnet Cogs.Console\bin\Debug\net9.0\cogs.dll publish-xsd --overwrite "cogsburger" "generated\xsd"
dotnet Cogs.Console\bin\Debug\net9.0\cogs.dll publish-cs --overwrite --csproj --nullable "cogsburger" "generated\src"
dotnet Cogs.Console\bin\Debug\net9.0\cogs.dll publish-json --overwrite "cogsburger" "generated\json"
dotnet Cogs.Console\bin\Debug\net9.0\cogs.dll publish-owl --overwrite "cogsburger" "generated\owl"

@pause