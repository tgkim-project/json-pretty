# <img src="src/JsonPretty.App/Assets/app-256.png" width="40" align="top" /> JSON Pretty

[![Release](https://img.shields.io/github/v/release/tgkim-project/json-pretty?label=release)](https://github.com/tgkim-project/json-pretty/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/tgkim-project/json-pretty/total?label=downloads)](https://github.com/tgkim-project/json-pretty/releases)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6)
[![Last commit](https://img.shields.io/github/last-commit/tgkim-project/json-pretty)](https://github.com/tgkim-project/json-pretty/commits/main)
[![License: MIT](https://img.shields.io/github/license/tgkim-project/json-pretty)](LICENSE)

정돈되지 않은 JSON을 정렬해서 보여주고 복사할 수 있는 Windows 프로그램 (WPF / .NET 9). 설계는 [DESIGN.md](DESIGN.md) 참고.

## 바로 실행

[**Releases**](https://github.com/tgkim-project/json-pretty/releases/latest)에서 `JsonPretty.exe` 를 내려받아 실행하거나, 저장소 루트의 `JsonPretty.exe` 를 실행하면 됩니다.
.NET 설치가 필요 없는 자체 포함 단일 파일(win-x64)이며, 저장소와 Releases 모두 항상 마지막 버전 하나만 있습니다.

## 실행

```powershell
dotnet run --project src/JsonPretty.App
```

## 테스트

```powershell
dotnet test
```

## 배포 (exe 갱신)

빌드에는 .NET 9 SDK가 필요합니다.

```powershell
.\release.ps1 -Message "커밋 메시지" -Version 1.1.0 -Push -GitHubRelease
```

테스트 → 버전 변경 → 자체 포함 exe 생성 → 루트 `JsonPretty.exe` 교체 → 커밋 → push → GitHub Release(`v1.1.0`) 생성 순서로 진행합니다.
`-Version` 을 빼면 지금 버전 그대로, `-GitHubRelease` 를 빼면 Releases는 건드리지 않습니다.

- 저장소에 예전 exe가 쌓이지 않도록 **이전 커밋들에서 예전 exe를 히스토리에서 지우고 강제 push**합니다
  (커밋 해시가 바뀌므로 다른 PC의 저장소는 새로 받아야 합니다. 서버에서 실제 용량이 줄어드는 시점은 GitHub의 저장소 정리 주기에 따릅니다).
- Releases도 **이전 릴리스와 태그를 지우고** 최신 하나만 남깁니다.
- GitHub Release에는 [GitHub CLI](https://cli.github.com/)(`gh`)가 필요하며, 처음 한 번 `gh auth login` 으로 로그인합니다.

## 단축키

| 키 | 동작 |
|---|---|
| `Ctrl+O` | 파일 열기 |
| `Ctrl+S` | 정렬 결과를 파일로 저장 (BOM 없는 UTF-8) |
| `Ctrl+Enter` | 정렬 |
| `Ctrl+Shift+V` | 클립보드 내용을 입력에 붙여넣기 |
| `Ctrl+Shift+C` | 정렬 결과 전체 복사 |
| `Ctrl+Alt+C` | 한 줄로 압축해서 복사 |

입력하면 0.3초 뒤 자동으로 정렬됩니다. 오류가 있으면 입력 창의 해당 줄이 빨갛게 표시되고(오류 글자에 밑줄), 상태바의 줄/열 메시지를 클릭하면 그 위치로 이동합니다.

파일은 [열기] 버튼, 창에 끌어다 놓기, 또는 `JsonPretty.exe 파일.json` 처럼 실행 인자로 열 수 있습니다(100 MB 이하). 들여쓰기·키 정렬·창 위치/크기·좌우 비율·마지막 폴더는 `%APPDATA%\JsonPretty\settings.json`에 저장되어 다음 실행 때 복원됩니다.

**느슨한 입력**(기본 켬): 작은따옴표 문자열, 따옴표 없는 키, `True/False/None` 을 표준 JSON으로 보정합니다. 보정 후에도 남는 오류는 원본 기준 줄/열로 알려 줍니다. 로그에서 흔한 `"{\"a\":1}"` 처럼 문자열 안에 든 JSON은 자동으로 풉니다.

**트리 보기**: 출력 창 아래 [트리] 탭. 노드를 선택하면 경로(`$.items[2].name`)가 보이고, 경로 복사·값 복사를 할 수 있습니다(우클릭 메뉴도 동일).

**찾기**: 입력/출력 창에서 `Ctrl+F` (다음 `F3`, 이전 `Shift+F3`).

**클립보드 감지**(기본 켬): 다른 프로그램에서 JSON을 복사하고 돌아오면 붙여넣기를 제안합니다. 같은 내용은 한 번만 묻고, 이 프로그램에서 복사한 내용은 묻지 않습니다.

**테마**: 툴바에서 시스템 / 라이트 / 다크 선택. "시스템"은 Windows 앱 모드 설정을 따라가며, 설정이 바뀌면 바로 반영됩니다.

출력 창은 객체/배열 단위로 접을 수 있습니다(줄 번호 옆 `⊟`). 오른쪽 위 "모두 접기 / 모두 펼치기" 또는 우클릭 메뉴를 사용하세요.

## 라이선스

[MIT](LICENSE)
