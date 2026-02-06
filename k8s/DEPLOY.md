# Kubernetes Deployment Guide — Truckload EBS

This guide deploys the Truckload Extension Backend Service (ASP.NET Core + PostgreSQL) to a Kubernetes cluster.

## Architecture

```
Internet
  │
  ▼
Ingress (nginx / traefik)
  │
  ▼
Service: truckload-backend (ClusterIP :80)
  │
  ▼
Deployment: truckload-backend (:8080)
  │
  ▼
Service: postgres (Headless :5432)
  │
  ▼
Deployment: postgres (:5432)
  │
  ▼
PVC: postgres-pvc (1Gi)
```

Everything runs in the `truckload` namespace.

---

## Prerequisites

- A Kubernetes cluster (k3s, microk8s, EKS, AKS, GKE, etc.)
- `kubectl` configured and pointing at your cluster
- An Ingress Controller installed (nginx-ingress or traefik) for external access
- Docker (or Podman) for building the image
- A container registry (Docker Hub, GHCR, or a private registry) — or load the image directly if using a single-node cluster

---

## Step 1: Build and Push the Image

```bash
# From the repo root
cd backend/Truckload.Ebs

# Build the image
docker build -t truckload-ebs:latest .

# Tag for your registry (pick one)
docker tag truckload-ebs:latest ghcr.io/<your-user>/truckload-ebs:latest
# or
docker tag truckload-ebs:latest <your-registry>/truckload-ebs:latest

# Push
docker push ghcr.io/<your-user>/truckload-ebs:latest
```

**Single-node clusters (k3s, microk8s, minikube):**
You can skip the registry and import the image directly:

```bash
# k3s
docker save truckload-ebs:latest | sudo k3s ctr images import -

# microk8s
docker save truckload-ebs:latest > truckload-ebs.tar
microk8s ctr image import truckload-ebs.tar

# minikube
minikube image load truckload-ebs:latest
```

---

## Step 2: Update the Image Reference

Edit `k8s/backend.yaml` and replace the placeholder image:

```yaml
image: truckload-ebs:latest    # ← Replace with your registry path
```

For example:
```yaml
image: ghcr.io/demortes/truckload-ebs:latest
```

If using a **private registry**, create an image pull secret:

```bash
kubectl create secret docker-registry regcred -n truckload \
  --docker-server=ghcr.io \
  --docker-username=<github-user> \
  --docker-password=<github-pat>
```

Then add to the pod spec in `backend.yaml`:

```yaml
spec:
  imagePullSecrets:
    - name: regcred
```

---

## Step 3: Create the Namespace and Secrets

```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Create secrets (replace with your real values)
kubectl create secret generic truckload-secrets -n truckload \
  --from-literal=POSTGRES_PASSWORD='your-strong-db-password' \
  --from-literal=TWITCH_EXTENSION_CLIENT_ID='your-twitch-client-id' \
  --from-literal=TWITCH_EXTENSION_SECRET='your-base64-twitch-secret'
```

Alternatively, fill in the base64-encoded values in `k8s/secret.yaml` and apply:

```bash
# Encode a value
echo -n 'your-value' | base64

# Apply
kubectl apply -f k8s/secret.yaml
```

---

## Step 4: Deploy PostgreSQL

```bash
kubectl apply -f k8s/postgres.yaml
```

Wait for it to become ready:

```bash
kubectl get pods -n truckload -l app=postgres -w
```

You should see `1/1 Running` and the readiness probe passing.

---

## Step 5: Deploy the Backend

```bash
kubectl apply -f k8s/backend.yaml
```

Watch the rollout:

```bash
kubectl rollout status deployment/truckload-backend -n truckload
```

Verify with:

```bash
# Check pods
kubectl get pods -n truckload

# Check logs
kubectl logs -n truckload -l app=truckload-backend --tail=50

# Quick health check via port-forward
kubectl port-forward -n truckload svc/truckload-backend 8080:80
# Then: curl http://localhost:8080/api/health
```

