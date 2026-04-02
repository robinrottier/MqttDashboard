<#
PowerShell release helper for MqttDashboard

Usage: pwsh ./scripts/release.ps1

This script performs a patch release workflow from current local source code.
Steps (each depends on previous):
 - Ensure working tree is clean (auto-commit TODO.md if the only change)
 - Build (Debug) and run tests (Debug)
 - Build (Release) and run tests in Release (skips browser tests by default)
 - Pull remote and ensure in-sync
 - Compute next patch version (based on latest semver tag)
 - Update CHANGELOG.md under [Unreleased] with a "Preparing release vX.Y.Z" line and commit as "Next RC"
 - Push branch, create PR to main, wait for CI checks to pass, merge PR
 - Create annotated tag vX.Y.Z and push tag (this triggers tag-based CI like patch-release)
 - Optionally wait for release workflows to succeed (requires 'gh' CLI)

Notes:
 - The script prefers using the 'gh' GitHub CLI if available for PRs and workflow checks.
 - If gh is not available, the script will still perform local git operations but will not automate PR merging or Actions polling.
 - Requires pwsh and git and dotnet on PATH.

Environment variables:
 - GITHUB_TOKEN (optional) - used by gh if configured; gh login is recommended.
 - RELEASE_TEST_FILTER (optional) - test filter to use for Release config (default excludes Playwright by trait: "TestCategory!=Playwright").
 - SKIP_RELEASE_TESTS=1 - skip running tests for Release build.
 - DRYRUN=1 - perform a dry run (no pushes, no merges, no tags)
#>

Param(
    [switch]$Help,
    [switch]$DryRun,
    [switch]$NoGh,
    [switch]$SkipReleaseTests,
    [string]$ReleaseTestFilter,
    [switch]$Parallel,
    [int]$WorkflowTimeoutMinutes = 30,
    [switch]$verbose = $false
)

Set-StrictMode -Version Latest

function Show-Help {
    @"
Usage: pwsh ./scripts/release.ps1 [options]

Options:
  -Help                 Show this help text
  -DryRun               Do not push, merge or tag (dry run)
  -RequireGh            Fail if GitHub CLI (gh) is not available
  -SkipReleaseTests     Skip running tests for the Release build
  -ReleaseTestFilter    Test filter to use for Release config (overrides default)
  -Parallel             Run Debug and Release build/test stages in parallel
  -WorkflowTimeoutMinutes <n>  Timeout minutes to wait for workflows (default: 30)

Environment variables (fallbacks):
  GITHUB_TOKEN          used by gh if configured
  RELEASE_TEST_FILTER   alternative way to pass release test filter
  DRYRUN=1              alternative to -DryRun
  SKIP_RELEASE_TESTS=1  alternative to -SkipReleaseTests

Examples:
  pwsh ./scripts/release.ps1 -DryRun
  pwsh ./scripts/release.ps1 -RequireGh -ReleaseTestFilter 'Category!=Playwright' -Parallel
"@
}

# If PowerShell bound the -Help switch via Param(), handle it immediately to avoid running any commands.
if ($Help) { Show-Help; exit 0 }

# Quick check of PowerShell $args to intercept common help flags before any work starts
if ($args -and ($args -contains '--help' -or $args -contains '-h' -or $args -contains '/?' -or $args -contains '/h')) {
    Show-Help; exit 0
}

# Immediate raw-arg check to catch GNU-style flags passed to the script (e.g. --help)
# Check both $args and the raw command line for common help tokens so they never reach git/dotnet.
try {
    $rawArgs = [Environment]::GetCommandLineArgs()
    $cmdLine = if ($rawArgs) { $rawArgs -join ' ' } else { '' }
    if ($cmdLine -match '--help' -or $cmdLine -match '\s-h(\s|$)' -or $cmdLine -match '/\?' -or $cmdLine -match '/h') {
        Show-Help; exit 0
    }
} catch {
    # ignore errors reading raw args
}

# Note: do not clear `$args` here — we need it for GNU-style parsing below.
# Make the script compatible with either PowerShell Core (`pwsh`) or Windows PowerShell.
# Later we will clear `$args` after parsing to avoid forwarding leftover flags to
# the first external command.

