# Docker Build and Run Instructions

## Overview

This is a .NET 10 ASP.NET Core Blazor Server application for document upload, text extraction (PDF/TXT/LOG), and AI summarization using Amazon Bedrock (Claude Sonnet 5). It requires a PostgreSQL database at startup.

## Prerequisites

- Docker installed and running
- A reachable PostgreSQL instance (e.g., Amazon RDS for PostgreSQL or Aurora PostgreSQL)
- AWS credentials with access to Amazon Bedrock (Claude Sonnet 5) in your chosen region
- (Optional) AWS Secrets Manager secret for database credentials

## Build

From the root of the extracted source directory:

```bash
docker build -t document-processor-web .
```

## Run

### Minimum required environment variables

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Bedrock__Region` | AWS region for Bedrock API (e.g., `us-east-1`) |

### Optional environment variables

| Variable | Default | Description |
|---|---|---|
| `Database__UseSecretsManager` | `false` | When `true`, fetches DB credentials from AWS Secrets Manager |
| `Database__SecretDescriptionPrefix` | `Password for Aurora PostgreSQL used for MAM417.` | Prefix of the Secrets Manager secret description |
| `Bedrock__SummarizationModelId` | `global.anthropic.claude-sonnet-5` | Bedrock model ID for summarization |
| `Bedrock__MaxTokens` | `2000` | Maximum tokens for Bedrock inference response |
| `Bedrock__MaxInputCharacters` | `10000` | Maximum characters of extracted text sent to the model |
| `Bedrock__MaxPdfPages` | `5` | Maximum number of PDF pages to extract text from |
| `Storage__RootPath` | `uploads` | Root directory for uploaded files |
| `Storage__MaxFileSizeMegabytes` | `50` | Maximum file size in MB |
| `Storage__MaxFilesPerUpload` | `10` | Maximum files per upload batch |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment name |
| `AWS_ACCESS_KEY_ID` | — | AWS access key (prefer IAM role in ECS/EKS) |
| `AWS_SECRET_ACCESS_KEY` | — | AWS secret access key (prefer IAM role in ECS/EKS) |
| `AWS_DEFAULT_REGION` | — | Default AWS region for SDK calls |

### Example run command

```bash
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=your-db-host;Port=5432;Database=DPS;Username=postgres;Password=your-password" \
  -e Bedrock__Region="us-east-1" \
  -v document-uploads:/app/uploads \
  --name document-processor-web \
  document-processor-web
```

The application will be available at: http://localhost:8080

### Using AWS Secrets Manager for database credentials

```bash
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=your-db-host;Port=5432;Database=DPS;Username=postgres;Password=fallback-password" \
  -e Database__UseSecretsManager="true" \
  -e Database__SecretDescriptionPrefix="Password for Aurora PostgreSQL used for MAM417." \
  -e Bedrock__Region="us-east-1" \
  -e AWS_DEFAULT_REGION="us-east-1" \
  -v document-uploads:/app/uploads \
  --name document-processor-web \
  document-processor-web
```

## Persistent Storage

The application stores uploaded files under `/app/uploads` inside the container. Mount a persistent volume to preserve uploads across container restarts:

```bash
-v document-uploads:/app/uploads
```

## Security Notes

- **Do not hardcode credentials** in environment variables for production. Use:
  - [ECS Secrets Manager integration](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html) for ECS deployments
  - [EKS Secrets Manager / KMS encryption](https://docs.aws.amazon.com/eks/latest/userguide/security-k8s.html) for EKS deployments
- Prefer IAM roles (ECS task role / EKS service account) over `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` for AWS SDK authentication.
- The application has no built-in authentication — add an authentication layer before exposing it publicly.

## Database Initialization

On first startup, the application automatically creates the `DPS` database schema via EF Core `EnsureCreatedAsync()`. Ensure the database user has `CREATE TABLE` privileges on the target database.

## Exposed Ports

| Port | Protocol | Description |
|---|---|---|
| `8080` | HTTP | ASP.NET Core Blazor Server web interface |
