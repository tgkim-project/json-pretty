# JSON Pretty — 설계 문서

정돈되지 않은 JSON을 붙여넣거나 불러오면 보기 좋게 정렬해서 보여주고, 결과를 바로 복사할 수 있는 Windows 데스크톱 프로그램.

---

## 1. 목표 / 범위

| 구분 | 내용 |
|---|---|
| 필수 (MVP) | JSON 입력 → 정렬(Pretty Print) 출력, 결과 복사 |
| 필수 (MVP) | 잘못된 JSON일 때 오류 위치(줄/열)와 메시지 표시 |
| 권장 (v1.1) | 아래 "6. 추가 기능 추천" 중 우선순위 상 항목 |
| 비목표 | JSON 스키마 편집기, 원격 API 호출 클라이언트 같은 대형 기능 |

---

## 2. 기술 스택

| 항목 | 선택 | 이유 |
|---|---|---|
| 언어/런타임 | C# / .NET 8 (LTS) | Windows 네이티브, 배포 간편 |
| UI | WPF | 데스크톱 UI 성숙도, 다크 테마/레이아웃 자유도 |
| 에디터 컴포넌트 | AvalonEdit (NuGet) | 줄 번호, 구문 강조, 코드 접기, 대용량 텍스트 처리 |
| JSON 처리 | System.Text.Json (`JsonDocument`, `Utf8JsonWriter`) | 기본 내장, 빠름, 오류 위치(LineNumber/BytePositionInLine) 제공 |
| 패턴 | MVVM (CommunityToolkit.Mvvm) | 뷰/로직 분리, 테스트 용이 |
| 배포 | `dotnet publish` 단일 실행파일 (self-contained, win-x64) | 설치 없이 exe 하나로 실행 |
| 테스트 | xUnit | 포맷터 코어 단위 테스트 |

> 대안: 더 가벼운 exe가 필요하면 WinForms + ScintillaNET, 웹 기술 선호 시 Tauri. 기본안은 WPF.

---

## 3. 화면 구성

```
┌───────────────────────────────────────────────────────────────┐
│ [열기] [붙여넣기] [정렬 ▶] [압축] [복사] [저장]  들여쓰기:[2 ▼] │  ← 툴바
├──────────────────────────────┬────────────────────────────────┤
│  입력 (원본 JSON)             │  출력 (정렬된 JSON)              │
│  - 붙여넣기/드래그앤드롭       │  - 구문 강조 + 줄 번호            │
│  - 편집 가능                  │  - 코드 접기(+/-)                │
│                              │  - 읽기 전용                     │
├──────────────────────────────┴────────────────────────────────┤
│ ✔ 유효한 JSON | 객체 12, 배열 3 | 4.2 KB | 줄 87  │ 복사됨!      │  ← 상태바
└───────────────────────────────────────────────────────────────┘
```

- 좌우 2단 분할 (GridSplitter로 비율 조절). 좁은 창에서는 상하 분할로 전환 옵션.
- 오류 시 입력 창에서 해당 줄 하이라이트 + 상태바에 `3행 15열: ',' 또는 '}' 가 필요합니다` 형태로 표시.
- 복사 성공 시 상태바에 1.5초간 "복사됨" 토스트.

---

## 4. 구조

```
JsonPretty/
├─ JsonPretty.sln
├─ src/
│  ├─ JsonPretty.Core/            # UI 무관 로직 (단위 테스트 대상)
│  │  ├─ JsonFormatter.cs         # Format / Minify / SortKeys
│  │  ├─ FormatOptions.cs         # 들여쓰기 크기, 탭/스페이스, 키 정렬 여부
│  │  ├─ FormatResult.cs          # 성공 여부, 결과 문자열, 오류(줄/열/메시지), 통계
│  │  └─ LenientParser.cs         # (v1.1) 느슨한 입력 보정
│  └─ JsonPretty.App/             # WPF
│     ├─ App.xaml
│     ├─ MainWindow.xaml
│     ├─ ViewModels/MainViewModel.cs
│     ├─ Services/ClipboardService.cs
│     ├─ Services/FileService.cs
│     ├─ Services/SettingsService.cs   # %APPDATA%\JsonPretty\settings.json
│     └─ Resources/Json.xshd           # AvalonEdit 구문 강조 정의
└─ tests/
   └─ JsonPretty.Core.Tests/
```

### 핵심 인터페이스

```csharp
public sealed record FormatOptions(
    int IndentSize = 2,
    bool UseTabs = false,
    bool SortKeys = false,
    bool EscapeNonAscii = false);   // false면 한글 그대로 출력

public sealed record FormatError(int Line, int Column, string Message);

public sealed record FormatResult(
    bool Success,
    string? Output,
    FormatError? Error,
    JsonStats? Stats);

public interface IJsonFormatter
{
    FormatResult Format(string input, FormatOptions options);
    FormatResult Minify(string input);
}
```

### 처리 흐름

