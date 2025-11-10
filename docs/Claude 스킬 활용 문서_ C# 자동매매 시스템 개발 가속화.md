# Claude 스킬 활용 문서: C# 자동매매 시스템 개발 가속화

**프로젝트명**: 한국투자증권 API 기반 미국주식 자동매매 시스템
**버전**: 1.0
**작성일**: 2025년 11월 10일

---

## 1. 개요

본 문서는 C# 자동매매 시스템 개발 프로젝트에서 Claude의 코드 생성 및 분석 능력을 극대화하여 개발 생산성을 높이고, 코드 품질을 향상시키기 위한 구체적인 **'스킬(Skill)'** 활용 방안을 정의합니다.

각 스킬은 특정 개발 작업을 자동화하기 위한 **'프롬프트(Prompt) 템플릿'** 형태로 제공되며, 개발자는 이를 복사하여 즉시 활용할 수 있습니다.

---

## 2. 핵심 활용 원칙

1.  **반복 작업 자동화**: DTO, ViewModel 등 반복적인 보일러플레이트 코드는 Claude를 통해 생성하여 시간을 절약합니다.
2.  **복잡한 로직 초안 생성**: API 호출, 스레딩, 알고리즘 등 복잡한 로직의 초기 코드를 Claude에게 맡기고, 개발자는 이를 검토하고 수정하는 데 집중합니다.
3.  **리팩토링 및 최적화**: 기존 코드를 제시하고, 더 효율적이거나 가독성 높은 코드로 리팩토링하도록 요청합니다.
4.  **단위 테스트 생성**: 특정 메서드에 대한 단위 테스트 케이스를 자동으로 생성하여 코드 커버리지를 높입니다.

---

## 3. 개발 단계별 Claude 스킬

### 스킬 1: 프로젝트 초기 구조 생성 (Scaffolding)

**목표**: .NET Worker Service와 WPF(MVVM) 프로젝트의 기본 폴더 구조와 클래스 파일을 생성합니다.

**프롬프트 템플릿**:
```
C# .NET 8 기반으로 다음 두 개의 프로젝트를 포함하는 솔루션의 기본 구조를 생성해줘.

1.  **백엔드 프로젝트 (Worker Service)**:
    - 이름: `AutoTrader.Worker`
    - 폴더: `Services`, `Models`, `Data`
    - `Services` 폴더 안에 `KisApiService.cs`, `WebSocketManager.cs`, `SchedulerService.cs` 인터페이스와 기본 클래스 생성

2.  **프론트엔드 프로젝트 (WPF)**:
    - 이름: `AutoTrader.UI`
    - MVVM 패턴 적용
    - 폴더: `Views`, `ViewModels`, `Models`
    - `ViewModels` 폴더 안에 `MainViewModel.cs`, `DashboardViewModel.cs`, `SettingsViewModel.cs` 기본 클래스 생성 (CommunityToolkit.Mvvm 사용)

모든 클래스는 네임스페이스와 기본 생성자를 포함해야 해.
```

### 스킬 2: API 데이터 모델(DTO) 생성

**목표**: 한국투자증권 API 문서의 JSON 응답 명세를 기반으로 C# DTO(Data Transfer Object) 클래스를 자동으로 생성합니다.

**프롬프트 템플릿**:
```
아래 한국투자증권 API의 JSON 응답 명세를 C# 클래스로 변환해줘. `Newtonsoft.Json`의 `JsonProperty` 어트리뷰트를 사용하여 JSON 필드명과 매핑해야 해.

**API**: 해외주식 거래대금순위 (HHDFS76320010)

**JSON 응답 예시**:
{
  "rt_cd": "0",
  "msg_cd": "SUCCESS",
  "msg1": "",
  "output2": [
    {
      "rank": "1",
      "symb": "TSLA",
      "name": "TESLA INC",
      "tamt": "25000000000"
    }
  ]
}
```

### 스킬 3: API 클라이언트 및 재시도 로직 구현

**목표**: `HttpClient`와 `Polly` 라이브러리를 사용하여 특정 API를 호출하고, 실패 시 지수 백오프 방식으로 재시도하는 코드를 생성합니다.

