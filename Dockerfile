FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

WORKDIR /App

# Copy everything
COPY . ./

# Restore as distinct layers
RUN dotnet restore ./Cogs.Console.sln
# Build and publish a release
RUN dotnet build ./Cogs.Console.sln -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /App
COPY --from=build-env /App/out .

ENTRYPOINT ["dotnet", "cogs.dll"]
