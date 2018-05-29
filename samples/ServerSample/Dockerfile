FROM microsoft/aspnetcore:2.0 AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/aspnetcore-build:2.0 AS build
WORKDIR /src
COPY samples/ServerSample/ServerSample.csproj samples/ServerSample/
RUN dotnet restore samples/ServerSample/ServerSample.csproj
COPY . .
WORKDIR /src/samples/ServerSample
RUN dotnet build ServerSample.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish ServerSample.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "ServerSample.dll"]
