# Docker Build and Run Instructions

## Overview

This is a .NET 10 Blazor Server application that processes uploaded documents (PDF, TXT, LOG),
extracts text, and summarizes them using Amazon Bedrock (Claude Sonnet 5). It stores results
in a PostgreSQL database.

## Prerequisites

- Docker installed and running
- A PostgreSQL instance (local or remote)
- AWS credentials with access to Amazon Bedrock (Claude Sonnet 5) in your chosen region

## Building the Image

From the root of the extracted source directory:

```bash
docker build -t document-processor-web .
```

## Running the Container

### Minimum required environment variables

```bash
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=<db-host>;Port=5432;Database=DPS;Username=<user>;Password=<password>" \
  -e Bedrock__Region="us-east-1" \
  -v /path/to/uploads:/app/uploads \
  document-processor-web
```

### Full configuration example

```bash
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=<db-host>;Port=5432;Database=DPS;Username=<user>;Password=<password>" \
  -e Bedrock__Region="us-east-1" \
  -e Bedrock__SummarizationModelId="global.anthropic.claude-sonnet-5" \
  -e Bedrock__MaxTokens="2000" \
  -e Storage__RootPath="uploads" \
  -e ASPNETCORE_ENVIRONMENT="Production" \
  -e AWS_ACCESS_KEY_ID="<your-access-key>" \
  -e AWS_SECRET_ACCESS_KEY="<your-secret-key>" \
  -e AWS_DEFAULT_REGION="us-east-1" \
  -v /path/to/uploads:/app/uploads \
  document-processor-web
```

### Using AWS Secrets Manager for database credentials

```bash
docker run -d \
  -p 8080:8080 \
  -e Database__UseSecretsManager="true" \
  -e Database__SecretDescriptionPrefix="Password for Aurora PostgreSQL used for MAM417." \
  -e Bedrock__Region="us-east-1" \
  -v /path/to/uploads:/app/uploads \
  document-processor-web
```

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✅ Yes | PostgreSQL connection string (e.g. `Host=<host>;Port=5432;Database=DPS;Username=<user>;Password=<password>`) |
| `Bedrock__Region` | ✅ Yes | AWS region for Amazon Bedrock (e.g. `us-east-1`) |
| `Database__UseSecretsManager` | No | Set to `true` to retrieve DB credentials from AWS Secrets Manager |
| `Database__SecretDescriptionPrefix` | No | Prefix of the Secrets Manager secret description to look up |
| `Bedrock__SummarizationModelId` | No | Bedrock model ID (default: `global.anthropic.claude-sonnet-5`) |
| `Bedrock__MaxTokens` | No | Max tokens for summarization response (default: `2000`) |
| `Storage__RootPath` | No | Root directory for uploaded files (default: `uploads`) |
| `ASPNETCORE_ENVIRONMENT` | No | Runtime environment (default: `Production`) |
| `ASPNETCORE_URLS` | No | Listening URLs (default: `http://+:8080`) |
| `AWS_ACCESS_KEY_ID` | No | AWS access key (prefer IAM role in ECS/EKS) |
| `AWS_SECRET_ACCESS_KEY` | No | AWS secret access key (prefer IAM role in ECS/EKS) |
| `AWS_DEFAULT_REGION` | No | Default AWS region for SDK calls |

## Persistent Storage

The application stores uploaded files under `/app/uploads` (date-partitioned: `uploads/yyyy/MM/dd/`).
Mount a persistent volume to this path to preserve uploads across container restarts:

```bash
-v /path/to/uploads:/app/uploads
```

## Ports

| Port | Protocol | Description |
|---|---|---|
| `8080` | HTTP | ASP.NET Core Blazor Server web interface |

## Accessing the Application

Once running, open your browser at:

```
http://localhost:8080
```

## Security Notes

- **Never hardcode credentials** in the connection string for production. Use:
  - [ECS Secrets Manager integration](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html) for ECS deployments
  - [EKS Secrets Manager/KMS](https://docs.aws.amazon.com/eks/latest/userguide/security-k8s.html) for EKS deployments
  - The built-in `Database__UseSecretsManager=true` option to retrieve credentials from AWS Secrets Manager at startup
- The container runs as a non-root `appuser` for security
- AWS credentials should be provided via an IAM role (ECS task role / EKS service account) rather than environment variables in production

## Database Initialization

The application calls `EnsureCreatedAsync()` on startup, which automatically creates the `DPS`
database and `Documents` table if they do not exist. No manual migration step is required for
initial deployment.

## AWS IAM Permissions Required

The ECS task role (or equivalent) must have the following permissions:

- `bedrock:InvokeModel` on the Claude Sonnet 5 model in the configured region
- `secretsmanager:ListSecrets` and `secretsmanager:GetSecretValue` (only if `Database__UseSecretsManager=true`)