function Parse-GnuArgs {
    if ($args -and $args.Count -gt 0) {
        foreach ($a in $args) {
            switch -Wildcard ($a) {
                '--help' { $Help = $true; break }
                '-h' { $Help = $true; break }
                '--dry-run' { $DryRun = $true; break }
                '--dryrun' { $DryRun = $true; break }
                '--no-gh' { $NoGh = $true; break }
                '--skip-release-tests' { $SkipReleaseTests = $true; break }
                '--parallel' { $Parallel = $true; break }
                '--release-test-filter=*' { $val = $a.Substring($a.IndexOf('=')+1); if ($val) { $ReleaseTestFilter = $val }; break }
                default { }
            }
        }
    }
}

## Decide which shell executable to use when launching subprocess shells.
## Prefer `pwsh` if installed; otherwise fall back to the current `powershell`.
try {
    $pwshCmd = Get-Command pwsh -ErrorAction SilentlyContinue
} catch { $pwshCmd = $null }
if ($pwshCmd) { $SHELL_EXE = $pwshCmd.Source } else {
    $psCmd = Get-Command powershell -ErrorAction SilentlyContinue
    if ($psCmd) { $SHELL_EXE = $psCmd.Source } else { throw "Neither pwsh nor powershell executable found on PATH." }
}

function Write-Log { param($msg) Write-Host "[release] $msg" }
function Exec($cmd, $failOnError = $true) {
    Write-Log "-> $cmd"
    $proc = Start-Process -FilePath $SHELL_EXE -ArgumentList ('-NoProfile','-NonInteractive','-Command',$cmd) -Wait -NoNewWindow -PassThru -RedirectStandardOutput -RedirectStandardError
    $out = $proc.StandardOutput.ReadToEnd()
    $err = $proc.StandardError.ReadToEnd()
    if ($verbose && $out) { Write-Host $out }
    if ($verbose && $err) { Write-Host $err }
    if ($failOnError -and $proc.ExitCode -ne 0) { throw "Command failed: $cmd (exit $($proc.ExitCode))" }
    return $proc.ExitCode
}

function Preflight-Checks {
    Write-Log "Running preflight checks..."
    $required = @('git','dotnet')
    foreach ($r in $required) {
        if (-not (Get-Command $r -ErrorAction SilentlyContinue)) {
            throw "$r not found on PATH. Please install and ensure it's on PATH."
        }
    }

    # By default require gh CLI to enable PR automation and workflow polling
    # Use --no-gh or -NoGh to opt out for environments without gh.
    $ghCmd = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $NoGh -and $env:NO_GH -ne '1') {
        if (-not $ghCmd) {
            throw "gh CLI not found but required. Install https://cli.github.com/ and authenticate, or run with --no-gh to skip GH automation."
        }
    }
    else {
        if (-not $ghCmd) {
            Write-Log "gh CLI not found — running in no-gh mode (PR automation and workflow polling disabled)."
        }
    }

    # Ensure git remote origin exists. Some git installations behave
    # differently when invoked non-interactively, so try a couple of variants.
    $remote = (Run-LocalCommand git 'remote get-url origin' $false).StdOut.Trim()
    if (-not $remote) {
        Write-Log "Primary git remote query returned empty — trying fallback 'git config --get remote.origin.url'"
        $remote = (Run-LocalCommand git 'config --get remote.origin.url' $false).StdOut.Trim()
    }
    if (-not $remote) { throw "git remote 'origin' not configured or could not be detected. Add an origin remote and retry." }
}


