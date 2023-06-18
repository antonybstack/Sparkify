FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Sparkify.Proxy/Sparkify.Proxy.csproj", "Sparkify.Proxy/"]
RUN dotnet restore "Sparkify.Proxy/Sparkify.Proxy.csproj"
COPY . .
WORKDIR "/src/Sparkify.Proxy"
RUN dotnet build "Sparkify.Proxy.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Sparkify.Proxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sparkify.Proxy.dll"]
