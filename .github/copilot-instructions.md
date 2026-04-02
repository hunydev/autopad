# AutoPad — 프로젝트 컨텍스트

## 개요

AutoPad은 Windows 클립보드 모니터링 유틸리티다. 텍스트/이미지/파일 복사를 실시간 감지하여 토스트 알림을 표시하고, 즉석 편집 기능을 제공한다. Microsoft Store에 $0.99로 판매 중이며, 소스 코드는 MIT 라이선스.

- **개발자**: hunydev
- **현재 버전**: 1.1.0.0
- **GitHub**: https://github.com/hunydev/autopad
- **Store**: https://apps.microsoft.com/detail/autopad
- **Landing page**: https://hunydev.github.io/autopad/ (`docs/` 폴더가 GitHub Pages로 배포됨)

---

## 기술 스택

| 항목 | 내용 |
|------|------|
| Runtime | .NET 8.0 |
| UI | WPF (Windows Presentation Foundation) |
| System Tray | WinForms `NotifyIcon` |
| Target | `net8.0-windows10.0.19041.0` |
| Packaging | MSIX (x64 + arm64 bundle) |
| Icons | Segoe Fluent Icons |
| 다국어 | English (기본), Korean |

---

## 빌드 & 배포

```powershell
# 일반 빌드 (개발/테스트)
dotnet build AutoPad\AutoPad.csproj -c Release -r win-x64 --no-self-contained

# publish.bat — self-contained + single-file 배포
# 결과: publish\self-contained\AutoPad.exe, publish\single\AutoPad.exe
.\publish.bat

# build-msix.bat — MS Store용 MSIX 번들 (x64 + arm64)
# 결과: AutoPad\bin\AutoPad.msixbundle
.\build-msix.bat
```

- `publish.bat`: self-contained + framework-dependent 두 가지 single-file exe 생성
- `build-msix.bat`: x64/arm64 각각 publish 후 MSIX 패키징, 자체 서명(autopad-dev.pfx)
- Windows SDK 필요: `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64`

---

## 프로젝트 구조

```
autopad/                          ← 리포지토리 루트 (workspace root)
├── autopad.sln                   ← 솔루션 파일
├── AutoPad/                      ← 메인 프로젝트
│   ├── AutoPad.csproj
│   ├── Package.appxmanifest      ← MSIX 매니페스트 (버전, Identity)
│   ├── App.xaml / App.xaml.cs    ← 앱 진입점, 글로벌 클립보드 이벤트 라우팅
│   ├── MainWindow.xaml(.cs)      ← 숨겨진 창 (클립보드 리스너용 Win32 HWND)
│   ├── AssemblyInfo.cs
│   ├── Services/
│   │   ├── ClipboardMonitor.cs       ← Win32 AddClipboardFormatListener
│   │   ├── ClipboardHistoryService.cs ← 히스토리 JSON 저장/로드
│   │   ├── Localization.cs           ← 다국어 문자열 (en/ko), Loc 정적 클래스
│   │   ├── MacroService.cs           ← Jint JS 매크로 실행/프리셋
│   │   ├── SettingsService.cs        ← JSON 설정 파일 관리
│   │   ├── ThemeHelper.cs            ← DwmSetWindowAttribute 다크 타이틀바
│   │   └── IconHelper.cs             ← 동적 앱 아이콘 생성
│   ├── Models/
│   │   ├── AppSettings.cs            ← 설정 데이터 모델 + ToastPosition enum
│   │   ├── ClipboardHistoryItem.cs   ← 히스토리 아이템 모델
│   │   └── MacroItem.cs              ← 매크로 아이템 모델
│   ├── Windows/
│   │   ├── ToastWindow.xaml(.cs)     ← 토스트 알림 팝업
│   │   ├── EditWindow.xaml(.cs)      ← 텍스트/이미지 편집 창
│   │   ├── HistoryWindow.xaml(.cs)   ← 클립보드 히스토리 브라우저
│   │   ├── HtmlViewerWindow.xaml(.cs)← HTML 소스 뷰어
│   │   ├── SettingsWindow.xaml(.cs)  ← 설정 대화상자 (탭: 일반/알림/히스토리/매크로)
│   │   ├── MacroEditorWindow.xaml(.cs) ← 매크로 편집 모달
│   │   ├── MacroPresetWindow.xaml(.cs) ← 매크로 프리셋 선택 모달
│   │   └── StickyWindow.xaml(.cs)    ← 스티커 메모 (고정) 창
│   ├── Assets/                   ← MSIX 로고 이미지 (150x150, 310x310 등)
│   ├── Resources/                ← app.ico, icon.png
│   └── Tools/                    ← (비어있음)
├── docs/                         ← GitHub Pages 랜딩 페이지
│   ├── index.html                ← 메인 랜딩 페이지
│   ├── privacy/                  ← 개인정보 처리방침
│   ├── CNAME, _config.yml
│   └── favicon.svg, og-image.png 등
├── store/                        ← MS Store 리스팅 자료
│   ├── listing-en.md             ← 영문 스토어 설명
│   ├── listing-ko.md             ← 한국어 스토어 설명
│   ├── assets/                   ← 스토어 아이콘/배너
│   └── screenshots/              ← 스토어 스크린샷
├── publish/                      ← 빌드 출력 (gitignore됨)
├── publish.bat                   ← 배포 빌드 스크립트
├── build-msix.bat                ← MSIX 번들 빌드 스크립트
├── PRESENTATION.md               ← 세미나 발표 자료
├── README.md
└── LICENSE                       ← MIT
```

