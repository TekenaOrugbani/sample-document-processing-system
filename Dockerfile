# ─── Stage 1: Build ───────────────────────────────────────────────────────────
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS builder

ARG http_proxy
ARG https_proxy

RUN dnf update -y && \
    dnf install -y tar gzip libicu shadow-utils findutils && \
    dnf clean all

# Install .NET 10 SDK via Microsoft install script
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && \
    chmod +x /tmp/dotnet-install.sh && \
    /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/local/dotnet && \
    rm /tmp/dotnet-install.sh

ENV PATH="/usr/local/dotnet:$PATH"
ENV DOTNET_ROOT="/usr/local/dotnet"

WORKDIR /build

# Copy solution and project files first for layer caching
COPY DocumentProcessor.sln ./
COPY src/DocumentProcessor.Web/DocumentProcessor.Web.csproj ./src/DocumentProcessor.Web/

# Restore NuGet packages
RUN dotnet restore src/DocumentProcessor.Web/DocumentProcessor.Web.csproj

# Copy the rest of the source code
COPY --chown=root:root src/ ./src/

# Publish the application
RUN dotnet publish src/DocumentProcessor.Web/DocumentProcessor.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM public.ecr.aws/amazonlinux/amazonlinux:2023

ARG http_proxy
ARG https_proxy

RUN dnf update -y && \
    dnf install -y tar gzip libicu openssl-libs zlib shadow-utils findutils && \
    dnf clean all

# Install .NET 10 ASP.NET Core Runtime
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && \
    chmod +x /tmp/dotnet-install.sh && \
    /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/local/dotnet && \
    rm /tmp/dotnet-install.sh

ENV PATH="/usr/local/dotnet:$PATH"
ENV DOTNET_ROOT="/usr/local/dotnet"

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /app

# Copy published output from builder stage
COPY --chown=appuser:appuser --from=builder /app/publish ./

# Create uploads directory and set ownership
RUN mkdir -p /app/uploads && chown -R appuser:appuser /app/uploads

USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"
ENV DOTNET_RUNNING_IN_CONTAINER="true"

ENTRYPOINT ["dotnet", "DocumentProcessor.Web.dll"]
