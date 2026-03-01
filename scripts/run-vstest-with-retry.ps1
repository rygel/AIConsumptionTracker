param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath,

    [Parameter(Mandatory = $true)]
    [string]$SuiteName,

    [string]$ResultsDirectory = "TestResults",
    [string]$QuarantineFile = ".github\test-quarantine.txt",
    [int]$MaxRetries = 1,
    [int]$AttemptTimeoutMinutes = 10
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AssemblyPath)) {
    throw "Test assembly not found: $AssemblyPath"
}

if ($MaxRetries -lt 0) {
    throw "MaxRetries must be >= 0."
}

if ($AttemptTimeoutMinutes -lt 1) {
    throw "AttemptTimeoutMinutes must be >= 1."
}

$resolvedResultsDirectory = if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory
}
else {
    Join-Path (Get-Location) $ResultsDirectory
}

New-Item -ItemType Directory -Path $resolvedResultsDirectory -Force | Out-Null

$testCaseFilter = $null
if (Test-Path -LiteralPath $QuarantineFile) {
    $quarantinedTests = Get-Content -LiteralPath $QuarantineFile |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith("#", [System.StringComparison]::Ordinal) }
        ForEach-Object {
            $parts = $_ -split "\|"
            $parts[0].Trim()
        } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique

    if ($quarantinedTests.Count -gt 0) {
        $testCaseFilter = ($quarantinedTests | ForEach-Object { "FullyQualifiedName!=$_" }) -join "&"
        Write-Host "Applying quarantine filter with $($quarantinedTests.Count) test(s)." -ForegroundColor Yellow
    }
}

function Invoke-VsTestRun {
    param(
        [string]$TrxFileName,
        [int]$AttemptNumber
    )

    $arguments = @(
        "vstest",
        $AssemblyPath,
        "--logger", "console;verbosity=normal",
        "--logger", "trx;LogFileName=$TrxFileName",
        "--ResultsDirectory:$resolvedResultsDirectory"
    )

    if (-not [string]::IsNullOrWhiteSpace($testCaseFilter)) {
        $arguments += @("--TestCaseFilter", $testCaseFilter)
    }

    $stdoutLog = Join-Path $resolvedResultsDirectory "$SuiteName-attempt$AttemptNumber-stdout.log"
    $stderrLog = Join-Path $resolvedResultsDirectory "$SuiteName-attempt$AttemptNumber-stderr.log"
    if (Test-Path -LiteralPath $stdoutLog) {
        Remove-Item -LiteralPath $stdoutLog -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $stderrLog) {
        Remove-Item -LiteralPath $stderrLog -Force -ErrorAction SilentlyContinue
    }

    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList $arguments `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog

    $timeoutMs = [Math]::Min([int]::MaxValue, $AttemptTimeoutMinutes * 60 * 1000)
    $completed = $process.WaitForExit($timeoutMs)
    if (-not $completed) {
        Write-Host "Attempt $AttemptNumber exceeded ${AttemptTimeoutMinutes}m timeout. Killing dotnet vstest process..." -ForegroundColor Red
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        catch {
            Write-Host "Failed to terminate timed out process $($process.Id): $($_.Exception.Message)" -ForegroundColor Yellow
        }

        if (Test-Path -LiteralPath $stdoutLog) {
            Write-Host "--- Last 200 lines stdout ($stdoutLog) ---"
            Get-Content -LiteralPath $stdoutLog -Tail 200
        }
        if (Test-Path -LiteralPath $stderrLog) {
            Write-Host "--- Last 200 lines stderr ($stderrLog) ---"
            Get-Content -LiteralPath $stderrLog -Tail 200
        }

        return 124
    }

    $process.WaitForExit()
    $exitCode = [int]$process.ExitCode
    Write-Host "dotnet vstest attempt $AttemptNumber exited with code $exitCode."
    if ($exitCode -ne 0) {
        if (Test-Path -LiteralPath $stdoutLog) {
            Write-Host "--- Last 200 lines stdout ($stdoutLog) ---"
            Get-Content -LiteralPath $stdoutLog -Tail 200
        }
        if (Test-Path -LiteralPath $stderrLog) {
            Write-Host "--- Last 200 lines stderr ($stderrLog) ---"
            Get-Content -LiteralPath $stderrLog -Tail 200
        }
    }

    return $exitCode
}

function Write-SlowestTestsReport {
    $trxFile = Get-ChildItem -LiteralPath $resolvedResultsDirectory -Filter "$SuiteName*.trx" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if (-not $trxFile) {
        Write-Host "No TRX file found for slow-test reporting."
        return
    }

    [xml]$trx = Get-Content -LiteralPath $trxFile.FullName -Raw
    $results = @($trx.TestRun.Results.UnitTestResult)

    if ($results.Count -eq 0) {
        Write-Host "No unit test result entries found in $($trxFile.Name)."
        return
    }

    $slowest = $results |
        ForEach-Object {
            $durationText = [string]$_.duration
            if ([string]::IsNullOrWhiteSpace($durationText)) {
                return
            }

            $duration = [TimeSpan]::Zero
            if (-not [TimeSpan]::TryParse($durationText, [ref]$duration)) {
                return
            }

            [PSCustomObject]@{
                Name       = [string]$_.testName
                DurationMs = [math]::Round($duration.TotalMilliseconds, 1)
            }
        } |
        Sort-Object DurationMs -Descending |
        Select-Object -First 10

    if (-not $slowest -or $slowest.Count -eq 0) {
        Write-Host "No parseable durations found in $($trxFile.Name)."
        return
    }

    Write-Host "Top slow tests for $SuiteName (from $($trxFile.Name)):" -ForegroundColor Cyan
    foreach ($entry in $slowest) {
        Write-Host ("  {0,8:N1} ms  {1}" -f $entry.DurationMs, $entry.Name)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "### $SuiteName slowest tests"
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value ""
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "| Duration (ms) | Test |"
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "|---:|---|"
        foreach ($entry in $slowest) {
            Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value ("| {0:N1} | {1} |" -f $entry.DurationMs, $entry.Name)
        }
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value ""
    }
}

$attempt = 1
$maxAttempts = $MaxRetries + 1
$exitCode = 1

while ($attempt -le $maxAttempts) {
    $suffix = if ($attempt -eq 1) { "" } else { "-retry$($attempt - 1)" }
    $trxFileName = "$SuiteName$suffix.trx"

    Write-Host "Running $SuiteName attempt $attempt of $maxAttempts..." -ForegroundColor Cyan
    $exitCode = Invoke-VsTestRun -TrxFileName $trxFileName -AttemptNumber $attempt

    if ($exitCode -eq 0) {
        break
    }

    if ($attempt -lt $maxAttempts) {
        Write-Host "Attempt $attempt failed for $SuiteName. Retrying once..." -ForegroundColor Yellow
    }

    $attempt++
}

Write-SlowestTestsReport

if ($exitCode -ne 0) {
    Write-Host "$SuiteName failed after $maxAttempts attempt(s)." -ForegroundColor Red
    exit $exitCode
}

Write-Host "$SuiteName passed." -ForegroundColor Green