**프롬프트 템플릿**:
```
C#에서 `HttpClient`와 `Polly` 라이브러리를 사용하여 한국투자증권 '거래대금 순위' API를 호출하는 메서드를 작성해줘.

**요구사항**:
1.  메서드 시그니처: `public async Task<ApiResult<List<StockRank>>> GetTopStocksAsync(string marketCode)`
2.  `Polly`를 사용하여 다음 조건에서 재시도해야 함:
    - `HttpRequestException` 발생 시
    - HTTP 상태 코드가 5xx (서버 오류)일 경우
3.  재시도 정책: 3번까지 시도하며, 시도 간 딜레이는 1초, 2초, 4초로 증가 (지수 백오프)
4.  성공 시 API 응답을 `ApiResult` 객체로 래핑하여 반환
```

### 스킬 4: WebSocket 다중 세션 관리자 구현

**목표**: 300개의 종목을 8개의 WebSocket 세션으로 나누어 구독하고 관리하는 `MultiWebSocketManager` 클래스의 핵심 로직을 생성합니다.

**프롬프트 템플릿**:
```
C# `ClientWebSocket`을 사용하여 한국투자증권 실시간 시세를 구독하는 `MultiWebSocketManager` 클래스를 작성해줘.

**요구사항**:
1.  `Subscribe(List<string> symbols)` 메서드를 구현
2.  입력된 종목 리스트를 41개씩 나누어 여러 개의 WebSocket 세션을 생성하고 연결
3.  각 세션에서 수신된 데이터는 `OnDataReceived(RealtimeData data)` 이벤트를 통해 외부로 전달
4.  연결이 끊어졌을 때 자동으로 재연결하는 로직 포함
```

### 스킬 5: MVVM 보일러플레이트 코드 생성

**목표**: WPF UI의 특정 View에 필요한 ViewModel 클래스의 기본 구조를 `CommunityToolkit.Mvvm`을 사용하여 생성합니다.

**프롬프트 템플릿**:
```
`CommunityToolkit.Mvvm`을 사용하여 WPF 대시보드 화면의 ViewModel인 `DashboardViewModel.cs`를 작성해줘.

**요구사항**:
1.  `ObservableObject`를 상속받아야 함
2.  다음 속성들을 `[ObservableProperty]` 어트리뷰트를 사용하여 구현:
    - `TotalAssets` (decimal)
    - `Cash` (decimal)
    - `MarketStatus` (string, 예: "개장")
    - `IsConnected` (bool)
3.  `RefreshDataCommand` 라는 `[RelayCommand]`를 포함해야 함
```

### 스킬 6: 단위 테스트 코드 생성

**목표**: 특정 비즈니스 로직 메서드에 대한 단위 테스트 코드를 `xUnit` 프레임워크 기반으로 생성합니다.

**프롬프트 템플릿**:
```
아래 C# 메서드에 대한 `xUnit` 단위 테스트 코드를 작성해줘.

**테스트 대상 메서드**:
```csharp
public class OrderCalculator
{
    // 총 자산과 매수 비율, 후보 종목 수를 기반으로 종목당 주문 금액을 계산한다.
    public decimal CalculateOrderAmountPerStock(decimal totalAssets, double buyRatio, int candidateCount)
    {
        if (candidateCount <= 0) return 0;

        decimal totalOrderAmount = totalAssets * (decimal)(buyRatio / 100.0);
        return totalOrderAmount / candidateCount;
    }
}
```

**테스트 케이스**:
1.  후보가 2개일 때 정상적으로 50%씩 분배되는지 확인
2.  후보가 1개일 때 100% 할당되는지 확인
3.  후보가 0개일 때 0을 반환하는지 확인
```

---

## 4. Claude MCP 활용 방안 (향후 확장)

프로젝트가 안정화된 후에는, 자주 사용되는 API 호출들을 **Claude MCP(Model Context Protocol) 서버**로 구축하여 개발 효율성을 더욱 높일 수 있습니다.

- **KIS_MCP_Server**: 한국투자증권 API 호출을 추상화한 MCP 서버
- **사용 예시**: `$ manus-mcp-cli tool call get_top_stocks --server KIS_MCP_Server --input '{"market": "NAS"}'`

이를 통해 복잡한 API 호출 과정을 단순한 CLI 명령어로 대체하여, 스크립트 기반의 테스트나 데이터 수집 작업을 매우 쉽게 수행할 수 있습니다.
