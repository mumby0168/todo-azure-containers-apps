FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ./ ./
RUN dotnet restore "src/ToBeDone.App/Server/ToBeDone.App.Server.csproj"
WORKDIR "src/ToBeDone.App/Server"
RUN dotnet build "ToBeDone.App.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ToBeDone.App.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ToBeDone.App.Server.dll"]
