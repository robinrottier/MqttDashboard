<#
.SYNOPSIS
    Release automation helper for MqttDashboard.

.DESCRIPTION
    Performs a full patch-release workflow from local source:

      1.  preflight       Verify required tools (git, dotnet, gh)
      2.  clean           Auto-commit TODO.md if the only dirty file; else abort
      3.  build-debug     Build + test (Debug configuration)
      4.  build-release   Build + test (Release configuration)
      5.  sync            Fetch + pull --rebase from origin
      6.  version         Compute next semver tag from latest git tag
      7.  changelog       Insert versioned section into CHANGELOG.md
      8.  push-changelog  Commit + push the changelog update
      9.  pr              Create PR → main, wait for CI checks, merge
      10. tag             Create annotated tag and push it
      11. wait-workflows  Wait for release workflows triggered by the tag

    Runs on pwsh 7+ (Windows, Linux, WSL).
    When invoked under Windows PowerShell 5.1, automatically re-launches in pwsh
    if available on PATH.

.PARAMETER DryRun
    Skip all remote operations (push, merge, tag push).

.PARAMETER NoGh
    Disable GitHub CLI automation (PR creation, workflow polling).

.PARAMETER SkipReleaseTests
    Skip running tests for the Release build.

.PARAMETER ReleaseTestFilter
    Test filter expression for Release config. Default: 'TestCategory!=Playwright'.

.PARAMETER Parallel
    Run Debug and Release build+test stages concurrently (output is written to
    separate temp files and replayed on completion).

.PARAMETER WorkflowTimeoutMinutes
    Maximum minutes to wait for GitHub Actions workflows (default: 30).

.PARAMETER BumpType
    Version component to increment: patch (default), minor, or major.

.PARAMETER From
    Start from a named step, skipping all earlier ones. Useful to resume after
    a failure without re-running build/test.
    Step names: preflight clean build-debug build-release sync version
                changelog push-changelog pr tag wait-workflows

.PARAMETER Only
    Run exactly one named step and exit.

.PARAMETER Skip
    Step name(s) to skip. Accepts an array or a comma-separated string.

.PARAMETER NonInteractive
    Suppress all interactive prompts; abort automatically on failure.
    Implied when stdin is redirected (e.g. CI pipelines).

.EXAMPLE
    pwsh ./scripts/release.ps1
    pwsh ./scripts/release.ps1 -DryRun
    pwsh ./scripts/release.ps1 -From sync
    pwsh ./scripts/release.ps1 -Only build-debug
    pwsh ./scripts/release.ps1 -Skip build-debug,build-release -DryRun
    pwsh ./scripts/release.ps1 -BumpType minor -WorkflowTimeoutMinutes 45

.NOTES
    Environment variable fallbacks:
      DRYRUN=1               equivalent to -DryRun
      NO_GH=1                equivalent to -NoGh
      SKIP_RELEASE_TESTS=1   equivalent to -SkipReleaseTests
      RELEASE_TEST_FILTER    equivalent to -ReleaseTestFilter
#>

[CmdletBinding()]
Param(
    [switch]$DryRun,
    [switch]$NoGh,
    [switch]$SkipReleaseTests,
    [string]$ReleaseTestFilter = '',
    [switch]$Parallel,
    [int]$WorkflowTimeoutMinutes = 30,
    [ValidateSet('patch','minor','major')]
    [string]$BumpType = 'patch',
    [string]$From = '',
    [string]$Only = '',
    [string[]]$Skip = @(),
    [switch]$NonInteractive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── Auto-restart in pwsh 7+ when invoked from Windows PowerShell 5.1 ─────────
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $pwshExe = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwshExe) {
        Write-Host "Windows PowerShell detected — re-launching in pwsh 7+..." -ForegroundColor Yellow
        $fwdArgs = [System.Collections.Generic.List[string]]::new()
        $fwdArgs.Add('-NoProfile'); $fwdArgs.Add('-File'); $fwdArgs.Add($MyInvocation.MyCommand.Path)
        foreach ($kv in $MyInvocation.BoundParameters.GetEnumerator()) {
            if ($kv.Value -is [switch]) {
                if ($kv.Value.IsPresent) { $fwdArgs.Add("-$($kv.Key)") }
            } elseif ($kv.Value -is [string[]]) {
                $fwdArgs.Add("-$($kv.Key)"); $fwdArgs.AddRange([string[]]$kv.Value)
            } else {
                $fwdArgs.Add("-$($kv.Key)"); $fwdArgs.Add("$($kv.Value)")
            }
        }
        & $pwshExe.Source @fwdArgs
        exit $LASTEXITCODE
    }
    Write-Warning "pwsh (PowerShell 7+) not found on PATH. Continuing on Windows PowerShell — some features may not work correctly."
}

