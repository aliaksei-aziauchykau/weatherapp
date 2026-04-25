param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)
$core = Join-Path (Split-Path -Parent $PSScriptRoot) "Deploy-WeatherApp.ps1"
& $core -DeployConfigDirectory $PSScriptRoot @Rest
exit $LASTEXITCODE
