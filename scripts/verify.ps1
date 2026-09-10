$ErrorActionPreference = 'Stop'

function Invoke-Check {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [scriptblock]$Command
    )

    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Invoke-Check -Name 'Backend tests' -Command {
    dotnet test .\MiniOrderManagement.slnx --configuration Release
}

Push-Location .\ui
try {
    Invoke-Check -Name 'Frontend dependency installation' -Command {
        npm ci
    }

    Invoke-Check -Name 'Frontend build' -Command {
        npm run build
    }
}
finally {
    Pop-Location
}

Invoke-Check -Name 'Docker Compose validation' -Command {
    docker compose config --quiet
}

Invoke-Check -Name 'Docker image builds' -Command {
    docker compose build
}

Write-Host "`nAll local verification checks passed." -ForegroundColor Green