# ─── Repo root ────────────────────────────────────────────────────────────────
$RepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $RepoRoot

# ─── Console helpers ──────────────────────────────────────────────────────────
function Write-Header([string]$msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Step([string]$msg)   { Write-Host "    $msg" -ForegroundColor Gray }
function Write-Ok([string]$msg)     { Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Warn([string]$msg)   { Write-Host "  ⚠ $msg" -ForegroundColor Yellow }
function Write-Fail([string]$msg)   { Write-Host "  ✗ $msg" -ForegroundColor Red }

# ─── Effective flags ──────────────────────────────────────────────────────────
$IsDryRun      = $DryRun      -or $env:DRYRUN              -eq '1'
$IsNoGh        = $NoGh        -or $env:NO_GH               -eq '1'
$IsSkipTests   = $SkipReleaseTests -or $env:SKIP_RELEASE_TESTS -eq '1'
$EffTestFilter = if ($ReleaseTestFilter) { $ReleaseTestFilter }
                 elseif ($env:RELEASE_TEST_FILTER) { $env:RELEASE_TEST_FILTER }
                 else { 'TestCategory!=Playwright' }

# Interactive only when attached to a real terminal and not suppressed
$IsInteractive = -not $NonInteractive -and
                 -not $IsDryRun -and
                 [Environment]::UserInteractive -and
                 -not [Console]::IsInputRedirected

# ─── Step catalogue ───────────────────────────────────────────────────────────
$StepOrder = @(
    'preflight', 'clean', 'build-debug', 'build-release',
    'sync', 'version', 'changelog', 'push-changelog',
    'pr', 'tag', 'wait-workflows'
)
$StepDesc = @{
    'preflight'      = 'Preflight checks (tools, git remote)'
    'clean'          = 'Ensure clean working tree'
    'build-debug'    = 'Build and test (Debug)'
    'build-release'  = 'Build and test (Release)'
    'sync'           = 'Pull and sync with remote'
    'version'        = 'Compute next version'
    'changelog'      = 'Update CHANGELOG.md'
    'push-changelog' = 'Commit and push changelog'
    'pr'             = 'Create PR → wait for CI → merge'
    'tag'            = 'Create and push release tag'
    'wait-workflows' = 'Wait for release workflows'
}

# Resolve skip set (accepts array or comma-separated strings)
$SkipSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($s in $Skip) { foreach ($part in ($s -split ',')) { [void]$SkipSet.Add($part.Trim()) } }

# Build the ordered list of steps to run
function Get-StepsToRun {
    if ($Only) { return @($Only.Trim()) }
    $started = [string]::IsNullOrEmpty($From)
    return $StepOrder | Where-Object {
        if (-not $started -and ($_ -ieq $From.Trim())) { $started = $true }
        $started -and -not $SkipSet.Contains($_)
    }
}

# ─── Shared state populated by steps ─────────────────────────────────────────
$script:NextVersion    = $null
$script:CurrentBranch  = $null

# ─── Command helpers ──────────────────────────────────────────────────────────

# Run a command, streaming output to the terminal. Returns exit code.
function Invoke-Cmd([string]$Exe, [string[]]$ArgList) {
    Write-Step "→ $Exe $($ArgList -join ' ')"
    & $Exe @ArgList
    return $LASTEXITCODE
}

# Run a command and return its stdout as a trimmed string (stderr discarded).
function Get-CmdOutput([string]$Exe, [string[]]$ArgList) {
    $out = & $Exe @ArgList 2>$null
    return ($out -join "`n").Trim()
}

function Assert-Cmd([string]$Exe, [string[]]$ArgList, [string]$ErrorMsg = '') {
    $code = Invoke-Cmd $Exe $ArgList
    if ($code -ne 0) { throw $(if ($ErrorMsg) { $ErrorMsg } else { "$Exe exited with code $code" }) }
}

# ─── Step: preflight ─────────────────────────────────────────────────────────
function Step-Preflight {
    foreach ($tool in @('git', 'dotnet')) {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
            throw "$tool not found on PATH. Install it and ensure it is on PATH."
        }
    }
    $ghAvail = [bool](Get-Command gh -ErrorAction SilentlyContinue)
    if (-not $IsNoGh -and -not $ghAvail) {
        throw "gh CLI required but not found. Install from https://cli.github.com/ or pass -NoGh to skip GH automation."
    }
    if ($IsNoGh -and -not $ghAvail) {
        Write-Warn "gh CLI not available — PR automation and workflow polling disabled"
    }
    $remote = Get-CmdOutput git @('remote', 'get-url', 'origin')
    if (-not $remote) { throw "git remote 'origin' not configured. Add an origin remote and retry." }
    Write-Ok "git, dotnet$(if ($ghAvail) { ', gh' }) found"
    Write-Ok "Remote: $remote"
}

# ─── Step: clean ─────────────────────────────────────────────────────────────
function Step-CleanTree {
    $status = Get-CmdOutput git @('status', '--porcelain')
    if ([string]::IsNullOrWhiteSpace($status)) { Write-Ok "Working tree clean"; return }

    $lines = ($status -split "`n") | Where-Object { $_ -ne '' }
    $isTodoOnly = $lines.Count -eq 1 -and $lines[0] -match 'TODO\.md$'

    if ($isTodoOnly) {
        Write-Step "Only TODO.md modified — auto-committing"
        Assert-Cmd git @('add', 'TODO.md')
        Assert-Cmd git @('commit', '-m', 'chore: update TODO')
        Write-Ok "TODO.md committed"
    } else {
        throw "Working tree is dirty. Commit or stash changes before releasing.`n$status"
    }
}

# ─── Step: build-debug ───────────────────────────────────────────────────────
function Step-BuildDebug {
    Write-Step "Building (Debug)..."
    Assert-Cmd dotnet @('build', 'MqttDashboard.slnx', '-c', 'Debug') "Debug build failed"
    Write-Step "Testing (Debug)..."
    Assert-Cmd dotnet @('test', 'MqttDashboard.slnx', '-c', 'Debug', '--no-build') "Debug tests failed"
    Write-Ok "Debug build + tests passed"
}

# ─── Step: build-release ─────────────────────────────────────────────────────
function Step-BuildRelease {
    if ($Parallel) {
        # Run both configs concurrently; replay captured output afterwards.
        Write-Step "Building Debug + Release in parallel..."
        $dOut  = [System.IO.Path]::GetTempFileName()
        $rOut  = [System.IO.Path]::GetTempFileName()
        $dCmd  = "dotnet build MqttDashboard.slnx -c Debug && dotnet test MqttDashboard.slnx -c Debug --no-build"
        $filter = $EffTestFilter
        $rTest = if ($IsSkipTests) { '' } else { " && dotnet test MqttDashboard.slnx -c Release --no-build --filter `"$filter`"" }
        $rCmd  = "dotnet build MqttDashboard.slnx -c Release$rTest"
        $p1 = Start-Process pwsh -ArgumentList @('-NoProfile','-Command',$dCmd) -PassThru -RedirectStandardOutput $dOut -RedirectStandardError "$dOut.err" -NoNewWindow
        $p2 = Start-Process pwsh -ArgumentList @('-NoProfile','-Command',$rCmd) -PassThru -RedirectStandardOutput $rOut -RedirectStandardError "$rOut.err" -NoNewWindow
        Wait-Process -Id $p1.Id, $p2.Id
        Write-Host (Get-Content $dOut -Raw) -ForegroundColor Gray
        Write-Host (Get-Content $rOut -Raw) -ForegroundColor Gray
        Remove-Item $dOut, "$dOut.err", $rOut, "$rOut.err" -ErrorAction SilentlyContinue
        if ($p1.ExitCode -ne 0) { throw "Debug build/test failed" }
        if ($p2.ExitCode -ne 0) { throw "Release build/test failed" }
        Write-Ok "Parallel Debug + Release build and tests passed"
        return   # skip the sequential Release-only logic below
    }

    Write-Step "Building (Release)..."
    Assert-Cmd dotnet @('build', 'MqttDashboard.slnx', '-c', 'Release') "Release build failed"
    if ($IsSkipTests) { Write-Warn "Skipping Release tests (-SkipReleaseTests)"; return }
    Write-Step "Testing (Release) [filter: $EffTestFilter]..."
    Assert-Cmd dotnet @('test', 'MqttDashboard.slnx', '-c', 'Release', '--no-build', '--filter', $EffTestFilter) "Release tests failed"
    Write-Ok "Release build + tests passed"
}

# ─── Step: sync ──────────────────────────────────────────────────────────────
function Step-GitSync {
    Assert-Cmd git @('fetch', '--prune') "git fetch failed"
    $script:CurrentBranch = Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD')
    Write-Step "Branch: $($script:CurrentBranch)"
    $code = Invoke-Cmd git @('pull', '--rebase', 'origin', $script:CurrentBranch)
    if ($code -ne 0) { throw "git pull --rebase failed. Resolve conflicts and retry." }
    Write-Ok "Synced with origin/$($script:CurrentBranch)"
}

# ─── Step: version ───────────────────────────────────────────────────────────
function Step-ComputeVersion {
    if (-not $script:CurrentBranch) {
        $script:CurrentBranch = Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD')
    }
    $allTags = Get-CmdOutput git @('tag', '--list')
    $latest = $allTags -split "`n" |
        Where-Object { $_ -match '^v?(\d+)\.(\d+)\.(\d+)$' } |
        Sort-Object { [Version]"$($_ -replace '^v','')" } |
        Select-Object -Last 1

    if ($latest -and $latest -match '^v?(\d+)\.(\d+)\.(\d+)$') {
        $maj = [int]$Matches[1]; $min = [int]$Matches[2]; $pat = [int]$Matches[3]
        $next = switch ($BumpType) {
            'major' { "v$($maj+1).0.0" }
            'minor' { "v$maj.$($min+1).0" }
            default { "v$maj.$min.$($pat+1)" }
        }
    } else {
        $latest = '(none)'; $next = 'v0.1.0'
    }
    $script:NextVersion = $next
    Write-Ok "Latest: $latest  →  Next ($BumpType bump): $next"
}

# ─── Step: changelog ─────────────────────────────────────────────────────────
function Step-UpdateChangelog {
    if (-not $script:NextVersion) { throw "Version not computed — run 'version' step first" }
    $path = Join-Path $RepoRoot 'CHANGELOG.md'
    if (-not (Test-Path $path)) { throw "CHANGELOG.md not found at $path" }

    $content = Get-Content $path -Raw
    $today   = Get-Date -Format 'yyyy-MM-dd'
    $verLine = "## [$($script:NextVersion)] - $today"

    # Insert a new versioned section immediately after ## [Unreleased]
    if ($content -match '(?m)^## \[Unreleased\]') {
        # Add blank line between [Unreleased] and the new versioned section
        $content = $content -replace '(?m)^(## \[Unreleased\])', "`$1`n`n$verLine"
    } else {
        Write-Warn "No [Unreleased] section found — prepending versioned section"
        $content = "## [Unreleased]`n`n$verLine`n`n" + $content
    }
    [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
    Write-Ok "CHANGELOG.md updated ($verLine)"
}

# ─── Step: push-changelog ────────────────────────────────────────────────────
function Step-CommitChangelog {
    if (-not $script:CurrentBranch) {
        $script:CurrentBranch = Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD')
    }
    Assert-Cmd git @('add', 'CHANGELOG.md')
    Assert-Cmd git @('commit', '-m', "chore: prepare release $($script:NextVersion)")
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping push"; return }
    Assert-Cmd git @('push', 'origin', $script:CurrentBranch) "git push failed"
    Write-Ok "Changelog committed and pushed"
}

# ─── Step: pr ────────────────────────────────────────────────────────────────
function Step-CreateMergePR {
    if ($IsNoGh -or -not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warn "NoGh / gh missing: skipping PR creation. Create and merge the PR manually, then re-run with -From tag."
        return
    }
    $branch = if ($script:CurrentBranch) { $script:CurrentBranch } else { Get-CmdOutput git @('rev-parse', '--abbrev-ref', 'HEAD') }
    Write-Step "Creating PR: $branch → main for $($script:NextVersion)"
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping PR create/merge"; return }

    Assert-Cmd gh @('pr', 'create', '--title', "Release $($script:NextVersion)", '--body', "Prepare release $($script:NextVersion)", '--base', 'main', '--head', $branch) "Failed to create PR"
    $prNum = Get-CmdOutput gh @('pr', 'view', '--json', 'number', '--jq', '.number')
    if (-not $prNum) { throw "Could not determine PR number" }
    Write-Ok "PR #$prNum created"

    # Wait for CI checks using gh pr checks (accurate per-check status)
    $timeoutSec = $WorkflowTimeoutMinutes * 60
    $interval = 15; $elapsed = 0
    Write-Step "Waiting for CI checks on PR #$prNum (timeout: $WorkflowTimeoutMinutes min)..."
    while ($true) {
        Start-Sleep -Seconds $interval; $elapsed += $interval
        if ($elapsed -gt $timeoutSec) { throw "Timeout ($WorkflowTimeoutMinutes min) waiting for PR CI checks" }

        $checksJson = Get-CmdOutput gh @('pr', 'checks', $prNum, '--json', 'state,name,__typename')
        if (-not $checksJson) { Write-Step "  No checks yet..."; continue }
        try   { $checks = $checksJson | ConvertFrom-Json }
        catch { Write-Step "  Waiting for checks to appear..."; continue }
        if (-not $checks -or $checks.Count -eq 0) { Write-Step "  No checks yet..."; continue }

        $failed  = @($checks | Where-Object { $_.state -in @('FAILURE','ERROR','CANCELLED','TIMED_OUT') })
        $pending = @($checks | Where-Object { $_.state -notin @('SUCCESS','SKIPPED','NEUTRAL','FAILURE','ERROR','CANCELLED','TIMED_OUT') })

        if ($failed.Count -gt 0) { throw "CI check(s) failed: $(($failed.name) -join ', ')" }
        if ($pending.Count -eq 0) { Write-Ok "All CI checks passed"; break }
        Write-Step "  Pending: $(($pending.name) -join ', ')..."
    }

    $slug = Get-RepoSlug
    Assert-Cmd gh @('pr', 'merge', $prNum, '--merge', '--delete-branch', '--repo', $slug) "Failed to merge PR #$prNum"
    Write-Ok "PR #$prNum merged into main"
}

# ─── Step: tag ───────────────────────────────────────────────────────────────
function Step-CreateTag {
    if (-not $script:NextVersion) { throw "Version not set — run 'version' step first" }
    Assert-Cmd git @('tag', '-a', $script:NextVersion, '-m', "Release $($script:NextVersion)") "Failed to create tag"
    if ($IsDryRun) { Write-Warn "DRYRUN: skipping tag push"; return }
    Assert-Cmd git @('push', 'origin', $script:NextVersion) "Failed to push tag"
    Write-Ok "Tag $($script:NextVersion) created and pushed"
}

# ─── Step: wait-workflows ────────────────────────────────────────────────────
function Step-WaitWorkflows {
    if ($IsNoGh -or -not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warn "gh not available: skipping workflow wait"
        return
    }
    $tag = $script:NextVersion
    $timeoutSec = $WorkflowTimeoutMinutes * 60
    $interval = 20; $elapsed = 0
    $startTime = [DateTime]::UtcNow.AddSeconds(-30)
    Write-Step "Waiting for workflows triggered by tag $tag (timeout: $WorkflowTimeoutMinutes min)..."

    while ($true) {
        Start-Sleep -Seconds $interval; $elapsed += $interval
        if ($elapsed -gt $timeoutSec) { throw "Timeout ($WorkflowTimeoutMinutes min) waiting for release workflows" }

        $runsJson = Get-CmdOutput gh @('run', 'list', '--branch', $tag, '--limit', '50', '--json', 'status,conclusion,name,startedAt')
        if (-not $runsJson) { Write-Step "  No runs detected yet for $tag..."; continue }
        try   { $runs = @($runsJson | ConvertFrom-Json | Where-Object { $_.startedAt -ge $startTime }) }
        catch { continue }
        if ($runs.Count -eq 0) { Write-Step "  Waiting for workflow runs to appear..."; continue }

        $failed     = @($runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -notin @('success','skipped','neutral') })
        $inProgress = @($runs | Where-Object { $_.status -ne 'completed' })

        if ($failed.Count -gt 0) { throw "Release workflow(s) failed: $(($failed.name) -join ', ')" }
        if ($inProgress.Count -eq 0) { Write-Ok "All release workflows succeeded"; break }
        Write-Step "  In progress: $(($inProgress.name) -join ', ')..."
    }
}

# ─── Helpers ─────────────────────────────────────────────────────────────────
function Get-RepoSlug {
    $url = Get-CmdOutput git @('remote', 'get-url', 'origin')
    if ($url -match 'github\.com[:/](.+?)/(.+?)(?:\.git)?$') { return "$($Matches[1])/$($Matches[2])" }
    throw "Could not parse GitHub repo slug from remote URL: $url"
}

# ─── Interactive step menu ────────────────────────────────────────────────────
function Show-StepMenu([string[]]$planned) {
    Write-Host "`nSteps planned to run:" -ForegroundColor Cyan
    $i = 1
    foreach ($s in $StepOrder) {
        $inPlan = $planned -icontains $s
        $marker = if ($inPlan) { '[x]' } else { '[ ]' }
        $color  = if ($inPlan) { 'White' } else { 'DarkGray' }
        Write-Host ("  {0,2}. {1} {2,-20} {3}" -f $i, $marker, $s, $StepDesc[$s]) -ForegroundColor $color
        $i++
    }
    Write-Host "`nEnter numbers of steps to SKIP (comma-separated), or press Enter to run all planned steps:" -ForegroundColor Yellow
    $input = Read-Host '  Skip'
    if ([string]::IsNullOrWhiteSpace($input)) { return $planned }

    $skipNums = $input -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ }
    $toSkip   = $skipNums | ForEach-Object { if ($_ -ge 1 -and $_ -le $StepOrder.Count) { $StepOrder[$_ - 1] } }
    return $planned | Where-Object { $toSkip -notcontains $_ }
}

# Interactive prompt when a step fails: Retry / Skip / Abort
function Prompt-OnFailure([string]$stepName) {
    if (-not $IsInteractive) { return 'abort' }
    Write-Host "`n  Step '$stepName' failed." -ForegroundColor Red
    Write-Host "  [R]etry  [S]kip  [A]bort (default)" -ForegroundColor Yellow
    $choice = (Read-Host '  Choice').Trim().ToLower()
    $action = switch ($choice) {
        'r' { 'retry' }
        's' { 'skip'  }
        default { 'abort' }
    }
    return $action
}

# ─── Step dispatch table ─────────────────────────────────────────────────────
$StepFns = @{
    'preflight'      = { Step-Preflight }
    'clean'          = { Step-CleanTree }
    'build-debug'    = { Step-BuildDebug }
    'build-release'  = { Step-BuildRelease }
    'sync'           = { Step-GitSync }
    'version'        = { Step-ComputeVersion }
    'changelog'      = { Step-UpdateChangelog }
    'push-changelog' = { Step-CommitChangelog }
    'pr'             = { Step-CreateMergePR }
    'tag'            = { Step-CreateTag }
    'wait-workflows' = { Step-WaitWorkflows }
}

# ─── Main ────────────────────────────────────────────────────────────────────
try {
    if ($IsDryRun) { Write-Warn "DRY RUN — no pushes, merges or tags will be made" }

    $stepsToRun = [string[]](Get-StepsToRun)

    # When running interactively with no explicit step selection, show the menu
    if ($IsInteractive -and -not $Only -and -not $From -and $SkipSet.Count -eq 0) {
        $stepsToRun = [string[]](Show-StepMenu $stepsToRun)
    } else {
        Write-Host "`nSteps to run: $($stepsToRun -join ' → ')" -ForegroundColor Cyan
    }

    if ($stepsToRun.Count -eq 0) { Write-Warn "No steps selected. Exiting."; exit 0 }

    $total = $stepsToRun.Count; $num = 0
    foreach ($step in $stepsToRun) {
        $num++
        Write-Header "[$num/$total] $step — $($StepDesc[$step])"

        $succeeded = $false
        while (-not $succeeded) {
            try {
                & $StepFns[$step]
                $succeeded = $true
            } catch {
                Write-Fail "Step '$step' failed: $_"
                $action = Prompt-OnFailure $step
                switch ($action) {
                    'retry' { Write-Warn "Retrying '$step'..." }
                    'skip'  { Write-Warn "Skipping '$step'"; $succeeded = $true }
                    default { throw "Aborted at step '$step': $_" }
                }
            }
        }
    }

    Write-Host "`n✓ Release $($script:NextVersion ?? '(version not computed)') complete." -ForegroundColor Green
}
catch {
    Write-Fail "RELEASE FAILED: $_"
    exit 1
}
finally {
    Pop-Location
}
