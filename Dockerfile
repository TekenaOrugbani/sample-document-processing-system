# Stage 1: Build
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS builder

RUN dnf update -y && \
    dnf install -y dotnet-sdk-10.0 findutils && \
    dnf clean all

WORKDIR /build

COPY src/ src/
COPY DocumentProcessor.sln .

RUN dotnet restore DocumentProcessor.sln

RUN dotnet publish src/DocumentProcessor.Web/DocumentProcessor.Web.csproj \
    -c Release \
    -o /publish \
    --no-restore

# Stage 2: Runtime
FROM public.ecr.aws/amazonlinux/amazonlinux:2023 AS runtime

RUN dnf update -y && \
    dnf install -y aspnetcore-runtime-10.0 findutils shadow-utils && \
    dnf clean all

RUN groupadd -r appuser && useradd -r -g appuser appuser

WORKDIR /app

COPY --chown=appuser:appuser --from=builder /publish .

RUN mkdir -p /app/uploads && chown -R appuser:appuser /app/uploads

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "DocumentProcessor.Web.dll"]
