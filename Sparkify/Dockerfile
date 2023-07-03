FROM mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY Sparkify/*.csproj .
RUN dotnet restore

# copy everything else and build app
COPY ./Sparkify .
RUN dotnet publish --use-current-runtime --no-restore -c Release -o /app --no-cache /restore

# final stage/image
FROM mcr.microsoft.com/dotnet/nightly/aspnet:8.0-preview
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS="http://+"
ENV Kestrel__Endpoints__HTTP__Url=http://0.0.0.0:8080

RUN apt update; apt install curl -y; \
    curl https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb --output packages-microsoft-prod.deb; \
    dpkg -i packages-microsoft-prod.deb; \
    rm packages-microsoft-prod.deb

RUN apt update; apt install libmsquic -y; apt install curl -y; apt clean -y;


EXPOSE 8080/udp
EXPOSE 8080/tcp
EXPOSE 6001/udp
EXPOSE 6001/tcp

ENTRYPOINT ["dotnet", "Sparkify.dll"]