function Run-LocalCommand([string]$exe, [string]$cmdArgs, $failOnError=$true) {
    Write-Log "-> $exe $cmdArgs"

    # Try to resolve the command to an actual application executable. If the
    # command is an alias or function, invoke it via the chosen shell so that
    # shell-level resolution (and PATH) behaves the same as an interactive run.
    $cmdInfo = Get-Command $exe -ErrorAction SilentlyContinue
    if ($cmdInfo -and $cmdInfo.CommandType -eq 'Application' -and $cmdInfo.Source) {
        $fileName = $cmdInfo.Source
        $arguments = $cmdArgs
        $useShell = $false
    }
    else {
        # Fallback: run through the detected shell so aliases/functions work
        $fileName = $SHELL_EXE
        # Build a single command string: exe + args
        $escapedArgs = $cmdArgs -replace '"', '`"'
        # Use PowerShell backtick-escaped quotes so the inner command is passed as a
        # single quoted argument to the shell executable.
        $arguments = "-NoProfile -NonInteractive -Command `"$exe $escapedArgs`""
        $useShell = $true
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $fileName
    $psi.Arguments = $arguments
    $psi.WorkingDirectory = Get-Location
    Write-Log "Executing: $fileName $arguments"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)
    $stdout = $p.StandardOutput.ReadToEnd()
    $stderr = $p.StandardError.ReadToEnd()
    $p.WaitForExit()

    # If running the resolved application produced no output but succeeded,
    # some environments use wrapper scripts or shims that behave differently
    # when invoked directly. Retry via the shell fallback in that case.
    if (-not $useShell -and $p.ExitCode -eq 0 -and ([string]::IsNullOrEmpty($stdout)) -and ([string]::IsNullOrEmpty($stderr))) {
        Write-Log "No output from direct invocation; retrying via shell fallback"
        $fileName = $SHELL_EXE
        $escapedArgs = $cmdArgs -replace '"', '`"'
        $arguments = "-NoProfile -NonInteractive -Command `"$exe $escapedArgs`""
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $fileName
        $psi.Arguments = $arguments
        $psi.WorkingDirectory = Get-Location
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.UseShellExecute = $false
        $p = [System.Diagnostics.Process]::Start($psi)
        $stdout = $p.StandardOutput.ReadToEnd()
        $stderr = $p.StandardError.ReadToEnd()
        $p.WaitForExit()
    }
    if ($verbose -and $stdout) { Write-Host $stdout }
    if ($verbose -and $stderr) { Write-Host $stderr }
    if ($failOnError -and $p.ExitCode -ne 0) { throw "$exe exited with code $($p.ExitCode)\n$stderr" }
    return @{ ExitCode = $p.ExitCode; StdOut = $stdout; StdErr = $stderr }
}

function Ensure-CleanWorkingTree {
    Write-Log "Checking git status..."
    $status = (Run-LocalCommand git "status --porcelain" $false).StdOut.Trim()
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        $lines = $status -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
        if ($lines.GetType().Name -eq "String" -and $lines -eq "M TODO.md" `
        -or $lines.GetType().Name -eq "String[]" -and $lines.Count -eq 1 -and $lines[0] -match 'TODO.md') {
            Write-Log "Only TODO.md modified: auto-committing"
            Run-LocalCommand git 'add TODO.md'
            Run-LocalCommand git 'commit -m "chore: update TODO"'
        }
        else {
            throw "Working tree is dirty. Commit or stash changes before releasing.`n$status"
        }
    }
    else { Write-Log "Working tree clean." }
}

function Dotnet-Build-And-Test {
    param(
        [string]$Configuration = 'Debug'
    )
    Write-Log "Building solution ($Configuration)..."
    $res = Run-LocalCommand dotnet "build MqttDashboard.slnx -c $Configuration"
    if ($res.ExitCode -ne 0) { throw "Build failed for configuration $Configuration" }

    # Run tests
    if ($Configuration -eq 'Release' -and $env:SKIP_RELEASE_TESTS -eq '1') {
        Write-Log "Skipping tests for Release build (SKIP_RELEASE_TESTS=1)"
        return
    }

    $testFilter = $env:RELEASE_TEST_FILTER
    if (-not $testFilter -and $Configuration -eq 'Release') {
        # Default: try to exclude browser tests that may require Playwright / browser env
        $testFilter = 'TestCategory!=Playwright'
    }

    Write-Log "Running tests ($Configuration) ..."
    $testArgs = "test MqttDashboard.slnx -c $Configuration --no-build"
    if ($testFilter) { $testArgs += ' --filter "' + $testFilter + '"' }
    $res = Run-LocalCommand dotnet $testArgs
    if ($res.ExitCode -ne 0) { throw "Tests failed for configuration $Configuration" }
}