---

## 핵심 아키텍처

### 앱 생명주기 (App.xaml.cs)
1. `Application_Startup` → Mutex 단일 인스턴스 → 설정 로드 → 히스토리 로드
2. `MainWindow` (숨김) 생성 → Win32 HWND로 클립보드 리스너 등록
3. `SetupNotifyIcon()` → WinForms 트레이 아이콘 + 컨텍스트 메뉴
4. `OnClipboardChanged` → 모니터링/설정 일시중단/편집 억제 체크 → `ToastWindow` 표시
5. 설정 창 열림 시 `_isSettingsPaused = true` → 토스트 동작 중단 → 닫으면 복원

### 클립보드 모니터링 (ClipboardMonitor.cs)
- `AddClipboardFormatListener` / `WM_CLIPBOARDUPDATE` 사용
- 감지 순서: Image → Text (+ HTML) → File
- 각 타입별 개별 try-catch (하나 실패해도 나머지 진행)
- `IsImageMonitoringEnabled`, `IsFileMonitoringEnabled`, `MaxFileSizeBytes` 프로퍼티

### 토스트 (ToastWindow.xaml.cs)
- 3개 일반 생성자: 텍스트, 이미지, 파일
- 1개 미리보기 생성자: `internal ToastWindow(position, isCompact, opacityPercent)` — 버튼 비활성화
- `_targetOpacity`: 설정 투명도 반영, 마우스 hover 시 1.0으로 애니메이션
- `DoubleAnimation` 으로 fade-in/fade-out
- `ApplyCompactMode()`: 메타 정보 숨김 + 버튼 축소
- `UpdatePreviewOpacity()`: 실시간 투명도 업데이트

### 설정 (SettingsWindow.xaml.cs)
- RadioButton 기반 탭 UI (일반/알림/히스토리)
- 알림 탭 진입 시 미리보기 토스트 표시, 위치/컴팩트/투명도 실시간 반영
- 다른 탭 이동/저장/닫기 시 미리보기 토스트 제거
- `DarkComboBox`, `DarkCheckBox`, `TabBtn` 커스텀 ControlTemplate

### 히스토리 (HistoryWindow.xaml.cs)
- `ClipboardHistoryService` 에서 JSON 로드/저장
- 항목 클릭 시 `ContextMenu` (DkContextMenu/DkMenuItem 스타일) → 복사/편집 선택
- 검색, 모두 삭제 기능
- `EditWindow.SuppressClipboardMonitor` 플래그로 재귀 감지 방지

### 다국어 (Localization.cs)
- `Loc` 정적 클래스, `Get(en, ko)` 패턴
- `Loc.Language` 프로퍼티로 런타임 전환 (재시작 시 반영)

---

