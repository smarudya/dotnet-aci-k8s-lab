# minikube alternative (notes)

kind is the scripted path; these are the equivalent minikube steps (bash / PowerShell identical).

```bash
minikube start --driver=docker
minikube addons enable ingress
minikube addons enable metrics-server

# Build straight into minikube's image store (no registry needed)
minikube image build -t catalog-api:local .

kubectl apply -k k8s
kubectl -n catalog-lab rollout status deployment/catalog-api

# Access (Service is ClusterIP):
kubectl -n catalog-lab port-forward svc/catalog-api 8080:80
curl http://localhost:8080/ready

minikube delete   # cleanup
```

Note: on minikube with the Docker driver on Windows/macOS, the ingress IP is not directly reachable;
use `minikube tunnel` or `kubectl port-forward`.