function Git-Pull-EnsureSync {
    Write-Log "Fetching remote..."
    $res =Run-LocalCommand git 'fetch --prune'
    if ($res.ExitCode -ne 0) { throw "git fetch failed" }

    $branch = (Run-LocalCommand git 'rev-parse --abbrev-ref HEAD').StdOut.Trim()
    Write-Log "Current branch: $branch"
    Write-Log "Pulling latest from origin/$branch (rebase)..."
    $exit = Run-LocalCommand git "pull --rebase origin $branch" $false
    if ($exit.ExitCode -ne 0) {
        throw "git pull failed or there are merge conflicts. Resolve them and retry."
    }
}

function Get-RepoSlug {
    $url = (Run-LocalCommand git 'remote get-url origin').StdOut.Trim()
    # supports https://github.com/owner/repo.git or git@github.com:owner/repo.git
    if ($url -match 'github.com[:/](.+?)/(.+?)(?:\.git)?$') { return "$($matches[1])/$($matches[2])" }
    return $null
}

function Get-LatestSemverTag {
    # list tags, filter semver-like, pick highest
    $tagsOut = (Run-LocalCommand git 'tag --list').StdOut -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
    $semverTags = @()
    foreach ($t in $tagsOut) {
        if ($t -match 'v?(\d+)\.(\d+)\.(\d+)$') {
            $major = [int]$matches[1]; $minor = [int]$matches[2]; $patch = [int]$matches[3]
            $semverTags += [PSCustomObject]@{ Tag = $t; Major=$major; Minor=$minor; Patch=$patch }
        }
    }
    if ($semverTags.Count -eq 0) { return $null }
    $sorted = $semverTags | Sort-Object Major,Minor,Patch -Descending
    return $sorted[0].Tag
}

function Bump-PatchTag($tag) {
    if (-not $tag) { return 'v0.0.1' }
    if ($tag -match '^v?(\d+)\.(\d+)\.(\d+)$') {
        $major=[int]$matches[1]; $minor=[int]$matches[2]; $patch=[int]$matches[3]
        $patch++
        return "v$major.$minor.$patch"
    }
    throw "Unable to parse tag: $tag"
}

function Update-ChangeLog($newTag) {
    $changelog = 'CHANGELOG.md'
    if (-not (Test-Path $changelog)) { throw "$changelog not found" }
    $lines = Get-Content $changelog
    $idx = $lines.IndexOf(($lines | where-object { $_ -match '^#+\s*\[Unreleased\]' }))
    if ($idx -lt 0) { # try alternate header
        $idx = $lines.IndexOf(($lines | where-object { $_ -match '^#\s*Unreleased' }))
    }
    $today = (Get-Date).ToString('yyyy-MM-dd')
    $entry = "- Preparing release $newTag ($today)"
    if ($idx -ge 0) {
        $insertAt = $idx + 1
        $newLines = $lines[0..($insertAt-1)] + @($entry) + $lines[$insertAt..($lines.Count-1)]
    } else {
        # Prepend an Unreleased section
        $newLines = @('# [Unreleased]','', $entry, '') + $lines
    }
    Set-Content -Path $changelog -Value $newLines -Encoding UTF8
}

function Commit-And-Push-ChangeLog {
    param([string]$branch)
    $res = Run-LocalCommand git 'add CHANGELOG.md'
    if ($res.ExitCode -ne 0) { throw "git add failed" }
    $res = Run-LocalCommand git 'commit -m "Next RC"'
    if ($res.ExitCode -ne 0) { throw "git commit failed" }
    if ($env:DRYRUN -eq '1') { Write-Log "DRYRUN: skipping push"; return }
    $res = Run-LocalCommand git "push origin $branch"
    if ($res.ExitCode -ne 0) { throw "git push failed" }
}

function Ensure-GH {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) { Write-Log "gh CLI not found. Install https://cli.github.com/ to enable PR automation and Actions polling."; return $false }
    return $true
}

