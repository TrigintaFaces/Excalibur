# Kubernetes Deployment

**Framework:** Excalibur
**Deployment Target:** Kubernetes clusters
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Kubernetes cluster (local via minikube/kind or cloud-hosted)
- Docker images built per the [Docker deployment guide](./docker.md)
- Familiarity with [health checks](../observability/health-checks.md) and [leader election](../leader-election/index.md)

## Overview

Deploy Excalibur applications to Kubernetes for production-grade orchestration, scaling, and high availability.

**Use Kubernetes when:**
- Running in production with high availability requirements
- Autoscaling based on load
- Managing multiple microservices
- Deploying to AKS, EKS, GKE, or on-premises clusters

---

## Quick Start

### Minimal Deployment

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: your-app
spec:
  replicas: 3
  selector:
    matchLabels:
      app: your-app
  template:
    metadata:
      labels:
        app: your-app
    spec:
      containers:
      - name: app
        image: yourregistry.azurecr.io/your-app:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
---
apiVersion: v1
kind: Service
metadata:
  name: your-app
spec:
  selector:
    app: your-app
  ports:
  - port: 80
    targetPort: 8080
  type: LoadBalancer
```

**Deploy:**

```bash
kubectl apply -f deployment.yaml
kubectl get pods
kubectl get svc
```

---

## Production-Ready Manifests

### Complete Deployment with ConfigMaps and Secrets

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: your-app-prod
---
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: app-config
  namespace: your-app-prod
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  ASPNETCORE_URLS: "http://+:8080"
  Dispatch__Outbox__ProcessorInterval: "00:00:30"
  Dispatch__Outbox__BatchSize: "100"
---
# secret.yaml (create from command line)
apiVersion: v1
kind: Secret
metadata:
  name: app-secrets
  namespace: your-app-prod
type: Opaque
stringData:
  connection-string: "Server=sql-server;Database=AppDb;User=sa;Password=..."
---
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: your-app
  namespace: your-app-prod
  labels:
    app: your-app
    version: v1.0.0
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: your-app
  template:
    metadata:
      labels:
        app: your-app
        version: v1.0.0
    spec:
      serviceAccountName: your-app-sa
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
      containers:
      - name: app
        image: yourregistry.azurecr.io/your-app:v1.0.0
        imagePullPolicy: IfNotPresent
        ports:
        - name: http
          containerPort: 8080
          protocol: TCP
        envFrom:
        - configMapRef:
            name: app-config
        env:
        - name: ConnectionStrings__Default
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 3
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
        startupProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 30
---
# service.yaml
apiVersion: v1
kind: Service
metadata:
  name: your-app
  namespace: your-app-prod
  labels:
    app: your-app
spec:
  type: ClusterIP
  selector:
    app: your-app
  ports:
  - name: http
    port: 80
    targetPort: 8080
    protocol: TCP
---
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: your-app
  namespace: your-app-prod
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.yourdomain.com
    secretName: your-app-tls
  rules:
  - host: api.yourdomain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: your-app
            port:
              number: 80
```

**Deploy:**

```bash
# Create secret from file
kubectl create secret generic app-secrets \
  --from-literal=connection-string="Server=..." \
  --namespace=your-app-prod

# Apply manifests
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml
kubectl apply -f ingress.yaml

# Verify deployment
kubectl get all -n your-app-prod
kubectl logs -f deployment/your-app -n your-app-prod
```

---

## Helm Chart

### Chart Structure

```
your-app/
├── Chart.yaml
├── values.yaml
├── values-dev.yaml
├── values-staging.yaml
├── values-prod.yaml
└── templates/
    ├── deployment.yaml
    ├── service.yaml
    ├── ingress.yaml
    ├── configmap.yaml
    ├── secret.yaml
    ├── serviceaccount.yaml
    ├── hpa.yaml
    └── pdb.yaml
```

### Chart.yaml

```yaml
apiVersion: v2
name: your-app
description: Excalibur application
version: 1.0.0
appVersion: "1.0.0"
type: application
dependencies: []
```

### values.yaml

```yaml
replicaCount: 3

image:
  repository: yourregistry.azurecr.io/your-app
  tag: "latest"
  pullPolicy: IfNotPresent

serviceAccount:
  create: true
  name: ""

resources:
  requests:
    memory: "256Mi"
    cpu: "250m"
  limits:
    memory: "512Mi"
    cpu: "500m"

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 80

service:
  type: ClusterIP
  port: 80
  targetPort: 8080

ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
  hosts:
    - host: api.yourdomain.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: your-app-tls
      hosts:
        - api.yourdomain.com

config:
  aspnetcoreEnvironment: "Production"
  aspnetcoreUrls: "http://+:8080"
  dispatch:
    outbox:
      processorInterval: "00:00:30"
      batchSize: 100

secrets:
  connectionString: ""  # Set via --set or values override
```

