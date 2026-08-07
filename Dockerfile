# Render で BirdHotel.Web を動かすための Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BirdHotel.Web/BirdHotel.Web.csproj BirdHotel.Web/
RUN dotnet restore BirdHotel.Web/BirdHotel.Web.csproj

COPY BirdHotel.Web/ BirdHotel.Web/
RUN dotnet publish BirdHotel.Web/BirdHotel.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

# Render は待ち受けポートを環境変数 PORT で渡してくるため、起動時に読み取る
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet BirdHotel.Web.dll"]