function Create-And-Merge-PR($branch, $newTag) {
    if (-not (Ensure-GH)) { Write-Log "Skipping PR creation/merge (gh missing). You must create and merge a PR manually."; return }
    $slug = Get-RepoSlug
    if (-not $slug) { Write-Log "Could not determine repo slug from git remote"; return }

    Write-Log "Creating PR to main for branch $branch..."
    $prCreate = Run-LocalCommand gh "pr create --title ""Release $newTag"" --body ""Prepare release $newTag"" --base main --head $branch" $false
    if ($prCreate.ExitCode -ne 0) { throw "Failed to create PR" }

    # Get PR number
    $prJson = (Run-LocalCommand gh "pr view --json number --jq .number").StdOut.Trim()
    if (-not $prJson) { throw "Unable to determine PR number" }
    $prNumber = $prJson
    Write-Log "PR created: #$prNumber"

    # Wait for checks to succeed on the branch
    $timeout = 60 * 30 # 30 minutes
    $interval = 15
    $elapsed = 0
    Write-Log "Waiting for workflow checks (max $($timeout/60) minutes)..."
    while ($true) {
        Start-Sleep -Seconds $interval; $elapsed += $interval
        # Use gh to list recent runs for this branch and check if any are in progress or failed
        $runsOut = (Run-LocalCommand gh "run list --branch $branch --limit 50 --json status,conclusion,headBranch" $false).StdOut
        if (-not $runsOut) { Write-Log "No run info yet"; continue }
        # rough check: if any run has status in_progress or queued -> wait; if any failed -> abort; if at least one completed+success -> proceed
        $runs = $runsOut | ConvertFrom-Json
        if ($null -eq $runs -or $runs.Count -eq 0) { Write-Log "No workflow runs yet..."; if ($elapsed -gt $timeout) { throw "Timeout waiting for workflows" }; continue }
        $inProgress = $runs | Where-Object { $_.status -ne 'completed' }
        if ($inProgress.Count -gt 0) { Write-Log "Workflows still in progress..."; if ($elapsed -gt $timeout) { throw "Timeout waiting for workflows" }; continue }
        $failed = $runs | Where-Object { $_.conclusion -ne 'success' }
        if ($null -ne $failed -and $failed.Count -gt 0) { throw "One or more workflow runs failed. See Actions for details." }
        Write-Log "Workflows succeeded"
        break
    }

    Write-Log "Merging PR #$prNumber into main..."
    if ($env:DRYRUN -eq '1') { Write-Log "DRYRUN: skipping PR merge"; return }
    $res = Run-LocalCommand gh "pr merge $prNumber --merge --delete-branch --repo $slug --confirm"
    if ($res.ExitCode -ne 0) { throw "Failed to merge PR #$prNumber" }
}

function Create-Tag-And-Push($tag) {
    Write-Log "Creating annotated tag $tag"
    $res = Run-LocalCommand git "tag -a $tag -m \"Release $tag\""
    if ($res.ExitCode -ne 0) { throw "Failed to create tag $tag" }
    if ($env:DRYRUN -eq '1') { Write-Log "DRYRUN: skipping tag push"; return }
    $res = Run-LocalCommand git "push origin $tag"
    if ($res.ExitCode -ne 0) { throw "Failed to push tag $tag" }
}

function Wait-For-TagWorkflows($tag) {
    if (-not (Ensure-GH)) { Write-Log "gh CLI not found: cannot poll tag workflows"; return }
    Write-Log "Waiting for workflows triggered by tag $tag..."
    $timeout = 60 * 45; $interval = 20; $elapsed = 0
    while ($true) {
        Start-Sleep -Seconds $interval; $elapsed += $interval
        $runsOut = (Run-LocalCommand gh "run list --branch $tag --limit 50 --json status,conclusion,headBranch" $false).StdOut
        if (-not $runsOut) { Write-Log "No runs detected yet for tag $tag"; if ($elapsed -gt $timeout) { throw "Timeout waiting for tag workflows" }; continue }
        $runs = $runsOut | ConvertFrom-Json
        if ($runs.Count -eq 0) { Write-Log "No runs yet"; if ($elapsed -gt $timeout) { throw "Timeout waiting for tag workflows" }; continue }
        $inProgress = $runs | Where-Object { $_.status -ne 'completed' }
        if ($inProgress.Count -gt 0) { Write-Log "Tag workflows in progress..."; if ($elapsed -gt $timeout) { throw "Timeout waiting for tag workflows" }; continue }
        $failed = $runs | Where-Object { $_.conclusion -ne 'success' }
        if ($failed.Count -gt 0) { throw "One or more tag workflow runs failed. Check Actions." }
        Write-Log "All detected tag workflows succeeded"
        break
    }
}

