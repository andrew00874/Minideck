# MiniDeck Browser Link

Chrome에서 최초 한 번만 설치합니다.

1. `setup.ps1` 실행 (현재 Windows 사용자에게 로컬 연결 호스트 등록).
2. Chrome 주소창에 `chrome://extensions` 입력.
3. 오른쪽 위 **개발자 모드** 켜기.
4. **압축해제된 확장 프로그램을 로드합니다** → 이 `browser-extension` 폴더 선택.
5. MiniDeck 다이얼에서 웹사이트 선택.

확장 ID: `mecekhgbdppcabapefkdljicahcnibcf`

같은 origin(프로토콜·호스트·포트)의 일반 탭 중 마지막 사용 탭을 활성화합니다. 페이지 경로·쿼리·해시는 바꾸지 않으며, 새로고침하지 않습니다. 탭이 없으면 새로 엽니다. 최소화된 창도 복원합니다. 시크릿 탭은 대상에서 제외합니다. 서로 다른 서브도메인은 별도 사이트입니다.

사이트를 사용하는 Chrome 프로필 한 곳에만 설치하세요. 다른 프로필·브라우저의 탭은 검색하지 않습니다. 탭 목록 권한과 로컬 MiniDeck 연결 권한을 사용하며, 페이지 본문·쿠키·비밀번호에는 접근하지 않고 외부 서버로 데이터를 보내지 않습니다. 탭 URL 목록은 확장 안에서만 처리하고 MiniDeck에는 처리 결과만 반환합니다.

브라우저를 닫았거나 확장이 연결되지 않았으면 MiniDeck에 안내가 나타납니다. 이때 기존 URL 실행으로 우회하지 않아 중복 탭을 만들지 않습니다. 연결 복구는 확장 아이콘 클릭 또는 최대 30초 후 자동 재시도로 가능합니다.

삭제: Chrome에서 확장을 제거하고 `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.minideck.browser` 레지스트리 키를 제거하면 됩니다. 폴더를 이동했다면 setup.ps1을 다시 실행해야 합니다.

검증: `node test.cjs`. 브라우저 실제 통합 검증은 확장 설치 후 수행합니다.
