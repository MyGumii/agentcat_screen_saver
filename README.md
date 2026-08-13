# Agent Cat Screen Saver for Windows

Agent Cat의 로컬 사용량 스냅샷을 읽어 Codex와 Claude Code 상태를 보여주는 Windows 화면보호기입니다.
Codex 또는 Claude가 작업 중이면 Agent Cat Windows 앱의 치즈냥이가 제자리에서 빠르게 달리고 질주합니다.

## 주요 기능

- `agentcat snapshot --json`과 동일한 로컬 스냅샷을 5초마다 읽습니다.
- Codex와 Claude의 오늘, 최근 7일, 최근 30일 토큰을 각각 표시합니다.
- 제공자가 실제 쿼터 비율을 노출할 때만 정확한 잔여 쿼터를 표시합니다.
- `activity.countsByProvider`와 `motionStage`를 사용해 고양이 속도를 바꿉니다.
- 키보드 또는 마우스 입력 시 종료됩니다.
- 화면보호기에서 복귀할 때 Windows 로그인을 요구하도록 설정합니다.
- 프롬프트, 응답, 소스 코드, 대화 본문 또는 OAuth 토큰을 읽거나 표시하지 않습니다.

## 데이터가 동작하는 방식

이 화면보호기는 원격 PC를 직접 모니터링하지 않습니다. 각 PC가 자기 로컬 Agent Cat 데이터를 읽습니다.

```text
Codex / Claude Code
        ↓ 로컬 사용량·프로세스 메타데이터
Agent Cat connector (127.0.0.1:8765)
        ↓ /v1/snapshot
Agent Cat Screen Saver
```

- Codex 합계: 로컬 `~/.codex/state_*.sqlite` 및 세션 사용량
- Claude 합계: 로컬 `~/.claude/stats-cache.json`, Claude Code 사용량 필드 및 Agent Cat hooks
- 실행 상태: `activity.countsByProvider.codex/claude`
- 정확한 쿼터: `providers.<provider>.limits.quotas[]`에 값이 있을 때만 표시

Claude를 사용하는 다른 PC에서도 Claude 사용량을 보려면 **그 PC에도** Agent Cat 커넥터와 이 화면보호기를 설치해야 합니다.

## 요구 사항

- Windows 10 또는 Windows 11
- [Agent Cat Windows 앱](https://agentcat.app/)이 설치되어 있거나 실행 가능해야 함
- Agent Cat local connector
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

# 빌드 + 현재 사용자 화면보호기로 등록 (5분)
powershell -ExecutionPolicy Bypass -File .\install.ps1 -TimeoutSeconds 300
```

설치 후 생성되는 파일:

```text
%LOCALAPPDATA%\AgentCatScreenSaver\AgentCatScreenSaver.scr
```

Windows 설정은 다음과 같이 적용됩니다.

- 화면보호기 사용: 켜짐
- 대기 시간: 기본 300초
- 다시 시작할 때 로그온 화면 표시: 켜짐
- 기존 설정: 최초 설치 시 `previous-settings.json`으로 백업

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
8. Use agentcat snapshot --json as the source of truth. Never estimate quotas that are absent from the snapshot.
9. Do not upload or report prompts, responses, transcripts, source code, credentials, or conversation bodies.
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
- `/c`: 정보 창

## 제거 및 이전 설정 복원

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

최초 설치 전에 백업한 사용자 화면보호기 설정으로 복원합니다.

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

### 화면보호기 화면은 뜨지만 데이터가 오래됨

```powershell
agentcat doctor --json
agentcat snapshot --json
```

화면보호기는 먼저 `http://127.0.0.1:8765/v1/snapshot`을 읽고, 데몬에 접근할 수 없으면
`%USERPROFILE%\.agentcat\latest-snapshot.json`으로 대체합니다.

## 개인정보 및 네트워크

- 화면보호기는 loopback 주소 `127.0.0.1` 외의 서버로 데이터를 보내지 않습니다.
- Agent Cat 스냅샷의 메타데이터만 사용합니다.
- 프롬프트, 응답, 코드, 파일 내용과 대화 본문은 표시하거나 저장하지 않습니다.
- 사용량 API와 쿼터 수집 방식은 Agent Cat connector의 동작을 따릅니다.

## 라이선스 및 Agent Cat 에셋

이 저장소의 소스 코드는 MIT License입니다.

Agent Cat 이름, 앱 및 치즈냥이 아트워크는 이 저장소의 MIT 라이선스 대상이 아닙니다.
원본 PNG는 저장소에 포함하지 않으며, 사용자의 로컬 Agent Cat 설치본에서 개인 사용을 위해 빌드 시 추출됩니다.
Agent Cat의 라이선스와 이용 조건을 별도로 확인하세요.

## 관련 링크

- [Agent Cat](https://agentcat.app/)
- [Agent Cat connectors](https://github.com/yong076/agentcat-connectors)
