FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/ToBeDone.Services.Work/ToBeDone.Services.Work.csproj", "src/ToBeDone.Services.Work/"]
RUN dotnet restore "src/ToBeDone.Services.Work/ToBeDone.Services.Work.csproj"
COPY . .
WORKDIR "/src/src/ToBeDone.Services.Work"
RUN dotnet build "ToBeDone.Services.Work.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ToBeDone.Services.Work.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ToBeDone.Services.Work.dll"]
