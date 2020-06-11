param(
    [string]$dotnet="dotnet",
    [ValidateSet("all","netcoreapp3.1","net47","net48")]
    [string]$framework="all",
    [ValidateSet("all","production","testing")]
    [string]$test="all",
    [string]$filter="",
    [string]$logger="",
    [ValidateSet("quiet","minimal","normal","detailed","diagnostic")]
    [string]$v="normal"
)

Import-Module $PSScriptRoot\powershell\common.psm1

$frameworks = Get-ChildItem -Path "$PSScriptRoot\..\Tests\bin" | Where-Object Name -CNotIn "netstandard2.0", "netstandard2.1" | Select-Object -expand Name

$targets = [ordered]@{
    "production" = "Production.Tests"
    "testing" = "SystematicTesting.Tests"
}

[System.Environment]::SetEnvironmentVariable('COYOTE_CLI_TELEMETRY_OPTOUT','1')

Write-Comment -prefix "." -text "Running the Coyote tests" -color "yellow"
foreach ($kvp in $targets.GetEnumerator()) {
    if (($test -ne "all") -and ($test -ne $($kvp.Name))) {
        continue
    }

    foreach ($f in $frameworks) {
        if (($framework -ne "all") -and ($f -ne $framework)) {
            continue
        }

        $target = "$PSScriptRoot\..\Tests\$($kvp.Value)\$($kvp.Value).csproj"
        Invoke-DotnetTest -dotnet $dotnet -project $($kvp.Name) -target $target -filter $filter -logger $logger -framework $f -verbosity $v
    }
}

Write-Comment -prefix "." -text "Done" -color "green"
