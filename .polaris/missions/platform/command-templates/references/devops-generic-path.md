## Path B: Generic DevOps Pipeline

### Quick Mode

If `--quick` or "quick" in arguments: Skip discovery. Auto-detect language/framework. Use defaults: GitHub Actions CI/CD, Kubernetes deployment, all environments (dev/staging/prod). Generate Dockerfile, workflows, Helm charts, and .env.example immediately.

### Step 1. Discovery

Detect project context:
- **Language/framework**: Scan for package.json, pyproject.toml, go.mod, Cargo.toml
- **Existing CI/CD**: Check .github/workflows/, .gitlab-ci.yml, azure-pipelines.yml
- **Existing Docker**: Check Dockerfile, docker-compose.yml
- **Test framework**: Detect test runner

Ask the user:
- **CI/CD platform**: GitHub Actions (default), Azure DevOps, GitLab CI
- **Deployment target**: Container registry, Kubernetes, cloud service, or none (CI-only)
- **Environments**: dev, staging, production
- **Container registry**: ghcr.io, Docker Hub, ACR, ECR

### Step 2. Generate CI/CD Pipeline

**GitHub Actions** (.github/workflows/): `ci.yml` (lint+test+build on PR), `release.yml` (container build+push on tag), `deploy.yml` (K8s deployment)

**Azure DevOps** (azure-pipelines.yml): Build pipeline with stages: lint, test, build, deploy

**GitLab CI** (.gitlab-ci.yml): Pipeline with stages: lint, test, build, deploy

Each pipeline: language-appropriate build, test execution with coverage, container image build/push, environment-specific deployment gates.

### Step 3. Generate Dockerfile

If no Dockerfile exists, create one with:
- Multi-stage build (separate build and runtime stages)
- Non-root user for security
- HEALTHCHECK instruction
- Standard OCI labels
- Language-specific optimizations: Python=slim base+requirements caching, Node=alpine+package.json caching, Go=static binary+scratch/distroless, C#=SDK build+aspnet runtime

### Step 4. Generate Docker Compose

If deploying with docker-compose, generate standard service config with build, ports, env_file, and healthcheck.

### Step 5. Generate Helm Charts (if Kubernetes)

Create complete Helm chart structure under `helm/`:

```
helm/
  Chart.yaml              # apiVersion v2, app metadata
  values.yaml             # Base: replicaCount=1, image config, service ClusterIP:80->8080,
                          # resources (100m-500m CPU, 128Mi-512Mi mem), liveness/readiness probes
  values-staging.yaml     # replicaCount=2, ingress enabled, staging domain
  values-production.yaml  # replicaCount=3, ingress+TLS, autoscaling 3-10 replicas, 500m-1000m CPU
  templates/
    _helpers.tpl, deployment.yaml, service.yaml, ingress.yaml, hpa.yaml,
    serviceaccount.yaml, configmap.yaml, secret.yaml
```

All templates reference values via `{{ .Values.* }}`. Security: `runAsNonRoot: true, runAsUser: 1000`.

### Step 5b. Generate CD Pipeline

Deployment pipeline for the selected CI/CD platform using Helm:
- **GitHub Actions**: `deploy.yml` with staging (on release) and production (manual dispatch)
- **Azure DevOps**: `azure-pipelines-deploy.yml` with parameterized environment and version
- **GitLab CI**: Deploy stages using `alpine/helm:3.13.0` image

Each pipeline: `helm upgrade --install` with namespace per environment, values files overlay, image tag override, `--wait --timeout 5m`, then `kubectl rollout status`.

### Step 6. Environment Configuration

Create `.env.example` with all required variables (no real values).

### Step 7. Validation

1. Lint generated workflow files
2. Build Docker image: `docker build -t <project>:dev .`
3. Run container with health check
4. Verify CI config syntax

### Step 8. Summary

List all generated files, then next steps: review configs, set up repository secrets, update helm values, push to trigger CI, run `/polaris.healthcheck`.

### Principles

- Detect before generating (scan existing setup first)
- Never store secrets (only .env.example with placeholders)
- Multi-stage Docker builds
- Health checks everywhere
- Ask before overwriting existing configs
