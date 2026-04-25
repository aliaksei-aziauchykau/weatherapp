# WeatherApp: .\.deploy\local\Run-Weather-Local.ps1 up -d
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)
$core = Join-Path (Split-Path -Parent $PSScriptRoot) "Deploy-WeatherApp.ps1"
& $core -DeployConfigDirectory $PSScriptRoot @Rest
exit $LASTEXITCODE
