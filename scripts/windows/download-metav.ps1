$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$data = Join-Path $root "data"
$target = Join-Path $data "metav"
New-Item -ItemType Directory -Force -Path $data | Out-Null
if (Test-Path $target) { Write-Host "Meta-V already exists: $target"; exit 0 }
git clone "https://github.com/theonize/KJV-bible-database-with-metadata-MetaV-.git" $target
