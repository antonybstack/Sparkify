FROM mcr.microsoft.com/dotnet/sdk:8.0-preview AS build
WORKDIR /source

RUN apt-get update \
    && apt-get install -y curl jq 

# copy csproj and restore as distinct layers
COPY Sparkify.Proxy/*.csproj .
RUN dotnet restore --use-current-runtime

# copy everything else and build app
COPY ./Sparkify.Proxy .
RUN dotnet publish --use-current-runtime --no-restore -c Release -o /app --no-cache /restore

# Generate the dev certificate
RUN dotnet dev-certs https -ep /https/sparkify.proxy.pfx -p password
RUN dotnet dev-certs https --trust
RUN update-ca-certificates

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-preview
WORKDIR /app
COPY --from=build /app .
COPY --from=build /https/sparkify.proxy.pfx .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS="https://+;http://+"
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/app/sparkify.proxy.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=password
ENV Kestrel__Endpoints__HTTP__Url=http://0.0.0.0:80
ENV Kestrel__Endpoints__HTTP__Protocols=Http1AndHttp2AndHttp3
ENV Kestrel__Endpoints__HTTPS__Url=https://0.0.0.0:443
ENV Kestrel__Endpoints__HTTPS__Protocols=Http1AndHttp2AndHttp3
# ENV Kestrel__Endpoints__HTTPS__SslProtocols='["Tls13"]'

RUN apt update; apt install curl -y; \
    curl https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb --output packages-microsoft-prod.deb; \
    dpkg -i packages-microsoft-prod.deb; \
    rm packages-microsoft-prod.deb

RUN apt update; apt install libmsquic -y; apt install curl -y; apt clean -y;

RUN apt-get update \
    && apt-get install -y curl jq 

EXPOSE 443/udp
EXPOSE 443/tcp
EXPOSE 80/udp
EXPOSE 80/tcp

ENTRYPOINT ["dotnet", "Sparkify.Proxy.dll"]