FROM microsoft/dotnet

COPY . /app

WORKDIR /app

RUN dotnet restore ./Cogs.Console.sln

RUN dotnet build ./Cogs.Console.sln -c Release -o out

ENTRYPOINT ["dotnet", "/app/Cogs.Console/out/Cogs.Console.dll"]