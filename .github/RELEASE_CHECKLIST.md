# AutoPad — 릴리즈 전 체크리스트

> 이 체크리스트는 새 버전 릴리즈 전에 반드시 검토해야 할 항목이다.
> 요청 시 Copilot이 이 파일을 읽고 각 항목을 자동 검증한다.

---

## 1. 버전 동기화

- [ ] `AutoPad/AutoPad.csproj` → `<Version>` 태그가 릴리즈 버전과 일치하는지
- [ ] `AutoPad/Package.appxmanifest` → `<Identity Version="...">` 가 동일한지
- [ ] 두 파일의 버전이 서로 일치하는지

---

## 2. 다국어 문자열 검증 (Localization.cs)

- [ ] **en 문자열에 한국어가 섞여 있지 않은지** — `Get("...", "...")` 첫 번째 인자에 한글이 있으면 안 됨
- [ ] **ko 문자열이 누락되지 않았는지** — 두 번째 인자가 빈 문자열이거나 영어 그대로인 경우 의도적인지 검토
- [ ] **새로 추가된 문자열이 실제 UI에서 사용되는지** — dead code 체크
- [ ] **문자열 포맷 파라미터 불일치 없는지** — `{0}`, `$"..."` 등 en/ko 양쪽 동일한 파라미터 사용

---

## 3. 빌드 검증

- [ ] `dotnet build AutoPad\AutoPad.csproj -c Release -r win-x64 --no-self-contained` — 경고 0, 에러 0
- [ ] `dotnet build AutoPad\AutoPad.csproj -c Release -r win-arm64 --no-self-contained` — ARM64 빌드 성공
- [ ] `.\publish.bat` 실행 후 `publish\single\AutoPad.exe` 정상 실행 확인

---

## 4. MS Store 리스팅 업데이트 (`store/`)

- [ ] `store/listing-en.md` → **"이 버전의 새로운 기능" 섹션** 이 현재 버전의 변경사항을 반영하는지
- [ ] `store/listing-ko.md` → 동일 섹션 한국어 버전 업데이트
- [ ] **설명(Description)** 에 새 기능이 반영되어야 한다면 업데이트
- [ ] **제품 기능 목록** 에 새 기능 항목 추가 필요 여부 (최대 20개)
- [ ] en/ko 리스팅 간 기능 목록 개수와 순서 일치

---

## 5. 랜딩 페이지 업데이트 (`docs/index.html`)

- [ ] **Features 섹션** 에 새 기능이 반영되어야 하는지 (현재 12개 항목)
- [ ] 새 기능이 있다면 en/ko i18n 객체 양쪽에 추가
- [ ] **How it works** 섹션이 여전히 정확한지
- [ ] **Requirements** 섹션이 변경 사항 반영하는지
- [ ] 스크린샷 업데이트 필요 여부

---

## 6. README.md 업데이트

- [ ] **Features 목록** 에 새 기능 추가 필요 여부
- [ ] **Requirements** 섹션이 현재와 일치하는지
- [ ] 빌드/설치 지침이 여전히 유효한지

---

## 7. copilot-instructions.md 동기화

- [ ] `.github/copilot-instructions.md` → `현재 버전` 이 릴리즈 버전과 일치하는지
- [ ] **버전 히스토리** 섹션에 새 버전 항목 추가
- [ ] **설정 항목** 테이블에 새 설정이 반영되었는지
- [ ] **프로젝트 구조** 에 새 파일이 반영되었는지

---

## 8. 기능 검증 (수동)

- [ ] 새 기능이 en/ko 양쪽 언어에서 정상 동작
- [ ] 기존 기능 회귀 없음 (토스트, 편집, 히스토리, 설정)
- [ ] 시스템 트레이 아이콘 및 컨텍스트 메뉴 정상
- [ ] 설정 저장/로드 정상 (settings.json 호환성)
- [ ] MSIX 패키징 테스트 (`build-msix.bat`)

---

## 9. 코드 품질

- [ ] 사용되지 않는 `using` 문 없는지
- [ ] WPF/WinForms 타입 충돌이 alias로 해결되었는지
- [ ] 새로 추가된 Window 파일이 `.csproj`에 포함되는지 (WPF 자동 포함이지만 확인)
- [ ] `SuppressClipboardMonitor` 플래그가 적절히 설정/해제되는지

---

## 검증 자동화 안내

Copilot에게 아래와 같이 요청하면 자동 검증을 수행한다:

```
릴리즈 체크리스트 검토해줘
```

Copilot은 이 파일(`RELEASE_CHECKLIST.md`)을 읽고:
1. 버전 동기화를 코드에서 확인
2. Localization.cs에서 en/ko 문자열 이상 여부 스캔
3. 빌드 실행
4. store/, docs/, README.md 와 현재 기능 비교
5. copilot-instructions.md 버전/구조 확인
6. 결과를 체크리스트 형태로 리포트
