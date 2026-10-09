# Docker Build and Run Instructions

## Prerequisites

- Docker installed and running
- A PostgreSQL instance (Aurora-compatible) accessible from the container
- AWS credentials with permissions for Amazon Bedrock and (optionally) AWS Secrets Manager

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

The application will be available at: http://localhost:8080

## Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✅ Yes | `Host=localhost;Port=5432;Database=DPS;Username=postgres;Password=LocalDev!Passw0rd` | PostgreSQL connection string |
| `Bedrock__Region` | ✅ Yes | `us-east-1` | AWS region for Amazon Bedrock API calls |
| `Bedrock__SummarizationModelId` | No | `global.anthropic.claude-sonnet-5` | Bedrock model ID for document summarization |
| `Bedrock__MaxTokens` | No | `2000` | Maximum tokens for Bedrock model responses |
| `Bedrock__MaxInputCharacters` | No | `10000` | Maximum characters of extracted text sent to the model |
| `Bedrock__MaxPdfPages` | No | `5` | Maximum PDF pages to extract text from |
| `Database__UseSecretsManager` | No | `false` | Set to `true` to retrieve DB credentials from AWS Secrets Manager |
| `Database__SecretDescriptionPrefix` | No | `Password for Aurora PostgreSQL used for MAM417.` | Prefix to locate the correct secret in Secrets Manager |
| `Storage__RootPath` | No | `uploads` | Filesystem path where uploaded documents are stored |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | ASP.NET Core environment name |
| `ASPNETCORE_URLS` | No | `http://+:8080` | URLs the application listens on |
| `AWS_ACCESS_KEY_ID` | No | — | AWS access key (prefer IAM roles in ECS/EKS) |
| `AWS_SECRET_ACCESS_KEY` | No | — | AWS secret access key (prefer IAM roles in ECS/EKS) |
| `AWS_DEFAULT_REGION` | No | — | Default AWS region for SDK clients |

## Persistent Storage

The application stores uploaded documents at `/app/uploads`. Mount a volume to persist files across container restarts:

```bash
-v document-uploads:/app/uploads
```

## AWS Credentials

For production deployments on ECS or EKS, use IAM roles instead of environment variables:

- **ECS**: Attach an IAM task role with permissions for `bedrock:InvokeModel` and (if needed) `secretsmanager:GetSecretValue` and `secretsmanager:ListSecrets`.
- **EKS**: Use [IRSA (IAM Roles for Service Accounts)](https://docs.aws.amazon.com/eks/latest/userguide/iam-roles-for-service-accounts.html).

For secret management:
- **ECS**: [Secrets Manager integration](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/secrets-envvar-secrets-manager.html)
- **EKS**: [Secrets Manager or KMS encryption](https://docs.aws.amazon.com/eks/latest/userguide/security-k8s.html)

## Database

The application requires a PostgreSQL database (Aurora PostgreSQL compatible). On first startup, it automatically creates the `Documents` table using `EnsureCreatedAsync()`.

To use AWS Secrets Manager for database credentials, set:

```bash
-e Database__UseSecretsManager=true \
-e Database__SecretDescriptionPrefix="Password for Aurora PostgreSQL used for MAM417."
```

The secret must contain a JSON object with fields: `host`, `port`, `dbname`, `username`, `password`.

## Health Check

The application exposes an HTTP endpoint at `/` on port `8080`. Use this for ALB target group health checks.

## Exposed Ports

| Port | Protocol | Description |
|---|---|---|
| `8080` | HTTP | ASP.NET Core Blazor Server web application |
