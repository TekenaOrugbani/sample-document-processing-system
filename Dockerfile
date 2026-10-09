# Stage 1: Build
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS builder

RUN dnf update -y && \
    dnf install -y dotnet-sdk-10.0 && \
    dnf clean all

WORKDIR /build

COPY src/DocumentProcessor.Web/DocumentProcessor.Web.csproj src/DocumentProcessor.Web/

RUN dotnet restore src/DocumentProcessor.Web/DocumentProcessor.Web.csproj

COPY src/ src/

RUN dotnet publish src/DocumentProcessor.Web/DocumentProcessor.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime
FROM public.ecr.aws/amazonlinux/amazonlinux:2023

RUN dnf update -y && \
    dnf install -y aspnetcore-runtime-10.0 shadow-utils && \
    dnf clean all

RUN groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /app

COPY --chown=appuser:appuser --from=builder /app/publish .

RUN mkdir -p /app/uploads && chown -R appuser:appuser /app/uploads

USER appuser

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DocumentProcessor.Web.dll"]