### templates/deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "your-app.fullname" . }}
  labels:
    {{- include "your-app.labels" . | nindent 4 }}
spec:
  {{- if not .Values.autoscaling.enabled }}
  replicas: {{ .Values.replicaCount }}
  {{- end }}
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      {{- include "your-app.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      annotations:
        checksum/config: {{ include (print $.Template.BasePath "/configmap.yaml") . | sha256sum }}
        checksum/secret: {{ include (print $.Template.BasePath "/secret.yaml") . | sha256sum }}
      labels:
        {{- include "your-app.selectorLabels" . | nindent 8 }}
    spec:
      serviceAccountName: {{ include "your-app.serviceAccountName" . }}
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
      containers:
      - name: {{ .Chart.Name }}
        image: "{{ .Values.image.repository }}:{{ .Values.image.tag | default .Chart.AppVersion }}"
        imagePullPolicy: {{ .Values.image.pullPolicy }}
        ports:
        - name: http
          containerPort: {{ .Values.service.targetPort }}
          protocol: TCP
        envFrom:
        - configMapRef:
            name: {{ include "your-app.fullname" . }}
        env:
        - name: ConnectionStrings__Default
          valueFrom:
            secretKeyRef:
              name: {{ include "your-app.fullname" . }}
              key: connection-string
        resources:
          {{- toYaml .Values.resources | nindent 10 }}
        livenessProbe:
          httpGet:
            path: /health
            port: http
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: http
          initialDelaySeconds: 10
          periodSeconds: 5
        startupProbe:
          httpGet:
            path: /health
            port: http
          initialDelaySeconds: 0
          periodSeconds: 5
          failureThreshold: 30
```

### Deploy with Helm

```bash
# Install
helm install your-app ./your-app \
  --namespace your-app-prod \
  --create-namespace \
  --values values-prod.yaml \
  --set secrets.connectionString="Server=..."

# Upgrade
helm upgrade your-app ./your-app \
  --namespace your-app-prod \
  --values values-prod.yaml \
  --set image.tag=v1.0.1

# Rollback
helm rollback your-app 1 --namespace your-app-prod

# Uninstall
helm uninstall your-app --namespace your-app-prod
```

---

## Autoscaling

### Horizontal Pod Autoscaler (HPA)

```yaml
# hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: your-app-hpa
  namespace: your-app-prod
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: your-app
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
      - type: Pods
        value: 4
        periodSeconds: 30
      selectPolicy: Max
```

### Pod Disruption Budget (PDB)

```yaml
# pdb.yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: your-app-pdb
  namespace: your-app-prod
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: your-app
```

---

## Event Sourcing Architecture

### Separate Write and Read Models (CQRS)

```yaml
# write-api.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: write-api
  namespace: your-app-prod
spec:
  replicas: 3
  selector:
    matchLabels:
      app: write-api
  template:
    metadata:
      labels:
        app: write-api
    spec:
      containers:
      - name: write-api
        image: yourregistry.azurecr.io/write-api:latest
        env:
        - name: EventStore__ConnectionString
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: eventstore-connection
---
# projections.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: projections
  namespace: your-app-prod
spec:
  replicas: 1  # Single instance for projection processing
  selector:
    matchLabels:
      app: projections
  template:
    metadata:
      labels:
        app: projections
    spec:
      containers:
      - name: projections
        image: yourregistry.azurecr.io/projections:latest
        env:
        - name: EventStore__ConnectionString
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: eventstore-connection
        - name: ReadStore__ConnectionString
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: readstore-connection
---
# read-api.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: read-api
  namespace: your-app-prod
spec:
  replicas: 5  # More read replicas
  selector:
    matchLabels:
      app: read-api
  template:
    metadata:
      labels:
        app: read-api
    spec:
      containers:
      - name: read-api
        image: yourregistry.azurecr.io/read-api:latest
        env:
        - name: ReadStore__ConnectionString
          valueFrom:
            secretKeyRef:
              name: app-secrets
              key: readstore-connection
```

---

## Secrets Management

### Azure Key Vault Integration (AKS)

```yaml
# serviceaccount.yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: your-app-sa
  namespace: your-app-prod
  annotations:
    azure.workload.identity/client-id: "your-client-id"
---
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    metadata:
      labels:
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: your-app-sa
      containers:
      - name: app
        env:
        - name: AZURE_TENANT_ID
          value: "your-tenant-id"
        - name: AZURE_CLIENT_ID
          value: "your-client-id"
        # Application reads secrets from Key Vault at runtime
```

**Program.cs:**

```csharp
if (builder.Environment.IsProduction())
{
    var keyVaultName = builder.Configuration["KeyVaultName"];
    builder.Configuration.AddAzureKeyVault(
        new Uri($"https://{keyVaultName}.vault.azure.net/"),
        new DefaultAzureCredential());
}
```

### AWS Secrets Manager (EKS)

```yaml
# Install Secrets Store CSI Driver
kubectl apply -f https://raw.githubusercontent.com/kubernetes-sigs/secrets-store-csi-driver/main/deploy/rbac-secretproviderclass.yaml

# secretproviderclass.yaml
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: aws-secrets
  namespace: your-app-prod
spec:
  provider: aws
  parameters:
    objects: |
      - objectName: "prod/your-app/connection-string"
        objectType: "secretsmanager"
---
# deployment.yaml with volume
spec:
  template:
    spec:
      serviceAccountName: your-app-sa
      volumes:
      - name: secrets
        csi:
          driver: secrets-store.csi.k8s.io
          readOnly: true
          volumeAttributes:
            secretProviderClass: "aws-secrets"
      containers:
      - name: app
        volumeMounts:
        - name: secrets
          mountPath: "/mnt/secrets"
          readOnly: true
```

---

## Monitoring and Observability

### Prometheus Metrics

```yaml
# servicemonitor.yaml (Prometheus Operator)
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: your-app
  namespace: your-app-prod
spec:
  selector:
    matchLabels:
      app: your-app
  endpoints:
  - port: http
    path: /metrics
    interval: 30s
```

**Enable metrics in Program.cs:**

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddPrometheusExporter();
        metrics.AddMeter("Excalibur.Dispatch");
    });

app.MapPrometheusScrapingEndpoint();  // /metrics endpoint
```

---

## CI/CD Integration

### GitOps with ArgoCD

```yaml
# argocd-application.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: your-app
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/your-org/your-app
    targetRevision: main
    path: k8s/overlays/prod
    helm:
      valueFiles:
        - values-prod.yaml
  destination:
    server: https://kubernetes.default.svc
    namespace: your-app-prod
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

### GitHub Actions Deployment

```yaml
name: Deploy to AKS

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Set AKS context
        uses: azure/aks-set-context@v3
        with:
          cluster-name: your-aks-cluster
          resource-group: your-rg

      - name: Deploy to AKS
        run: |
          kubectl apply -f k8s/
          kubectl rollout status deployment/your-app -n your-app-prod
```

---

## Troubleshooting

### Pod Not Starting

```bash
# Check pod events
kubectl describe pod <pod-name> -n your-app-prod

# Check logs
kubectl logs <pod-name> -n your-app-prod

# Check previous logs (if crashed)
kubectl logs <pod-name> --previous -n your-app-prod

# Interactive shell
kubectl exec -it <pod-name> -n your-app-prod -- /bin/sh
```

### Connection Issues

```bash
# Test DNS resolution
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup your-app

# Test connectivity
kubectl run -it --rm debug --image=curlimages/curl --restart=Never -- curl http://your-app/health

# Check network policies
kubectl get networkpolicy -n your-app-prod
```

### Resource Constraints

```bash
# Check resource usage
kubectl top pods -n your-app-prod
kubectl top nodes

# Describe node
kubectl describe node <node-name>

# Check events
kubectl get events -n your-app-prod --sort-by='.lastTimestamp'
```

---

## Next Steps

- **Docker:** [Docker containerization](docker.md) for image building
- **Serverless:** [Azure Functions](azure-functions.md) or [AWS Lambda](aws-lambda.md)
- **Security:** [Security Best Practices](security-best-practices.md)
- **Monitoring:** [Grafana Dashboards](../observability/grafana-dashboards.md)

---

## See Also

- [Docker Deployment](docker.md) - Build and optimize Docker images for Kubernetes deployments
- [Health Checks](../observability/health-checks.md) - Configure liveness, readiness, and startup probes
- [Leader Election](../leader-election/index.md) - Coordinate single-instance workloads across replicas

---

**Last Updated:** 2026-01-01
**Framework:** Excalibur 1.0.0
**Kubernetes:** 1.28+
