# ─── Stage 1: Build ───────────────────────────────────────────────────────────
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS builder

RUN dnf update -y && \
    dnf install -y dotnet-sdk-10.0 && \
    dnf clean all

RUN dnf install -y shadow-utils && \
    groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /build

# Copy solution and project files first for layer caching
COPY --chown=appuser:appuser DocumentProcessor.sln ./
COPY --chown=appuser:appuser src/DocumentProcessor.Web/DocumentProcessor.Web.csproj ./src/DocumentProcessor.Web/

# Restore dependencies
RUN dotnet restore src/DocumentProcessor.Web/DocumentProcessor.Web.csproj

# Copy the rest of the source code
COPY --chown=appuser:appuser src/ ./src/

# Publish the application
RUN dotnet publish src/DocumentProcessor.Web/DocumentProcessor.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS runtime

RUN dnf update -y && \
    dnf install -y aspnetcore-runtime-10.0 && \
    dnf clean all

RUN dnf install -y shadow-utils && \
    groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /app

# Copy published output from builder stage
COPY --chown=appuser:appuser --from=builder /app/publish ./

# Create uploads directory for persistent document storage
RUN mkdir -p /app/uploads && chown -R appuser:appuser /app/uploads

# Expose HTTP port
EXPOSE 8080

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DocumentProcessor.Web.dll"]
