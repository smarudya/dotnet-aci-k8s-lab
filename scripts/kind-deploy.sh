#!/usr/bin/env bash
# Lab deploy to a local kind cluster. Usage: ./scripts/kind-deploy.sh [--delete]
# Prereqs: docker, kind, kubectl, curl.
set -euo pipefail

CLUSTER="catalog-lab"
IMAGE="catalog-api:local"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INGRESS_URL="https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.11.2/deploy/static/provider/kind/deploy.yaml"
METRICS_URL="https://github.com/kubernetes-sigs/metrics-server/releases/download/v0.7.2/components.yaml"

for tool in docker kind kubectl curl; do
  command -v "$tool" >/dev/null || { echo "Missing prerequisite: $tool" >&2; exit 1; }
done

if [[ "${1:-}" == "--delete" ]]; then
  kind delete cluster --name "$CLUSTER"
  exit 0
fi

if ! kind get clusters | grep -qx "$CLUSTER"; then
  kind create cluster --config "$ROOT/scripts/kind-config.yaml"
fi
kubectl config use-context "kind-$CLUSTER" >/dev/null

echo "==> Building image $IMAGE"
docker build -t "$IMAGE" "$ROOT"
kind load docker-image "$IMAGE" --name "$CLUSTER"

echo "==> Installing ingress-nginx"
kubectl apply -f "$INGRESS_URL"

echo "==> Installing metrics-server (lab: --kubelet-insecure-tls; needed for HPA)"
kubectl apply -f "$METRICS_URL"
kubectl -n kube-system patch deployment metrics-server --type=json \
  -p='[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-insecure-tls"}]' || true

kubectl -n ingress-nginx wait --for=condition=Available deployment/ingress-nginx-controller --timeout=180s

echo "==> Applying manifests"
kubectl apply -k "$ROOT/k8s"
kubectl -n catalog-lab rollout status deployment/catalog-api --timeout=180s

echo "==> Smoke test"
for i in $(seq 1 20); do
  if curl -fsS http://catalog.localtest.me:8081/ready; then echo; break; fi
  sleep 3
done
curl -fsS http://catalog.localtest.me:8081/api/info; echo
kubectl -n catalog-lab get deploy,pods,svc,hpa,ingress
echo "Done. Try: curl http://catalog.localtest.me:8081/api/products?category=displays"
echo "Cleanup:  ./scripts/kind-deploy.sh --delete"