## 설정 항목 (AppSettings.cs)

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---------|------|--------|------|
| ToastPosition | enum | BottomRight | 토스트 위치 (6종) |
| ToastDurationSeconds | int | 5 | 표시 시간 |
| IsMonitoringEnabled | bool | true | 클립보드 모니터링 |
| IsImageMonitoringEnabled | bool | true | 이미지 모니터링 |
| IsFileMonitoringEnabled | bool | true | 파일 모니터링 |
| FileMonitoringMaxSizeMB | int | 10 | 파일 크기 제한 |
| IsCompactMode | bool | false | 컴팩트 토스트 |
| ToastOpacityPercent | int | 100 | 투명도 (10~100) |
| IsHistoryEnabled | bool | true | 히스토리 |
| HistoryMaxItems | int | 50 | 히스토리 최대 개수 |
| Language | string | "en" | 언어 |
| StartWithWindows | bool | false | 자동 시작 |
| StartMinimized | bool | true | 최소화 시작 |
| IsDarkMode | bool | true | 다크 모드 |
| Macros | List<MacroItem> | [] | JS 매크로 목록 |
| IsSpellCheckEnabled | bool | false | 맞춤법 검사 |

---

## 버전 변경 시 수정 파일

버전을 올릴 때 아래 두 파일의 버전을 동시에 변경해야 한다:
1. `AutoPad/AutoPad.csproj` → `<Version>` 태그
2. `AutoPad/Package.appxmanifest` → `<Identity Version="...">`

---

## 버전 히스토리

### v1.0.4.0 (초기 Store 출시)
- 기본 클립보드 모니터링 (텍스트/이미지/파일)
- 토스트 알림 + 편집기
- 이미지 마크업, HTML 소스 뷰어
- 경로/URL 감지
- 다크 테마, 영/한 다국어

### v1.0.5.0
- 텍스트 편집 도구 (공백 라인 제거, 줄바꿈 제거, 특수공백 제거, TRIM, 숫자 마스킹)
- URL hover → "URL 열기" 버튼 (편집기 + 토스트)
- 실시간 글자 수/바이트/라인 수 표시
- 설정에 앱 버전 표시
- ARM64 지원
- 파일 모니터링 설정 즉시 적용
- 앱 아이콘 통일

### v1.0.6.0
- 클립보드 히스토리 (검색, 재복사, 편집)
- 토스트 투명도 조절 (10~100%, hover 시 선명)
- 컴팩트 토스트 모드
- 탭 기반 설정 UI (일반/알림/히스토리)
- 설정 > 알림 탭에서 실시간 미리보기 토스트
- 이미지 모니터링 독립 토글
- 설정 중 클립보드 모니터링 자동 일시 중단
- 히스토리 항목 컨텍스트 메뉴 (복사/편집)
- DarkCheckBox disabled 스타일 (opacity 0.4)

### v1.1.0.0 (현재)
- Jint 기반 JavaScript 매크로 시스템 (샌드박스 실행)
- 28개 내장 매크로 프리셋 (마스킹, JSON, Base64, 정렬, 추출 등)
- 매크로 편집기 (테스트 실행, 스크립트 검증)
- 매크로 관리 (추가/편집/삭제/순서변경/프리셋)
- 스티커 메모 (고정) 기능 — 편집 창에서 Pin 버튼으로 내용 고정
- 스티커: Always on top 토글, 편집 전환, 이미지 Fit 토글, 리사이즈
- 이미지 Base64 복사 (전체/선택 영역 → data URI)
- 특수 공백 제거를 드롭다운 메뉴로 분리 (삭제/공백 치환)
- 텍스트 편집기 맞춤법 검사 옵션 (설정 > 일반)
- 설정 매크로 탭 추가 (4탭 구성: 일반/알림/히스토리/매크로)
- 바이너리 파일 감지 — 편집 시도 시 null byte 검사 후 경고 표시, "텍스트로 보기" 버튼으로 강제 로드
- 설정 매크로 탭 스크롤 수정 — 매크로 목록만 스크롤되도록 개선

---

## 코딩 컨벤션

- XAML 스타일은 `Window.Resources`에 정의 (전역 스타일 없음)
- 다국어 문자열은 모두 `Loc.XXX`로 접근 (`Localization.cs`에 집중)
- WPF/WinForms 타입 충돌 시 `using WpfXxx = System.Windows.XXX` alias 사용
- 설정 파일: `%LOCALAPPDATA%\AutoPad\settings.json`
- 히스토리 파일: `%LOCALAPPDATA%\AutoPad\history.json`
- 이미지 히스토리: `%LOCALAPPDATA%\AutoPad\history_images\`
