# Stage 1: Build
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS builder

RUN dnf update -y && \
    dnf install -y dotnet-sdk-10.0 && \
    dnf clean all

WORKDIR /build

# Copy solution and project files first for layer caching
COPY DocumentProcessor.sln ./
COPY src/DocumentProcessor.Web/DocumentProcessor.Web.csproj ./src/DocumentProcessor.Web/

# Restore dependencies
RUN dotnet restore src/DocumentProcessor.Web/DocumentProcessor.Web.csproj

# Copy the rest of the source code
COPY src/ ./src/

# Publish the application
RUN dotnet publish src/DocumentProcessor.Web/DocumentProcessor.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS runtime

RUN dnf update -y && \
    dnf install -y aspnetcore-runtime-10.0 shadow-utils && \
    dnf clean all

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /app

# Copy published output
COPY --chown=appuser:appuser --from=builder /app/publish ./

# Create uploads directory with correct ownership
RUN mkdir -p /app/uploads && chown -R appuser:appuser /app/uploads

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "DocumentProcessor.Web.dll"]
