# AutoPad - 세미나 발표 자료

---

## 📌 프로젝트 개요

**AutoPad**는 Windows 클립보드 모니터링 유틸리티로, 복사된 내용을 실시간으로 감지하여 토스트 알림과 **즉석 편집 기능**을 제공하는 데스크톱 애플리케이션입니다.

---

## 🎯 개발 배경 및 동기

### 문제 상황

웹사이트나 기술 문서에서 자주 마주치는 상황:

```bash
# Docker 실행 예시
docker run -d \
  --name <container-name> \
  -p <host-port>:8080 \
  -e DATABASE_URL=<your-database-url> \
  -e API_KEY=<your-api-key> \
  myimage:latest
```

```bash
# 설치 스크립트 예시
curl -sSL https://example.com/install.sh | bash -s -- \
  --username <your-username> \
  --token <your-token> \
  --region <your-region>
```

이처럼 `<placeholder>` 형태로 사용자 입력을 요구하는 복사 붙여넣기용 명령어들이 많습니다.

### 기존 워크플로우의 불편함

```
┌─────────────────────────────────────────────────────────────────┐
│  1. 웹에서 명령어 복사                                          │
│                    ↓                                            │
│  2. 텍스트 에디터(메모장, VSCode 등) 실행                       │
│                    ↓                                            │
│  3. 붙여넣기                                                    │
│                    ↓                                            │
│  4. <placeholder> 부분 찾아서 수정                              │
│                    ↓                                            │
│  5. 전체 선택 → 복사                                            │
│                    ↓                                            │
│  6. 터미널에 붙여넣기 실행                                      │
└─────────────────────────────────────────────────────────────────┘
          ⏱️ 번거롭고 시간 소요
```

### AutoPad 사용 시

```
┌─────────────────────────────────────────────────────────────────┐
│  1. 웹에서 명령어 복사 → 토스트 팝업 자동 표시                  │
│                    ↓                                            │
│  2. [편집] 버튼 클릭 → 즉석 편집 창                             │
│                    ↓                                            │
│  3. <placeholder> 부분 수정                                     │
│                    ↓                                            │
│  4. [복사 후 닫기] → 자동으로 클립보드에 저장                   │
│                    ↓                                            │
│  5. 터미널에 붙여넣기 실행                                      │
└─────────────────────────────────────────────────────────────────┘
          ⚡ 별도 에디터 없이 즉시 편집 & 재복사
```

### 핵심 Pain Point 해결

| Before | After (AutoPad) |
|--------|-----------------|
| 복사 → 에디터 열기 → 붙여넣기 → 편집 → 복사 → 에디터 닫기 | 복사 → 팝업에서 편집 버튼 → 편집 → 복사 후 닫기 |
| **6단계, 별도 앱 필요** | **3단계, 앱 내에서 완료** |

---

## 🛠️ 기술 스택

### 프레임워크 & 플랫폼

| 항목 | 기술 |
|------|------|
| **런타임** | .NET 8.0 |
| **UI 프레임워크** | WPF (Windows Presentation Foundation) |
| **보조 프레임워크** | WinForms (시스템 트레이 NotifyIcon) |
| **타겟 플랫폼** | Windows 10/11 (net8.0-windows) |

### 핵심 기술 요소

```
┌─────────────────────────────────────────────────────────────┐
│  Win32 Interop                                              │
│  ├─ AddClipboardFormatListener (클립보드 변경 감지)         │
│  └─ DwmSetWindowAttribute (다크 모드 타이틀바)              │
├─────────────────────────────────────────────────────────────┤
│  WPF 기능 활용                                              │
│  ├─ InkCanvas (이미지 드로잉/펜/지우개)                     │
│  ├─ BitmapEncoder (PNG/JPEG/BMP 저장)                       │
│  └─ DispatcherTimer (자동 닫기 타이머)                      │
├─────────────────────────────────────────────────────────────┤
│  시스템 통합                                                │
│  ├─ NotifyIcon (시스템 트레이)                              │
│  ├─ Registry (시작 프로그램 등록)                           │
│  └─ Mutex (단일 인스턴스 보장)                              │
└─────────────────────────────────────────────────────────────┘
```

### 주요 라이브러리 & API

- **System.Windows.Clipboard** - 클립보드 데이터 접근
- **System.Text.Encoding** - 다중 인코딩 지원 (UTF-8, EUC-KR)
- **CodePagesEncodingProvider** - 레거시 인코딩 지원
- **Microsoft.Win32.SaveFileDialog** - 파일 저장 대화상자

---

