<#
.SYNOPSIS
  JsonPretty.exe(자체 포함 단일 파일)를 새로 만들어 프로젝트 루트에 두고 커밋한다.
  저장소에는 항상 마지막 exe 하나만 남도록, 이전 커밋들에 들어 있던 예전 exe는 히스토리에서 지운다.

.EXAMPLE
  .\release.ps1 -Message "다크 테마 추가" -Push

.EXAMPLE
  .\release.ps1 -Message "새 기능" -Version 1.1.0 -Push -GitHubRelease
  버전을 1.1.0으로 올려 빌드하고, push 후 GitHub Releases에 v1.1.0 으로 exe를 올린다.

.NOTES
  - GitHub Release는 GitHub CLI(gh)로 만든다. 처음 한 번 'gh auth login' 으로 로그인해야 한다.
  - Releases에도 항상 마지막 버전 하나만 남긴다. 이전 릴리스와 그 태그를 지운 뒤 새로 만든다
    (예전 태그가 남아 있으면 예전 exe가 든 커밋도 저장소에 계속 남기 때문).
  - 예전 exe를 히스토리에서 지우면 커밋 해시가 바뀌므로 강제 push(--force-with-lease)를 한다.
    다른 PC에 받아 둔 저장소는 새로 받아야 한다.
  - 서버(Gitea)에서 예전 exe가 실제로 사라지는 시점은 서버의 저장소 정리(gc) 주기에 따른다.
  - 커밋은 선형 히스토리(병합 커밋 없음)를 전제로 한다.
#>
param(
    [Parameter(Mandatory = $true)][string]$Message,
    [switch]$Push,
    # 빌드·테스트 없이 루트에 이미 있는 JsonPretty.exe 로 커밋만 한다
    [switch]$SkipBuild,
    # 앱 버전(예: 1.1.0). 주면 csproj의 <Version>을 바꾼 뒤 빌드한다. 안 주면 지금 버전 그대로.
    [string]$Version,
    # push 후 GitHub Releases에 v버전 으로 exe를 올린다 (gh 필요)
    [switch]$GitHubRelease
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$exeName = 'JsonPretty.exe'
$csproj = Join-Path $PSScriptRoot 'src\JsonPretty.App\JsonPretty.App.csproj'

if ($GitHubRelease) {
    if (-not $Push) { throw '-GitHubRelease 는 -Push 와 함께 써야 합니다 (릴리스 태그가 push된 커밋을 가리켜야 함).' }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI(gh)가 없습니다. winget install GitHub.cli 후 gh auth login 하세요.' }
    & gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { throw 'gh 로그인이 필요합니다. gh auth login 을 먼저 실행하세요.' }
}

# 버전 지정 시 csproj 갱신
$projXml = [IO.File]::ReadAllText($csproj)
if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "버전 형식은 1.2.3 이어야 합니다: $Version" }
    $projXml = [regex]::Replace($projXml, '<Version>[^<]*</Version>', "<Version>$Version</Version>")
    [IO.File]::WriteAllText($csproj, $projXml, (New-Object Text.UTF8Encoding $false))
}
$appVersion = [regex]::Match($projXml, '<Version>([^<]*)</Version>').Groups[1].Value
if (-not $appVersion) { throw 'csproj에서 <Version>을 찾지 못했습니다.' }
Write-Host "버전: $appVersion"

function Invoke-Git {
    # git은 줄바꿈 경고 등을 stderr로 내보내는데, PowerShell 5.1은 이를 오류로 취급할 수 있다.
    # 결과(stdout)만 돌려주고 stderr는 화면에만 보여 준 뒤, 성공 여부는 종료 코드로 판단한다.
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & git @args 2>&1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.ErrorRecord]) {
                $text = $_.ToString()
                if ($text -notmatch 'LF will be replaced') { Write-Host $text -ForegroundColor DarkGray }
            }
            else { $_ }
        }
        $code = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $prev }
    if ($code -ne 0) { throw "git $($args -join ' ') 실패" }
    return $out
}

if (-not $SkipBuild) {
    # 1) 테스트
    dotnet test --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw '테스트 실패: 배포를 중단합니다.' }

    # 2) 자체 포함 단일 exe 만들기 (.NET 설치 없이 실행, 압축)
    $publishDir = Join-Path $PSScriptRoot 'publish'
    dotnet publish src/JsonPretty.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $publishDir --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'publish 실패' }
    Copy-Item (Join-Path $publishDir $exeName) (Join-Path $PSScriptRoot $exeName) -Force
}
$size = (Get-Item $exeName).Length / 1MB
Write-Host ("{0} 생성: {1:N1} MB" -f $exeName, $size)

# 3) 커밋 (메시지는 BOM 없는 UTF-8 파일로 넘겨 한글이 깨지지 않게 한다)
$msgFile = [IO.Path]::GetTempFileName()
[IO.File]::WriteAllText($msgFile, $Message, (New-Object Text.UTF8Encoding $false))
Invoke-Git add -A | Out-Null
Invoke-Git commit -q -F $msgFile | Out-Null
Remove-Item $msgFile

# 4) HEAD 이전 커밋들에서 예전 exe 지우기
$branch = (Invoke-Git rev-parse --abbrev-ref HEAD).Trim()
$oldHead = (Invoke-Git rev-parse HEAD).Trim()
$remoteHead = (& git rev-parse --verify -q "origin/$branch" 2>$null)
$commits = @(Invoke-Git rev-list --reverse --parents HEAD)

