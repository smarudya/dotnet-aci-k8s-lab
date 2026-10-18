<#
.SYNOPSIS  Lab deploy to a local kind cluster (PowerShell).
.EXAMPLE   ./scripts/kind-deploy.ps1
.EXAMPLE   ./scripts/kind-deploy.ps1 -Delete
Prereqs: docker, kind, kubectl.
#>
[CmdletBinding()]
param([switch]$Delete)

$ErrorActionPreference = 'Stop'
$Cluster = 'catalog-lab'
$Image = 'catalog-api:local'
$Root = Split-Path -Parent $PSScriptRoot
$IngressUrl = 'https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.11.2/deploy/static/provider/kind/deploy.yaml'
$MetricsUrl = 'https://github.com/kubernetes-sigs/metrics-server/releases/download/v0.7.2/components.yaml'

function Invoke-Checked([scriptblock]$Cmd) {
    & $Cmd
    if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $Cmd" }
}

foreach ($tool in 'docker', 'kind', 'kubectl') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Missing prerequisite: $tool" }
}

if ($Delete) { Invoke-Checked { kind delete cluster --name $Cluster }; return }

if (-not ((kind get clusters) -contains $Cluster)) {
    Invoke-Checked { kind create cluster --config "$Root/scripts/kind-config.yaml" }
}
Invoke-Checked { kubectl config use-context "kind-$Cluster" }

Write-Host "==> Building image $Image"
Invoke-Checked { docker build -t $Image $Root }
Invoke-Checked { kind load docker-image $Image --name $Cluster }

Write-Host '==> Installing ingress-nginx'
Invoke-Checked { kubectl apply -f $IngressUrl }

Write-Host '==> Installing metrics-server (lab: --kubelet-insecure-tls; needed for HPA)'
Invoke-Checked { kubectl apply -f $MetricsUrl }
$patch = '[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-insecure-tls"}]'
kubectl -n kube-system patch deployment metrics-server --type=json -p $patch | Out-Null

Invoke-Checked { kubectl -n ingress-nginx wait --for=condition=Available deployment/ingress-nginx-controller --timeout=180s }

Write-Host '==> Applying manifests'
Invoke-Checked { kubectl apply -k "$Root/k8s" }
Invoke-Checked { kubectl -n catalog-lab rollout status deployment/catalog-api --timeout=180s }

Write-Host '==> Smoke test'
$ready = $false
for ($i = 0; $i -lt 20 -and -not $ready; $i++) {
    try { Invoke-RestMethod 'http://catalog.localtest.me:8081/ready' | Out-Null; $ready = $true }
    catch { Start-Sleep -Seconds 3 }
}
if (-not $ready) { throw '/ready did not return 200 in time' }
Invoke-RestMethod 'http://catalog.localtest.me:8081/api/info' | ConvertTo-Json
Invoke-Checked { kubectl -n catalog-lab get deploy,pods,svc,hpa,ingress }
Write-Host 'Done. Cleanup: ./scripts/kind-deploy.ps1 -Delete'