# --- Main flow ---
try {
    cd $PSScriptRoot/.. | Out-Null
    Write-Log "Working directory: $(Get-Location)"

    # Parse any GNU-style args passed in (e.g. --help)
    Parse-GnuArgs

    # Clear automatic $args now that parsing is done to avoid forwarding any
    # leftover flags to the first external command invoked by the script.
    $args = @()

    if ($Help) { Show-Help; exit 0 }

    # Resolve effective flags from parameters and environment
    $DRYRUN = $DryRun.IsPresent -or ($env:DRYRUN -eq '1')
    $NO_GH = $NoGh.IsPresent -or ($env:NO_GH -eq '1')
    $SKIP_RELEASE_TESTS_FLAG = $SkipReleaseTests.IsPresent -or ($env:SKIP_RELEASE_TESTS -eq '1')
    if ($ReleaseTestFilter) { $GLOBAL:RELEASE_TEST_FILTER = $ReleaseTestFilter } elseif ($env:RELEASE_TEST_FILTER) { $GLOBAL:RELEASE_TEST_FILTER = $env:RELEASE_TEST_FILTER }

    Preflight-Checks

    Ensure-CleanWorkingTree

    if ($Parallel.IsPresent) {
        Write-Log "Running Debug and Release build/tests in parallel"
        $debugCmd = 'dotnet build MqttDashboard.slnx -c Debug; dotnet test MqttDashboard.slnx -c Debug --no-build'
        $releaseTestPart = ''
        if (-not $SKIP_RELEASE_TESTS_FLAG) {
            $filter = $GLOBAL:RELEASE_TEST_FILTER
            if (-not $filter -and $null -eq $filter) { $filter = 'TestCategory!=Playwright' }
            $releaseTestPart = '; dotnet test MqttDashboard.slnx -c Release --no-build --filter "' + $filter + '"'
        }
        $releaseCmd = "dotnet build MqttDashboard.slnx -c Release$releaseTestPart"

        $p1 = Start-Process -FilePath $SHELL_EXE -ArgumentList ('-NoProfile','-NonInteractive','-Command',$debugCmd) -PassThru
        $p2 = Start-Process -FilePath $SHELL_EXE -ArgumentList ('-NoProfile','-NonInteractive','-Command',$releaseCmd) -PassThru
        Wait-Process -Id $p1.Id, $p2.Id
        if ($p1.ExitCode -ne 0 -or $p2.ExitCode -ne 0) { throw 'One or more build/test processes failed' }
    }
    else {
        Dotnet-Build-And-Test -Configuration 'Debug'
        if ($SKIP_RELEASE_TESTS_FLAG) { Write-Log 'Skipping Release tests per flag' }
        Dotnet-Build-And-Test -Configuration 'Release'
    }

    Git-Pull-EnsureSync

    $latest = Get-LatestSemverTag
    Write-Log "Latest tag: $latest"
    $next = Bump-PatchTag $latest
    Write-Log "Next patch tag: $next"

    Update-ChangeLog $next

    $currentBranch = (Run-LocalCommand git 'rev-parse --abbrev-ref HEAD').StdOut.Trim()
    Commit-And-Push-ChangeLog -branch $currentBranch

    Create-And-Merge-PR $currentBranch $next

    Create-Tag-And-Push $next

    Wait-For-TagWorkflows $next

    Write-Log "Release succeeded: $next — ready to deploy"
}
catch {
    Write-Host "[release] ERROR: $_" -ForegroundColor Red
    exit 1
}