$needsRewrite = $false
if ($commits.Count -gt 1) {
    foreach ($line in $commits[0..($commits.Count - 2)]) {
        $c = ($line -split ' ')[0]
        if (& git ls-tree --name-only $c -- $exeName) { $needsRewrite = $true; break }
    }
}

if ($needsRewrite) {
    Write-Host '이전 커밋에서 예전 exe를 지우는 중...'
    $tmpIndex = Join-Path $env:TEMP ("jsonpretty-index-" + [guid]::NewGuid())
    $newParent = $null
    $saved = @{}
    foreach ($v in 'GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_AUTHOR_DATE','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL','GIT_COMMITTER_DATE','GIT_INDEX_FILE') {
        $saved[$v] = [Environment]::GetEnvironmentVariable($v)
    }
    try {
        for ($i = 0; $i -lt $commits.Count; $i++) {
            $parts = $commits[$i] -split ' '
            if ($parts.Count -gt 2) { throw '병합 커밋이 있어 자동으로 히스토리를 고칠 수 없습니다.' }
            $c = $parts[0]

            $tree = (Invoke-Git rev-parse "$c^{tree}").Trim()
            if ($i -lt $commits.Count - 1) {
                # 마지막(방금 만든) 커밋이 아니면 exe를 뺀 트리로 바꾼다
                $env:GIT_INDEX_FILE = $tmpIndex
                Invoke-Git read-tree $c | Out-Null
                Invoke-Git rm --cached -f -q --ignore-unmatch $exeName | Out-Null
                $tree = (Invoke-Git write-tree).Trim()
                Remove-Item Env:GIT_INDEX_FILE
            }

            $env:GIT_AUTHOR_NAME     = (Invoke-Git log -1 --format=%an $c)
            $env:GIT_AUTHOR_EMAIL    = (Invoke-Git log -1 --format=%ae $c)
            $env:GIT_AUTHOR_DATE     = (Invoke-Git log -1 --format=%aI $c)
            $env:GIT_COMMITTER_NAME  = (Invoke-Git log -1 --format=%cn $c)
            $env:GIT_COMMITTER_EMAIL = (Invoke-Git log -1 --format=%ce $c)
            $env:GIT_COMMITTER_DATE  = (Invoke-Git log -1 --format=%cI $c)

            $body = [IO.Path]::GetTempFileName()
            $raw = & git log -1 --format=%B $c
            [IO.File]::WriteAllText($body, ($raw -join "`n"), (New-Object Text.UTF8Encoding $false))
            if ($newParent) { $new = (Invoke-Git commit-tree $tree -p $newParent -F $body).Trim() }
            else            { $new = (Invoke-Git commit-tree $tree -F $body).Trim() }
            Remove-Item $body
            $newParent = $new
        }
    }
    finally {
        foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) }
        if (Test-Path $tmpIndex) { Remove-Item $tmpIndex -Force }
    }

    Invoke-Git update-ref "refs/heads/$branch" $newParent $oldHead | Out-Null
    # 로컬에 남은 예전 exe 데이터도 정리
    Invoke-Git reflog expire --expire=now --all | Out-Null
    Invoke-Git gc --prune=now --quiet | Out-Null
}

Invoke-Git log --oneline -5

# 5) push
if ($Push) {
    if ($needsRewrite -and $remoteHead) {
        Invoke-Git push --force-with-lease="${branch}:$($remoteHead.Trim())" origin $branch
    }
    else {
        Invoke-Git push origin $branch
    }
}

# 6) GitHub Release: 예전 릴리스·태그를 지우고 v버전 으로 새로 만들어 exe를 올린다
if ($GitHubRelease) {
    $originUrl = (Invoke-Git remote get-url origin).Trim()
    if ($originUrl -notmatch 'github\.com[:/](?<repo>[^/]+/[^/]+?)(\.git)?$') { throw "origin이 GitHub 저장소가 아닙니다: $originUrl" }
    $repo = $Matches['repo']
    $tag = "v$appVersion"

    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $existing = @(& gh release list --repo $repo --limit 100 --json tagName --jq '.[].tagName' 2>$null)
        foreach ($t in $existing) {
            if (-not $t) { continue }
            Write-Host "이전 릴리스 삭제: $t"
            & gh release delete $t --repo $repo --yes --cleanup-tag
            if ($LASTEXITCODE -ne 0) { throw "릴리스 $t 삭제 실패" }
        }
        # 릴리스 없이 태그만 남아 있는 경우도 정리
        & git ls-remote --exit-code --tags origin "refs/tags/$tag" *> $null
        if ($LASTEXITCODE -eq 0) { & git push origin ":refs/tags/$tag" 2>&1 | Out-Null }
        & git tag -d $tag 2>&1 | Out-Null

        $notes = [IO.Path]::GetTempFileName()
        $body = "$Message`n`n---`n**JsonPretty.exe** 를 내려받아 바로 실행하세요. Windows x64, .NET 설치 필요 없음 (자체 포함 단일 파일)."
        [IO.File]::WriteAllText($notes, $body, (New-Object Text.UTF8Encoding $false))
        & gh release create $tag $exeName --repo $repo --target $branch --title "JSON Pretty $tag" --notes-file $notes --latest
        $code = $LASTEXITCODE
        Remove-Item $notes
        if ($code -ne 0) { throw 'GitHub Release 생성 실패' }
    }
    finally { $ErrorActionPreference = $prev }

    Write-Host "GitHub Release: https://github.com/$repo/releases/tag/$tag"
}