---

## Step 6: Expose with Ingress

Edit `k8s/ingress.yaml` — replace `truckload.example.com` with your domain.

```bash
kubectl apply -f k8s/ingress.yaml
```

Point your domain's DNS (A or CNAME record) to the cluster's external IP.

### TLS with cert-manager

If you have [cert-manager](https://cert-manager.io/) installed, uncomment the TLS section in `ingress.yaml`:

```yaml
metadata:
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
    - hosts:
        - truckload.example.com
      secretName: truckload-tls
```

### Alternative: NodePort (no Ingress Controller)

If you don't have an Ingress Controller, change the backend Service type in `backend.yaml`:

```yaml
spec:
  type: NodePort
  ports:
    - port: 80
      targetPort: 8080
      nodePort: 30080    # Access via http://<node-ip>:30080
```

---

## Step 7: Verify End-to-End

```bash
# Health check
curl https://truckload.example.com/api/health
# Expected: {"status":"ok","version":"2.0.0"}

# Simulate ingest (requires a valid API key from the config page)
curl -X POST https://truckload.example.com/api/ingest \
  -H "X-Api-Key: <your-key>" \
  -H "Content-Type: application/json" \
  -d '{"connected":true,"game":"ats","job":{"active":true,"cargo":"Electronics","source":"LA","destination":"Phoenix","distance":372,"etaMinutes":245}}'

# Fetch telemetry
curl https://truckload.example.com/api/telemetry/<channel-id>
```

---

## Step 8: Update the Frontend

Set `VITE_BACKEND_URL` to your deployed backend URL before building the Twitch extension:

```bash
cd src
VITE_BACKEND_URL=https://truckload.example.com npm run build
```

Upload the built `dist/` folder to the Twitch Developer Console as your extension assets.

---

## Quick Reference

| Command | Purpose |
|---------|---------|
| `kubectl apply -f k8s/` | Apply all manifests at once |
| `kubectl get all -n truckload` | See all resources |
| `kubectl logs -f -n truckload -l app=truckload-backend` | Stream backend logs |
| `kubectl logs -f -n truckload -l app=postgres` | Stream postgres logs |
| `kubectl exec -it -n truckload deploy/postgres -- psql -U truckload` | Open psql shell |
| `kubectl rollout restart deployment/truckload-backend -n truckload` | Restart backend |
| `kubectl delete namespace truckload` | Tear down everything |

---

## Scaling Notes

- **Backend**: Safe to scale horizontally (`replicas: 2+`). Each pod is stateless — it reads/writes to the shared PostgreSQL database.
- **PostgreSQL**: This deployment runs a single replica. For production HA, consider a managed database (RDS, Cloud SQL, Azure Database) or an operator like CloudNativePG.
- **Resource limits**: The default limits (256Mi RAM / 500m CPU for the backend) are conservative. Adjust based on observed usage.

---

## Updating the Backend

```bash
# Build new image
cd backend/Truckload.Ebs
docker build -t ghcr.io/<user>/truckload-ebs:v2 .
docker push ghcr.io/<user>/truckload-ebs:v2

# Update the deployment
kubectl set image deployment/truckload-backend -n truckload \
  backend=ghcr.io/<user>/truckload-ebs:v2

# Watch rollout
kubectl rollout status deployment/truckload-backend -n truckload
```

EF Core migrations run automatically on startup in Development mode. For Production, run migrations manually before deploying:

```bash
# Option 1: Run migration from a one-off pod
kubectl run migrate --rm -it -n truckload \
  --image=ghcr.io/<user>/truckload-ebs:v2 \
  --env="ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=truckload;Username=truckload;Password=<pw>" \
  --env="ASPNETCORE_ENVIRONMENT=Development" \
  -- dotnet Truckload.Ebs.dll --migrate-only

# Option 2: Use dotnet ef from your dev machine with a port-forward
kubectl port-forward -n truckload svc/postgres 5432:5432
dotnet ef database update --project backend/Truckload.Ebs
```
