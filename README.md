# Agent Cat Screen Saver for Windows

Agent Cat의 로컬 사용량과 Herdr의 실시간 에이전트 상태를 한 화면에 보여주는 Windows 화면보호기입니다.
Codex 또는 Claude가 작업 중이거나 Herdr에 `working` 에이전트가 있으면 Agent Cat Windows 앱의
치즈냥이가 제자리에서 빠르게 질주합니다.

## 주요 기능

- `agentcat snapshot --json`과 동일한 로컬 스냅샷을 5초마다 읽습니다.
- Codex와 Claude의 오늘, 최근 7일, 최근 30일 토큰을 각각 표시합니다.
- 제공자가 실제 쿼터 비율을 노출할 때만 정확한 잔여 쿼터를 표시합니다.
- Herdr의 로컬 `api snapshot`을 2초마다 읽어 워크스페이스, 탭, 패널 및 에이전트 상태를 표시합니다.
- Herdr 상태 `working`, `blocked`, `done`, `idle`, `unknown`을 구분하고, 개입이 필요한 상태를 먼저 정렬합니다.
- `activity.countsByProvider`와 `motionStage`를 사용해 고양이 속도를 바꿉니다.
- Herdr에 작업 중인 에이전트가 있으면 고양이가 질주하고, `blocked` 또는 `done` 상태는 고양이 옆 배지로 알립니다.
- 실제 Windows 잠금 화면에는 같은 대시보드의 정적 스냅샷을 공식 최소 주기인 1분마다 새로 적용합니다.
- 화면보호기는 정적화하지 않고 기존처럼 약 30fps 연속 애니메이션으로 동작합니다.
- 키보드 또는 마우스 입력 시 종료됩니다.
- 화면보호기에서 복귀할 때 Windows 로그인을 요구하도록 설정합니다.
- 프롬프트, 응답, 터미널 본문, 소스 코드, 전체 작업 경로, 세션 ID 또는 OAuth 토큰을 표시하지 않습니다.

## 데이터가 동작하는 방식

이 화면보호기는 원격 PC를 직접 모니터링하지 않습니다. 각 PC가 자기 로컬 Agent Cat과 Herdr 데이터를 읽습니다.

```text
Codex / Claude Code
        ↓ 로컬 사용량·프로세스 메타데이터
Agent Cat connector (127.0.0.1:8765)
        ↓ /v1/snapshot
Agent Cat Screen Saver ← herdr api snapshot ← Herdr local socket
```

- Codex 합계: 로컬 `~/.codex/state_*.sqlite` 및 세션 사용량
- Claude 합계: 로컬 `~/.claude/stats-cache.json`, Claude Code 사용량 필드 및 Agent Cat hooks
- 실행 상태: `activity.countsByProvider.codex/claude`
- 정확한 쿼터: `providers.<provider>.limits.quotas[]`에 값이 있을 때만 표시
- Herdr 실행 상태: `agents[].agent_status`
- Herdr 화면 정보: `workspaces[]`, `tabs[]`, `panes[]`의 개수와 표시용 레이블
- Herdr 에이전트 행: 제공자 이름, 표시용 워크스페이스명, 포커스 여부, 상태만 표시

Claude를 사용하는 다른 PC에서도 Claude 사용량을 보려면 **그 PC에도** Agent Cat 커넥터와 이 화면보호기를 설치해야 합니다.

## 요구 사항

