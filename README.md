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
| `Ctrl+Enter` | 정렬 |
| `Ctrl+Shift+V` | 클립보드 내용을 입력에 붙여넣기 |
| `Ctrl+Shift+C` | 정렬 결과 전체 복사 |
| `Ctrl+Alt+C` | 한 줄로 압축해서 복사 |

입력하면 0.3초 뒤 자동으로 정렬됩니다. 오류가 있으면 상태바에 줄/열이 표시되고, 클릭하면 해당 위치로 이동합니다.
