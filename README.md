# JSON Pretty

정돈되지 않은 JSON을 정렬해서 보여주고 복사할 수 있는 Windows 프로그램 (WPF / .NET 8). 설계는 [DESIGN.md](DESIGN.md) 참고.

## 실행

```powershell
dotnet run --project src/JsonPretty.App
```

## 테스트

```powershell
dotnet test
```

## 단일 exe 배포

```powershell
dotnet publish src/JsonPretty.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

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

출력 창은 객체/배열 단위로 접을 수 있습니다(줄 번호 옆 `⊟`). 오른쪽 위 "모두 접기 / 모두 펼치기" 또는 우클릭 메뉴를 사용하세요.