- Windows 10 또는 Windows 11
- [Agent Cat Windows 앱](https://agentcat.app/)이 설치되어 있거나 실행 가능해야 함
- Agent Cat local connector
- [Herdr](https://herdr.dev/)는 선택 사항이며, 설치되어 실행 중이면 자동으로 연동됨
- Windows에 기본 포함된 .NET Framework C# compiler
- 소스 코드를 내려받을 Git 또는 GitHub CLI

저장소는 Agent Cat 원본 이미지를 재배포하지 않습니다. `build.ps1`이 현재 PC에 설치된 Agent Cat 앱에서
`cute-cat-orange-sprite`를 로컬로 추출하여 실행 파일에 포함합니다. 추출된 PNG와 빌드 결과물은 `.gitignore` 대상입니다.

## 빠른 설치

PowerShell에서 실행합니다.

```powershell
git clone https://github.com/MyGumii/agentcat_screen_saver.git
cd agentcat_screen_saver

# Agent Cat connector가 없을 때만 설치
if (-not (Get-Command agentcat -ErrorAction SilentlyContinue)) {
    irm https://raw.githubusercontent.com/yong076/agentcat-connectors/main/install.ps1 | iex
}

# 로컬 수집 상태 확인
agentcat snapshot --json

# Herdr가 설치된 PC에서는 실시간 에이전트 상태 확인
herdr status
herdr api snapshot

# 빌드 + 화면보호기(5분) + 1분 잠금 화면 갱신 + 즉시 실행 단축키 설치
powershell -ExecutionPolicy Bypass -File .\install.ps1 -TimeoutSeconds 300
```

설치 후 생성되는 파일:

```text
%LOCALAPPDATA%\AgentCatScreenSaver\AgentCatScreenSaver.scr
%LOCALAPPDATA%\AgentCatScreenSaver\AgentCatScreenSaver.exe
%LOCALAPPDATA%\AgentCatScreenSaver\LockScreen\agentcat-lock-*.png
```

Windows 설정은 다음과 같이 적용됩니다.

- 화면보호기 사용: 켜짐
- 대기 시간: 기본 300초
- 다시 시작할 때 로그온 화면 표시: 켜짐
- 기존 설정: 최초 설치 시 `previous-settings.json`으로 백업
- 잠금 화면 이전 이미지: 최초 설치 시 `previous-lockscreen.json`으로 백업
- 잠금 화면 갱신: 예약 작업 `AgentCatLockScreenUpdater`, 1분 간격
- 예약 작업은 `wscript.exe //B //Nologo` 숨김 실행기를 사용해 PowerShell 창이 깜빡이지 않도록 실행
- 즉시 화면보호기 실행: 바탕 화면 바로가기 또는 `Ctrl+Alt+A`

## Windows 잠금 화면과의 차이

Windows의 실제 잠금 화면과 로그인 UI는 보안 데스크톱에서 실행되므로 일반 `.scr` 프로그램을 그 위에
실시간 애니메이션으로 표시할 수 없습니다. 대신 이 프로젝트는 Agent Cat과 Herdr 상태를 1920×1080
PNG로 렌더링하고 Windows `LockScreen.SetImageFileAsync` API로 적용합니다.

따라서 이 프로젝트는 다음과 같이 동작합니다.

1. PC가 유휴 상태가 되면 Agent Cat + Herdr 화면보호기가 연속 애니메이션으로 표시됩니다.
2. 예약 작업은 로그인 세션이 유지되는 동안 잠금 여부와 관계없이 1분마다 새 PNG를 만들고 적용합니다.
3. `Win+L`로 잠그면 가장 최근에 적용된 정적 대시보드가 Windows 잠금 화면에 표시됩니다.
4. 화면보호기에서 입력하면 `ScreenSaverIsSecure=1` 설정에 따라 Windows 로그인 화면으로 전환됩니다.

작업 스케줄러가 공식 지원하는 반복 간격의 최솟값은 1분입니다. 10초 주기는 공식 스키마 범위 밖이므로
사용하지 않습니다. 또한 Windows가 이미 표시 중인 잠금 화면을 언제 다시 그릴지는 OS 캐시에 따라 달라질
수 있지만, 배경 이미지 파일과 사용자 잠금 화면 설정 자체는 매분 갱신됩니다. 보안 데스크톱을 우회하거나
로그인 UI를 교체하지 않습니다.

참고 문서:

- [Microsoft Learn: Configure the Desktop and Lock Screen Backgrounds in Windows](https://learn.microsoft.com/windows/configuration/background/)
- [Microsoft Learn: Credentials Processes in Windows Authentication](https://learn.microsoft.com/windows-server/security/windows-authentication/credentials-processes-in-windows-authentication)
- [Microsoft Learn: Task Scheduler repetition interval](https://learn.microsoft.com/windows/win32/taskschd/taskschedulerschema-interval-repetitiontype-element)

잠금 화면 갱신 확인:

```powershell
Get-ScheduledTaskInfo -TaskName AgentCatLockScreenUpdater
Get-Content "$env:LOCALAPPDATA\AgentCatScreenSaver\LockScreen\status.json"
```

예약 작업의 실행 프로그램은 `%WINDIR%\System32\wscript.exe`이고, 인수로
`run-lockscreen-update-hidden.vbs`를 전달합니다. VBS는 PowerShell 자식 프로세스를 창 스타일 `0`으로
시작하므로 `-WindowStyle Hidden`만 사용할 때 생길 수 있는 짧은 콘솔 창 깜빡임을 방지합니다.

## 미리보기

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
Start-Process .\bin\AgentCatScreenSaver.exe -ArgumentList /test
```

실제 전체 화면 모드:

```powershell
Start-Process .\bin\AgentCatScreenSaver.scr -ArgumentList /s
```

마우스 또는 키보드 입력으로 종료됩니다.

## 즉시 화면보호기 실행

Windows에는 `Win+L`과 같은 기본 화면보호기 단축키가 없습니다. 설치 스크립트가 다음 두 가지를 만듭니다.

- 바탕 화면의 `Agent Cat Screen Saver` 바로가기
- 전역 단축키 `Ctrl+Alt+A`

명령으로 직접 실행하려면:

```powershell
Start-Process "$env:LOCALAPPDATA\AgentCatScreenSaver\AgentCatScreenSaver.scr" -ArgumentList /s
```

## Herdr 연동

별도 설정은 필요하지 않습니다. 화면보호기는 다음 순서로 `herdr.exe`를 찾고 2초마다
`herdr api snapshot`을 읽습니다.

1. `HERDR_EXE` 환경 변수
2. `%LOCALAPPDATA%\Programs\Herdr\bin\herdr.exe`
3. 현재 `PATH`

표시되는 상태는 `working`, `blocked`, `done`, `idle`, `unknown`입니다. 목록은 빠른 판단을 위해
`blocked` → `done` → `working` → `idle` → `unknown` 순으로 정렬됩니다. `working`이 하나라도 있으면
치즈냥이는 질주하고, `blocked`와 `done`은 고양이 옆에도 배지로 표시됩니다.

Herdr가 설치되어 있지 않거나 서버가 중지되어 있어도 Agent Cat 사용량 화면은 그대로 동작합니다.

## Claude Code PC에서 확인할 것

Claude Code를 한 번 실행해 로그인하고 실제 요청을 수행한 뒤 확인합니다.

```powershell
$env:PYTHONIOENCODING = 'utf-8'
agentcat doctor --json
agentcat snapshot --json
```

정상 수집 시 스냅샷에서 다음 항목을 볼 수 있습니다.

```text
providers.claude.status = "ok"
providers.claude.tokens.today
providers.claude.tokens.week
providers.claude.tokens.month
activity.countsByProvider.claude >= 1   # Claude 실행 중
providers.claude.limits.quotas[]        # 계정이 쿼터를 노출할 때만
```

Claude가 아직 사용되지 않았다면 `not_found`, 로그인하지 않았다면 `not_configured`, 텔레메트리가 아직 없다면
데이터 대기 상태가 표시될 수 있습니다. 화면보호기는 존재하지 않는 값을 추정하지 않습니다.

## 다른 PC의 AI 에이전트에게 그대로 줄 요청문

다른 Windows PC에서 Codex 또는 Claude Code 에이전트에게 아래 블록을 그대로 전달하면 됩니다.

```text
Set up the Agent Cat Windows screen saver from this repository:
https://github.com/MyGumii/agentcat_screen_saver

1. Verify whether the Agent Cat connector exists with:
   agentcat snapshot --json
2. If agentcat is missing, install it on Windows with:
   irm https://raw.githubusercontent.com/yong076/agentcat-connectors/main/install.ps1 | iex
3. Clone the repository and inspect README.md.
4. Run install.ps1 with a 300-second timeout.
5. Verify the installed SCR path and Windows screen-saver registry values.
6. Run the /test preview and confirm the orange Agent Cat animates.
7. If Claude Code is installed, run agentcat doctor --json and confirm Claude hooks/login/usage status.
8. If Herdr is installed, run herdr status and herdr api snapshot; confirm the preview shows live agent states.
9. Verify AgentCatLockScreenUpdater has a PT1M repetition interval and status.json reports applied=true.
10. Verify the desktop shortcut starts the continuous /s screen saver and uses Ctrl+Alt+A.
11. Use agentcat snapshot --json as the source of truth. Never estimate quotas that are absent from the snapshot.
12. Do not upload or report prompts, responses, terminal output, source code, credentials, or conversation bodies.
```

## 수동 빌드

에셋 준비와 빌드를 분리해서 실행할 수도 있습니다.

```powershell
# 기본 Agent Cat 설치 경로 자동 탐색
powershell -ExecutionPolicy Bypass -File .\prepare-assets.ps1

# 경로를 직접 지정해야 할 때
powershell -ExecutionPolicy Bypass -File .\prepare-assets.ps1 `
  -AgentCatExe "C:\path\to\agent-cat-windows.exe"

powershell -ExecutionPolicy Bypass -File .\build.ps1
```

표준 화면보호기 인수:

- `/s`: 전체 화면 실행
- `/test`: 크기 조절 가능한 미리보기
- `/snapshot <PNG 경로>`: 잠금 화면용 1920×1080 정적 대시보드 생성
- `/c`: 정보 창

## 제거 및 이전 설정 복원

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

예약 작업과 바탕 화면 단축키를 제거하고, 최초 설치 전에 백업한 화면보호기 설정 및 잠금 화면 이미지를
복원합니다.

## 문제 해결

### `agentcat` 명령을 찾을 수 없음

```powershell
irm https://raw.githubusercontent.com/yong076/agentcat-connectors/main/install.ps1 | iex
agentcat snapshot --json
```

### Agent Cat 앱 실행 파일을 찾을 수 없음

Agent Cat Windows 앱을 설치하고 한 번 실행하거나 다음처럼 명시적으로 지정합니다.

```powershell
.\prepare-assets.ps1 -AgentCatExe "C:\Users\you\AppData\Local\Agent Cat\agent-cat-windows.exe"
```

### Claude 카드가 `NOT CONNECTED`

1. 해당 PC에서 `claude`를 실행하고 로그인합니다.
2. 실제 Claude Code 요청을 한 번 수행합니다.
3. `agentcat doctor --json`에서 `claude.credentials`, `claude.journal`, `claude.hooks`를 확인합니다.
4. `agentcat snapshot --json`을 다시 실행합니다.

### Herdr가 `OFFLINE`으로 표시됨

```powershell
herdr status
herdr api snapshot
```

Herdr 서버가 중지된 경우 Herdr를 다시 실행합니다. 표준 경로가 아닌 곳에 설치했다면 사용자 환경 변수
`HERDR_EXE`에 `herdr.exe`의 전체 경로를 지정한 뒤 화면보호기를 다시 시작합니다.

### 화면보호기 화면은 뜨지만 데이터가 오래됨

```powershell
agentcat doctor --json
agentcat snapshot --json
```

화면보호기는 먼저 `http://127.0.0.1:8765/v1/snapshot`을 읽고, 데몬에 접근할 수 없으면
`%USERPROFILE%\.agentcat\latest-snapshot.json`으로 대체합니다.

### 잠금 화면 이미지가 갱신되지 않음

```powershell
Get-ScheduledTask -TaskName AgentCatLockScreenUpdater
Get-ScheduledTaskInfo -TaskName AgentCatLockScreenUpdater
Get-Content "$env:LOCALAPPDATA\AgentCatScreenSaver\LockScreen\status.json"
Start-ScheduledTask -TaskName AgentCatLockScreenUpdater
```

`status.json`의 `applied`가 `true`이고 `image` 파일명이 매분 바뀌면 생성과 Windows API 적용은
정상입니다. 이미 열려 있는 잠금 화면의 즉시 재표시는 Windows 캐시 정책에 영향을 받을 수 있습니다.

1분마다 PowerShell 창이 잠깐 보인다면 예약 작업의 `Actions.Execute`가 `wscript.exe`인지 확인하고
`install.ps1`을 다시 실행합니다. 최신 설치는 PowerShell을 예약 작업에서 직접 실행하지 않습니다.

## 개인정보 및 네트워크

- 화면보호기는 loopback 주소 `127.0.0.1` 외의 서버로 데이터를 보내지 않습니다.
- Agent Cat 스냅샷의 메타데이터만 사용합니다.
- Herdr에서는 로컬 스냅샷의 상태·개수·표시용 이름·포커스 여부만 사용합니다.
- `herdr agent read`를 호출하지 않으며 프롬프트, 응답, 터미널 출력, 코드, 파일 내용과 대화 본문은 표시하거나 저장하지 않습니다.
- 잠금 화면 PNG는 로컬 `%LOCALAPPDATA%\AgentCatScreenSaver\LockScreen`에만 저장하며 최근 5장만 유지합니다.
- 사용량 API와 쿼터 수집 방식은 Agent Cat connector의 동작을 따릅니다.

## 라이선스 및 Agent Cat 에셋

이 저장소의 소스 코드는 MIT License입니다.

Agent Cat 이름, 앱 및 치즈냥이 아트워크는 이 저장소의 MIT 라이선스 대상이 아닙니다.
원본 PNG는 저장소에 포함하지 않으며, 사용자의 로컬 Agent Cat 설치본에서 개인 사용을 위해 빌드 시 추출됩니다.
Agent Cat의 라이선스와 이용 조건을 별도로 확인하세요.

## 관련 링크

- [Agent Cat](https://agentcat.app/)
- [Agent Cat connectors](https://github.com/yong076/agentcat-connectors)