## ✨ 주요 기능

### 1. 클립보드 모니터링 & 즉석 편집
- 복사 즉시 토스트 팝업 → **[편집] 버튼으로 바로 수정 가능**
- 수정 후 **[복사 후 닫기]**로 클립보드에 즉시 반영
- Multi-line 명령어도 편하게 편집
- 복사된 텍스트의 **글자수(Rune)와 바이트 크기** 실시간 표시

### 2. 이미지 마킹 도구
- 스크린샷 복사 후 펜/지우개로 즉시 마킹
- 다양한 색상 팔레트 및 펜 굵기 조절
- 문서 작성 시 캡처 → 마킹 → 붙여넣기 원스톱

### 3. 파일 저장 & 인코딩
- 텍스트: TXT 파일로 저장
- 이미지: PNG/JPEG/BMP 형식 지원
- UTF-8 / EUC-KR 인코딩 변환 지원

### 4. 시스템 통합
- 시스템 트레이 상주 (백그라운드 동작)
- Windows 시작 시 자동 실행 옵션
- 단일 인스턴스 실행 보장
- 다크 테마 UI (타이틀바 포함)

---

## 🏗️ 아키텍처

```
AutoPad/
├── App.xaml.cs              # 애플리케이션 진입점, 전역 이벤트 처리
├── Services/
│   ├── ClipboardMonitor.cs  # Win32 클립보드 변경 감지
│   ├── SettingsService.cs   # JSON 설정 저장/로드
│   ├── IconHelper.cs        # 앱 아이콘 동적 생성
│   └── ThemeHelper.cs       # 다크 모드 DWM 적용
├── Models/
│   └── AppSettings.cs       # 설정 데이터 모델
└── Windows/
    ├── ToastWindow.xaml     # 토스트 팝업 (미리보기, 편집/저장 버튼)
    ├── EditWindow.xaml      # 편집 창 (텍스트/이미지)
    └── SettingsWindow.xaml  # 설정 창
```

---

## 📊 기대 효과

| 효과 | 설명 |
|------|------|
| **작업 흐름 간소화** | 복사 → 편집 → 재복사를 한 곳에서 해결 |
| **시간 절약** | 별도 에디터 실행 불필요 |
| **컨텍스트 유지** | 작업 중인 화면에서 벗어나지 않음 |
| **오류 감소** | 복사된 내용 즉시 확인으로 실수 방지 |

---

## 🔑 Use Case 예시

### 💡 Docker Compose 명령어 수정
1. 웹에서 docker-compose.yml 스니펫 복사
2. 토스트 [편집] → `<your-password>` 부분만 수정
3. [복사 후 닫기] → 터미널에 바로 붙여넣기

### 💡 API 호출 curl 명령어
1. 문서에서 curl 예제 복사
2. `<API_KEY>`, `<USER_ID>` 부분 수정
3. 바로 실행

### 💡 스크린샷 마킹
1. Win+Shift+S로 캡처
2. 토스트 [편집] → 중요 부분 빨간 펜으로 표시
3. Slack/Teams에 바로 붙여넣기

---

## 🔧 기술적 도전과 해결

| 도전 | 해결 방법 |
|------|-----------|
| WPF/WinForms 네임스페이스 충돌 | using alias 활용 (`WpfClipboard`, `WpfButton` 등) |
| 클립보드 변경 감지 | Win32 `AddClipboardFormatListener` API Interop |
| 다크 모드 타이틀바 | DWM `DWMWA_USE_IMMERSIVE_DARK_MODE` 속성 |
| 편집 창 복사 시 토스트 억제 | `SuppressClipboardMonitor` 플래그 + 타이머 |
| EUC-KR 인코딩 지원 | `CodePagesEncodingProvider` 등록 |

---

## 📈 향후 발전 방향

- 클립보드 히스토리 기능
- 클라우드 동기화
- 단축키 커스터마이징
- 다국어 지원
- 정규식 기반 placeholder 자동 감지 및 하이라이트

---

## 💡 결론

AutoPad는 **"복사한 내용을 편집하려면 별도 에디터가 필요하다"**는 일상적 불편함에서 출발했습니다.

특히 개발자/DevOps 환경에서 자주 접하는 **placeholder가 포함된 multi-line 명령어**를 빠르게 수정하고 재복사하는 워크플로우를 **원스톱으로 해결**합니다.

**.NET 8 + WPF** 기반의 현대적인 Windows 데스크톱 앱으로, Win32 API Interop을 활용한 시스템 통합과 다크 테마 UI로 사용자 경험을 극대화했습니다.
