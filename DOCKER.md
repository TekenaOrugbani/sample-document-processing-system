# Docker Instructions — Document Processor Web

## Overview

A .NET 10 Blazor Server application that processes uploaded documents (PDF, TXT, LOG),
extracts text, and summarizes content using Amazon Bedrock (Claude Sonnet 5).
Results are stored in a PostgreSQL database.

## Prerequisites

- Docker
- A running PostgreSQL instance (version 13+)
- An AWS account with Amazon Bedrock access to Claude Sonnet 5 in your chosen region
- AWS credentials (IAM role recommended for ECS; access keys for local testing)

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
  -e ConnectionStrings__DefaultConnection="Host=<db-host>;Port=5432;Database=DPS;Username=<db-user>;Password=<db-password>" \
  -e Bedrock__Region="us-east-1" \
  -e AWS_ACCESS_KEY_ID="<your-access-key>" \
  -e AWS_SECRET_ACCESS_KEY="<your-secret-key>" \
  -v document-uploads:/app/uploads \
  document-processor-web
```

Replace the placeholder values with your actual configuration.

## Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✅ | `Host=localhost;Port=5432;Database=DPS;Username=postgres;Password=LocalDev!Passw0rd` | PostgreSQL connection string |
| `Bedrock__Region` | ✅ | `us-east-1` | AWS region for Amazon Bedrock |
| `Database__UseSecretsManager` | No | `false` | Set to `true` to retrieve DB credentials from AWS Secrets Manager |
| `Database__SecretDescriptionPrefix` | No | `Password for Aurora PostgreSQL used for MAM417.` | Prefix of the Secrets Manager secret description |
| `Bedrock__SummarizationModelId` | No | `global.anthropic.claude-sonnet-5` | Bedrock model ID for summarization |
| `Bedrock__MaxTokens` | No | `2000` | Maximum tokens for Bedrock inference |
| `Storage__RootPath` | No | `uploads` | Root directory for uploaded files |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | ASP.NET Core environment name |
| `ASPNETCORE_URLS` | No | `http://+:8080` | Listener URLs |
| `AWS_ACCESS_KEY_ID` | No | — | AWS access key (prefer IAM roles in ECS) |
| `AWS_SECRET_ACCESS_KEY` | No | — | AWS secret access key (prefer IAM roles in ECS) |
| `AWS_DEFAULT_REGION` | No | — | Default AWS region for SDK clients |

## Persistent Storage

Uploaded documents are stored under `/app/uploads` inside the container, organized by date
(`yyyy/MM/dd/filename`). Mount a persistent volume to preserve uploads across container restarts:

```bash
-v document-uploads:/app/uploads
```

In ECS, use an EFS volume mount for `/app/uploads`.

## AWS Secrets Manager (Optional)

To retrieve PostgreSQL credentials from AWS Secrets Manager instead of the connection string:

1. Set `Database__UseSecretsManager=true`
2. Ensure the secret's description starts with the value of `Database__SecretDescriptionPrefix`
3. The secret must contain the following JSON fields:
   ```json
   {
     "username": "your_username",
     "password": "your_password",
     "host": "your-db-host.rds.amazonaws.com",
     "port": "5432",
     "dbname": "your_database_name"
   }
   ```
4. The ECS task role (or EC2 instance role) must have `secretsmanager:ListSecrets` and
   `secretsmanager:GetSecretValue` permissions.

For ECS secret injection, see:
https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html

For EKS secret handling, see:
https://docs.aws.amazon.com/eks/latest/userguide/security-k8s.html

## Security Notes

- **Never** use the default development password (`LocalDev!Passw0rd`) in production.
- Use AWS Secrets Manager or ECS task definition secrets for all credentials.
- The container runs as a non-root `appuser` for security.
- There is no built-in authentication — add an authentication layer before exposing the app publicly.

## Health Check

The application exposes HTTP on port `8080`. Use `/` as the health check path.
The app requires a reachable PostgreSQL instance to start successfully — it calls
`EnsureCreatedAsync()` at startup to create the schema if it does not exist.

## Ports

| Port | Protocol | Description |
|---|---|---|
| `8080` | HTTP | Blazor Server web application |
