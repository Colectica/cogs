FROM microsoft/dotnet

COPY . /app

WORKDIR /app

RUN dotnet restore ./Cogs.Console.sln

RUN dotnet build ./Cogs.Console.sln

ENTRYPOINT ["dotnet", "/app/Cogs.Console/bin/Debug/netcoreapp2.0/Cogs.Console.dll"]