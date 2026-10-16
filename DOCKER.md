# Docker Build & Run Instructions

## Overview

This is a .NET 10 Blazor Server application for document processing with AI summarization
via Amazon Bedrock (Claude Sonnet 5), storing results in PostgreSQL.

## Prerequisites

- Docker installed and running
- A PostgreSQL instance accessible from the container
- AWS credentials with access to Amazon Bedrock (Claude Sonnet 5) and optionally Secrets Manager

## Build

From the root of the extracted source directory:

```bash
docker build -t document-processor-web .
```

## Run

```bash
docker run -d \
  --name document-processor-web \
  -p 8080:8080 \
  -v uploads-data:/app/uploads \
  -e ConnectionStrings__DefaultConnection="Host=<db-host>;Port=5432;Database=DPS;Username=<db-user>;Password=<db-password>" \
  -e Bedrock__Region="us-east-1" \
  -e AWS_ACCESS_KEY_ID="<your-access-key>" \
  -e AWS_SECRET_ACCESS_KEY="<your-secret-key>" \
  -e AWS_DEFAULT_REGION="us-east-1" \
  document-processor-web
```

The application will be available at: http://localhost:8080

## Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✅ Yes | — | Full PostgreSQL connection string (e.g. `Host=myhost;Port=5432;Database=DPS;Username=postgres;Password=secret`) |
| `Bedrock__Region` | ✅ Yes | `us-east-1` | AWS region for Amazon Bedrock |
| `Bedrock__SummarizationModelId` | No | `global.anthropic.claude-sonnet-5` | Bedrock model ID for document summarization |
| `Bedrock__MaxTokens` | No | `2000` | Maximum tokens for Bedrock inference responses |
| `Bedrock__MaxInputCharacters` | No | `10000` | Maximum characters of extracted text sent to the model |
| `Bedrock__MaxPdfPages` | No | `5` | Maximum PDF pages to extract text from |
| `Storage__RootPath` | No | `uploads` | Root directory for storing uploaded documents |
| `Storage__MaxFileSizeMegabytes` | No | `50` | Maximum file size in MB |
| `Storage__MaxFilesPerUpload` | No | `10` | Maximum files per upload batch |
| `Database__UseSecretsManager` | No | `false` | When `true`, fetches DB credentials from AWS Secrets Manager |
| `Database__SecretDescriptionPrefix` | No | `Password for Aurora PostgreSQL used for MAM417.` | Prefix to match the Secrets Manager secret description |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | ASP.NET Core environment name |
| `ASPNETCORE_URLS` | No | `http://+:8080` | URLs the application listens on |
| `AWS_ACCESS_KEY_ID` | No | — | AWS access key (use IAM role in ECS/EKS instead) |
| `AWS_SECRET_ACCESS_KEY` | No | — | AWS secret access key (use IAM role in ECS/EKS instead) |
| `AWS_DEFAULT_REGION` | No | — | Default AWS region for SDK clients |

## Persistent Storage

The application stores uploaded documents on disk under `/app/uploads`. Mount a persistent
volume to this path to preserve uploads across container restarts:

```bash
-v uploads-data:/app/uploads
```

## AWS Secrets Manager (Optional)

To use AWS Secrets Manager for database credentials instead of a connection string:

1. Create a secret in Secrets Manager with a JSON value:
   ```json
   {
     "username": "your_username",
     "password": "your_password",
     "host": "your-db-host.rds.amazonaws.com",
     "port": "5432",
     "dbname": "DPS"
   }
   ```
2. Set the secret's **description** to start with `Password for Aurora PostgreSQL used for MAM417.`
3. Set `Database__UseSecretsManager=true` in the container environment

## Secret Handling in Production

**⚠️ Do not pass secrets as plain environment variables in production.**

- **ECS**: Use [AWS Secrets Manager integration for ECS](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html) to inject secrets into the task definition.
- **EKS**: Use [AWS Secrets Manager with EKS](https://docs.aws.amazon.com/eks/latest/userguide/security-k8s.html) via the Secrets Store CSI Driver or KMS encryption.
- Assign an IAM role to the ECS task / EKS pod with permissions for `bedrock:InvokeModel` and (if using Secrets Manager) `secretsmanager:GetSecretValue`.

## Health Check

The application exposes HTTP on port `8080`. Use `/` as the health check path.

## Exposed Ports

| Port | Protocol | Description |
|---|---|---|
| `8080` | HTTP | ASP.NET Core Blazor Server web interface |
