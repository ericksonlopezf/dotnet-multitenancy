# Multi-stage build for EricksonLopez.MultiTenancy.Showcase
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and project files
COPY EricksonLopez.MultiTenancy.slnx ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./

COPY src/ ./src/
COPY samples/ ./samples/

# Restore and build the showcase project
WORKDIR /app/samples/EricksonLopez.MultiTenancy.Showcase
RUN dotnet restore -f net9.0
RUN dotnet publish -c Release -f net9.0 -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EricksonLopez.MultiTenancy.Showcase.dll", "--server"]
