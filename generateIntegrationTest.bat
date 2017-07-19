
del /f /s /q generated 1>nul
rmdir /s /q generated

dotnet Cogs.Console\bin\Debug\netcoreapp2.0\Cogs.Console.dll publish-cs  "cogsburger" "generated"
dotnet Cogs.Console\bin\Debug\netcoreapp2.0\Cogs.Console.dll publish-json  "cogsburger" "generated"