1. 입력 변경 → 300ms 디바운스 → 백그라운드 스레드에서 `Format` 호출 (UI 멈춤 방지)
2. `JsonDocument.Parse` (옵션: `AllowTrailingCommas`, `CommentHandling.Skip`)
3. 성공 → `Utf8JsonWriter`로 재작성 (들여쓰기, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`으로 한글 유지) → 출력 창 갱신
4. 실패 → `JsonException.LineNumber / BytePositionInLine`을 1-based 줄/열로 변환 → 오류 표시, 출력 창은 마지막 성공 결과 유지
5. 키 정렬 옵션 시 객체 속성을 이름순으로 재귀 정렬해 씀

---

## 5. 복사 기능 상세

| 동작 | 단축키 | 설명 |
|---|---|---|
| 정렬 결과 전체 복사 | `Ctrl+Shift+C` / 툴바 [복사] | 출력 창 전체 |
| 압축(한 줄) 복사 | `Ctrl+Alt+C` | Minify 결과를 바로 복사 |
| 선택 영역 복사 | `Ctrl+C` | 에디터 기본 동작 |
| 경로 복사 | 우클릭 메뉴 | 커서 위치 노드의 경로 (`$.items[2].name`) |

- WPF `Clipboard.SetText`는 다른 프로그램이 클립보드를 잡고 있으면 `COMException(CLIPBRD_E_CANT_OPEN)`이 날 수 있으므로 `ClipboardService`에서 최대 5회, 50ms 간격 재시도.
- 클립보드 텍스트는 CRLF 줄바꿈으로 통일 (메모장 등 Windows 앱 호환).

---

## 6. 추가 기능 추천

우선순위: ★★★ 꼭 넣기 / ★★ 있으면 좋음 / ★ 여유 있을 때

| 우선 | 기능 | 설명 |
|---|---|---|
| ★★★ | 실시간 검증 + 오류 위치 이동 | 오류 메시지 클릭 시 해당 줄로 커서 이동 |
| ★★★ | 들여쓰기 옵션 | 2칸 / 4칸 / 탭 |
| ★★★ | 압축(Minify) | 정렬의 반대. API 요청 본문에 붙일 때 유용 |
| ★★★ | 파일 열기/저장 + 드래그앤드롭 | `.json` 파일을 창에 끌어다 놓기 |
| ★★★ | 구문 강조 + 코드 접기 | 키/문자열/숫자/불리언 색 구분, 객체 단위 접기 |
| ★★ | 키 정렬 | 알파벳순 정렬 → 두 JSON 비교할 때 편함 |
| ★★ | 트리 뷰 탭 | 노드를 펼쳐보는 트리, 노드 선택 시 경로/값 복사 |
| ★★ | 검색 / JSONPath 필터 | `Ctrl+F` 텍스트 검색, `$.items[*].id` 같은 경로 질의 |
| ★★ | 느슨한 입력 자동 보정 | 작은따옴표, 따옴표 없는 키, 주석, 끝 쉼표 허용 후 표준 JSON으로 변환 |
| ★★ | 이스케이프된 JSON 풀기 | `"{\"a\":1}"` 처럼 문자열 안에 든 JSON을 한 번 더 파싱 (로그에서 자주 나옴) |
| ★★ | 클립보드 자동 감지 | 창 활성화 시 클립보드에 JSON이 있으면 "붙여넣을까요?" 배너 |
| ★★ | 다크/라이트 테마 | 시스템 설정 따라가기 |
| ★★ | 설정/창 위치 기억 | 마지막 들여쓰기, 테마, 창 크기 저장 |
| ★ | 두 JSON 비교(Diff) | 키 정렬 후 좌우 차이 하이라이트 |
| ★ | 변환 | JSON → YAML / CSV(배열일 때) / C# 클래스 생성 |
| ★ | 히스토리 | 최근 정렬한 내용 N개 보관 (민감 데이터 주의 → 기본 끔) |
| ★ | 전역 단축키 | 다른 프로그램에서 JSON 복사 후 단축키 → 클립보드 내용을 바로 정렬해서 다시 클립보드로 |
| ★ | 대용량 파일 모드 | 수십 MB 이상은 강조/접기 끄고 스트리밍 포맷 |
| ★ | 탐색기 우클릭 메뉴 | ".json 파일 → JSON Pretty로 열기" |

---

## 7. 예외/엣지 케이스

- 빈 입력 → 오류 아님, 출력 비움
- BOM 포함 파일 → 읽을 때 제거
- 최상위가 값 하나(`123`, `"abc"`)인 경우도 유효 JSON으로 처리
- 매우 큰 숫자(`12345678901234567890`)나 `1.0` → 원문 그대로 보존 (double 변환 금지, `JsonElement.GetRawText` 사용)
- 중복 키 → 경고만 표시하고 그대로 출력
- 깊은 중첩 → `MaxDepth` 를 넉넉히 (예: 256) 설정, 초과 시 명확한 오류

---

## 8. 개발 단계

| 단계 | 내용 | 완료 기준 |
|---|---|---|
| 1 | Core: Format/Minify/오류 위치 + 테스트 | xUnit 통과 (정상/오류/한글/큰 숫자) |
| 2 | WPF 기본 화면: 입력/출력/정렬/복사 | MVP 동작 |
| 3 | AvalonEdit 적용: 구문 강조, 줄 번호, 접기, 오류 줄 하이라이트 | |
| 4 | 파일 열기/저장, 드래그앤드롭, 설정 저장 | |
| 5 | ★★ 기능 (키 정렬, 트리 뷰, 느슨한 입력, 테마 등) | |
| 6 | 단일 exe 배포 | `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` |
