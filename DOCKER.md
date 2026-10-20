# Docker Build & Run Instructions

## Overview

`DocumentProcessor.Web` is a .NET 10 Blazor Server application that processes uploaded
documents (PDF, TXT, LOG), extracts text, and generates AI summaries using Amazon Bedrock
(Claude Sonnet 5). It stores results in a PostgreSQL database.

---

## Prerequisites

- Docker installed and running
- A PostgreSQL instance accessible from the container
- AWS credentials with permissions for:
  - `bedrock:InvokeModel` (Amazon Bedrock — Claude Sonnet 5)
  - `secretsmanager:ListSecrets` / `secretsmanager:GetSecretValue` (optional, if using Secrets Manager)

---

## Build

Run from the **root of the extracted source directory** (where `DocumentProcessor.sln` lives):

```bash
docker build -t document-processor-web .
```

---

## Run

### Minimum required environment variables

| Variable | Description | Example |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | `Host=mydb.example.com;Port=5432;Database=DPS;Username=appuser;Password=secret` |
| `Bedrock__Region` | AWS region for Amazon Bedrock | `us-east-1` |
| `Bedrock__SummarizationModelId` | Bedrock model ID | `global.anthropic.claude-sonnet-5` |

### Optional environment variables

| Variable | Description | Default |
|---|---|---|
| `Database__UseSecretsManager` | Fetch DB credentials from AWS Secrets Manager | `false` |
| `Database__SecretDescriptionPrefix` | Secrets Manager secret description prefix | `Password for Aurora PostgreSQL used for MAM417.` |
| `Bedrock__MaxTokens` | Max tokens for Bedrock inference responses | `2000` |
| `Bedrock__MaxInputCharacters` | Max characters of extracted text sent to the model | `10000` |
| `Bedrock__MaxPdfPages` | Max PDF pages to extract text from | `5` |
| `Storage__RootPath` | Directory path for uploaded files | `uploads` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Production` |

### Example run command

```bash
docker run -d \
  --name document-processor-web \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=mydb.example.com;Port=5432;Database=DPS;Username=appuser;Password=secret" \
  -e Bedrock__Region="us-east-1" \
  -e Bedrock__SummarizationModelId="global.anthropic.claude-sonnet-5" \
  -v document-uploads:/app/uploads \
  document-processor-web
```

The application will be available at: **http://localhost:8080**

---

## Persistent Storage

Uploaded documents are stored under `/app/uploads` inside the container. Mount a volume
to persist files across container restarts:

```bash
-v document-uploads:/app/uploads
```

---

## AWS Credentials

In production (ECS/EKS), attach an IAM role to the task/pod with the required permissions.
For local testing, pass credentials via environment variables:

```bash
-e AWS_ACCESS_KEY_ID="..." \
-e AWS_SECRET_ACCESS_KEY="..." \
-e AWS_DEFAULT_REGION="us-east-1"
```

---

## Secrets Management

For production deployments, avoid passing secrets as plain environment variables.
Use the following instead:

- **ECS**: [Secrets Manager integration for ECS](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html)
- **EKS**: [Secrets Manager or KMS encryption for EKS](https://docs.aws.amazon.com/eks/latest/userguide/security-k8s.html)

Enable the built-in Secrets Manager support by setting:

```bash
-e Database__UseSecretsManager="true" \
-e Database__SecretDescriptionPrefix="Password for Aurora PostgreSQL used for MAM417."
```

The secret must be a JSON object with fields: `host`, `port`, `dbname`, `username`, `password`.

---

## Database Schema

The application calls `EnsureCreatedAsync()` on startup, which automatically creates the
`DPS` database and `Documents` table if they do not exist. No manual migration is required
for a fresh deployment.

---

## Health Check

The application exposes HTTP on port `8080`. Use `/` as the health check path.
Expected HTTP response code: `200–499`.

---

## Notes

- HTTPS redirection is enabled in the application code. In a containerised deployment
  behind an ALB or reverse proxy, terminate TLS at the load balancer and forward HTTP
  to port `8080`.
- The application has no built-in authentication. Add an authentication layer before
  exposing it publicly.
- Scanned PDFs with no text layer will yield little for the model to summarise.
