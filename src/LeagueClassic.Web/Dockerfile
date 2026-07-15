# Build context is the repo root (needed so this can COPY the whole
# src/LeagueClassic.Web tree by relative path). Build with:
#   docker build -f src/LeagueClassic.Web/Dockerfile -t leagueclassic-web .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/LeagueClassic.Web/LeagueClassic.Web.csproj src/LeagueClassic.Web/
RUN dotnet restore src/LeagueClassic.Web/LeagueClassic.Web.csproj

COPY src/LeagueClassic.Web/ src/LeagueClassic.Web/
RUN dotnet publish src/LeagueClassic.Web/LeagueClassic.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LeagueClassic.Web.dll"]
