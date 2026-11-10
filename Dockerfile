FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/HelpDesk.Api/HelpDesk.Api.csproj src/HelpDesk.Api/packages.lock.json src/HelpDesk.Api/
RUN dotnet restore src/HelpDesk.Api/HelpDesk.Api.csproj --locked-mode
COPY src/HelpDesk.Api/ src/HelpDesk.Api/
RUN dotnet publish src/HelpDesk.Api/HelpDesk.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER root
RUN mkdir -p /app/keys && chown app:app /app/keys
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HelpDesk.Api.dll"]
