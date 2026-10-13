# Docker Build & Run Instructions

## Overview

This is a .NET 10 ASP.NET Core Blazor Server application that processes uploaded documents
(PDF, TXT, LOG), extracts text, and summarizes them using Amazon Bedrock (Claude Sonnet 5).
It stores results in a PostgreSQL database via Entity Framework Core 10.

---

## Prerequisites

- Docker installed and running
- A PostgreSQL instance (local or remote)
- An AWS account with Amazon Bedrock access to Claude Sonnet 5 in your chosen region
- AWS credentials (IAM role recommended for ECS; access keys for local testing)

---

## Build

The build context must be the **root of the extracted source directory** (where `DocumentProcessor.sln` lives).

```bash
# From the root of the extracted source archive:
docker build -t document-processor-web .
```

---

## Run

### Minimum required environment variables

| Variable | Description | Example |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | `Host=mydb.example.com;Port=5432;Database=DPS;Username=appuser;Password=secret` |
| `Bedrock__Region` | AWS region for Bedrock API | `us-east-1` |

### Optional environment variables

| Variable | Description | Default |
|---|---|---|
| `Bedrock__SummarizationModelId` | Bedrock model ID | `global.anthropic.claude-sonnet-5` |
| `Bedrock__MaxTokens` | Max tokens for Bedrock responses | `2000` |
| `Bedrock__MaxInputCharacters` | Max characters of text sent to model | `10000` |
| `Bedrock__MaxPdfPages` | Max PDF pages to extract | `5` |
| `Storage__RootPath` | Root path for uploaded files | `uploads` |
| `Storage__MaxFileSizeMegabytes` | Max upload file size in MB | `50` |
| `Storage__MaxFilesPerUpload` | Max files per upload batch | `10` |
| `Database__UseSecretsManager` | Use AWS Secrets Manager for DB credentials | `false` |
| `Database__SecretDescriptionPrefix` | Prefix to match Secrets Manager secret | `Password for Aurora PostgreSQL used for MAM417.` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Production` |
| `AWS_ACCESS_KEY_ID` | AWS access key (prefer IAM roles) | — |
| `AWS_SECRET_ACCESS_KEY` | AWS secret key (prefer IAM roles) | — |
| `AWS_DEFAULT_REGION` | Default AWS region for SDK | — |

### Example run command

```bash
docker run -d \
  --name document-processor-web \
  -p 8080:8080 \
  -e "ConnectionStrings__DefaultConnection=Host=mydb.example.com;Port=5432;Database=DPS;Username=appuser;Password=secret" \
  -e "Bedrock__Region=us-east-1" \
  -e "AWS_ACCESS_KEY_ID=<your-access-key>" \
  -e "AWS_SECRET_ACCESS_KEY=<your-secret-key>" \
  -v /data/uploads:/app/uploads \
  document-processor-web
```

The application will be available at: `http://localhost:8080`

---

## Persistent Storage

Uploaded documents are stored under `/app/uploads` inside the container, partitioned by date (`yyyy/MM/dd/`). Mount a persistent volume to this path to preserve uploads across container restarts:

```bash
-v /your/host/path:/app/uploads
```

---

## AWS Secrets Manager (optional)

To use AWS Secrets Manager for database credentials instead of a connection string:

1. Create a secret in Secrets Manager with the following JSON structure:
   ```json
   {
     "username": "your_username",
     "password": "your_password",
     "host": "your-db-host.rds.amazonaws.com",
     "port": "5432",
     "dbname": "your_database_name"
   }
   ```
2. Set the secret's **description** to start with the value of `Database__SecretDescriptionPrefix` (default: `Password for Aurora PostgreSQL used for MAM417.`).
3. Set `Database__UseSecretsManager=true` in the container environment.
4. Ensure the container's IAM role has `secretsmanager:ListSecrets` and `secretsmanager:GetSecretValue` permissions.

---

## Secret Handling Recommendations

- **ECS**: Use [ECS Secrets Manager integration](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html) to inject secrets as environment variables via the task definition.
- **EKS**: Use [AWS Secrets Manager with EKS](https://docs.aws.amazon.com/eks/latest/userguide/security-k8s.html) via the Secrets Store CSI Driver or KMS encryption.
- **Never** hardcode credentials in the Dockerfile or image layers.

---

## Health Check

The application exposes HTTP on port `8080`. A basic health check can be performed against the root path:

```bash
curl -f http://localhost:8080/
```

---

## Database Schema

The application calls `EnsureCreatedAsync()` on startup, which automatically creates the `Documents` table in the configured PostgreSQL database if it does not exist. No manual migration step is required for initial deployment.
