# Claude 스킬 및 MCP를 활용한 개발 효율화 전략

## 1. 현황 진단 및 핵심 문제 정의

선행 리서치 결과, 프로젝트의 가장 시급하고 중요한 기술적 제약사항이 발견되었습니다. 바로 **키움증권 API(Open API+ 및 REST API)는 미국 주식 거래를 지원하지 않는다**는 점입니다. 이는 PRD v2.1의 핵심 요구사항인 '미국 주식 거래대금 상위 300개 종목 자동매매'를 원천적으로 구현 불가능하게 만드는 중대한 문제입니다.

따라서, 성공적인 프로젝트 완수를 위해서는 이 문제를 해결하기 위한 기술 스택의 근본적인 변경이 불가피합니다.

---

## 2. 대안 아키텍처 및 기술 스택 제안

이 문제를 해결하기 위한 세 가지 주요 대안을 제시하며, 각 방안의 장단점을 비교 분석한 결과는 다음과 같습니다.

| 구분 | **옵션 1: 한국투자증권(KIS) API로 전환 (⭐ 강력 권장)** | **옵션 2: 키움 API + 외부 데이터 소스 조합** | **옵션 3: 프로젝트 범위 변경 (국내 주식)** |
|:---|:---|:---|:---|
| **개요** | 해외주식을 완벽히 지원하는 KIS REST API로 전체 시스템을 재설계 | 거래대금 순위는 외부 API(예: Yahoo Finance)로 가져오고, 주문은 키움 API를 사용 | 프로젝트 대상을 미국 주식에서 국내 주식으로 변경 |
| **장점** | - **해외주식 완벽 지원**<br>- 최신 REST API 방식으로 개발 용이<br>- C#, Python 등 언어 제약 없음<br>- 공식 문서 및 예제 풍부 | - 키움증권의 낮은 수수료 유지<br>- 기존 키움 API 코드 일부 재활용 가능 | - 기술적 복잡도 최소화<br>- 키움 API만으로 모든 기능 구현 가능 |
| **단점** | - **업계 최고 수준의 수수료 (0.25%)**<br>- 기존 코드 재사용 불가, 전면 재개발 | - 아키텍처 복잡성 급증<br>- 외부 API 의존성 및 잠재적 비용 발생<br>- 데이터 동기화 및 안정성 문제 | - **사용자의 원래 목표(미국 주식)와 불일치**<br>- 국내/미국 시장 특성 차이 존재 |

**결론적으로, 높은 수수료라는 단점에도 불구하고 프로젝트의 목표 달성, 개발 용이성, 시스템 안정성을 종합적으로 고려했을 때, 한국투자증권(KIS) API로 전환하는 '옵션 1'이 가장 합리적이고 현실적인 선택입니다.**

---

## 3. Claude 스킬 및 MCP 적용 방안

'옵션 1'을 채택한다는 전제 하에, Claude의 고급 기능을 활용하여 개발 생산성을 극대화하고 코드 품질을 높이는 구체적인 방안은 다음과 같습니다.

### 3.1. Claude 스킬 활용 방안

반복적이고 정형화된 코드 작성을 '스킬'로 만들어 자동화함으로써, 개발자는 핵심 비즈니스 로직 구현에만 집중할 수 있습니다.

| 스킬 이름 (예시) | 설명 | 입력 (Input) | 출력 (Output) |
|:---|:---|:---|:---|
| `GenerateKisRestApiClient` | C# `HttpClient`를 사용하여 KIS REST API의 특정 기능을 호출하는 클라이언트 클래스 코드를 생성합니다. (인증, 헤더 포함) | - API 엔드포인트 (예: `/uapi/hashkey`)<br>- HTTP 메서드 (GET/POST)<br>- 요청/응답 DTO 클래스 이름 | 완성된 C# 클래스 파일 (.cs) |
| `CreateWpfMvvmTemplate` | WPF 프로젝트를 위한 MVVM 패턴의 기본 구조(View, ViewModel, Model 폴더 및 기본 클래스)를 자동으로 생성합니다. | - View 이름 (예: `MainView`) | - `MainView.xaml`<br>- `MainViewModel.cs`<br>- `BaseViewModel.cs` 등 구조 파일 |
| `ImplementApiThrottlingQueue` | API 호출 제한(Throttling)을 준수하기 위한 C# `BlockingCollection` 기반의 스레드 안전(Thread-safe) 큐 클래스를 구현합니다. | - 초당 호출 제한 횟수 (예: 5) | Throttling 기능이 구현된 C# 클래스 파일 |
| `GenerateSerilogConfig` | VPS 환경에 필수적인 파일 로깅(일별 분할, 자동 삭제 포함)을 위한 Serilog 설정 코드를 생성합니다. | - 로그 파일 경로<br>- 보관 주기 (일) | `Program.cs`에 적용할 Serilog 설정 코드 블록 |

### 3.2. Claude MCP (Model-Context Protocol) 활용 방안

현재 프로젝트는 독립 실행형 애플리케이션이지만, 향후 확장성과 데이터 관리 효율성을 위해 MCP를 활용한 백엔드 연동을 제안합니다. `supabase` MCP 서버를 활용하여 다음과 같은 기능을 구현할 수 있습니다.

#### 목표: 거래 내역 및 시스템 로그의 영구적 저장 및 관리

- **문제점**: 로컬 파일 로그는 VPS 장애 시 유실될 수 있으며, 외부에서 조회하거나 통계를 내기 어렵습니다.
- **해결책**: MCP를 통해 Supabase 클라우드 데이터베이스에 모든 거래 내역과 주요 시스템 이벤트를 실시간으로 저장합니다.

#### 구현 방안

1.  **Supabase 테이블 생성**: `trades`와 `system_logs` 테이블을 Supabase 프로젝트에 생성합니다.
2.  **MCP 연동 스킬 생성**: `manus-mcp-cli`를 호출하여 Supabase에 데이터를 저장하는 Claude 스킬을 정의합니다.

    **스킬 예시: `LogTradeToSupabase`**
    ```bash
    # Claude 내부적으로 실행될 명령어
    manus-mcp-cli tool call insert --server supabase --input '{
      "table": "trades",
      "data": {
        "timestamp": "{{timestamp}}",
        "symbol": "{{symbol}}",
        "order_type": "{{order_type}}",
        "price": {{price}},
        "quantity": {{quantity}},
        "status": "{{status}}"
      }
    }'
    ```

3.  **C# 애플리케이션 연동**: C# 코드에서는 주문 체결 이벤트 발생 시, 위 스킬을 호출하는 대신 직접 Supabase API를 호출하거나, 혹은 로컬 로그 파일에 쓰는 것만으로도 충분합니다. MCP는 주로 Manus 에이전트가 직접 백엔드와 상호작용할 때 사용됩니다.

#### 기대 효과
- **데이터 영속성**: 모든 거래 기록이 안전한 클라우드 DB에 영구 보관됩니다.
- **확장성**: 향후 웹 기반 대시보드를 구축하여 언제 어디서든 거래 현황과 통계를 조회할 수 있습니다.
- **안정성**: 애플리케이션과 데이터 저장소가 분리되어 시스템 안정성이 향상됩니다.

---

## 4. 외부 리소스 및 참고 자료

프로젝트 구현 시 참고할 핵심 GitHub 리포지토리 및 기술 문서는 다음과 같습니다.

| 리소스 종류 | 이름/링크 | 설명 및 활용 방안 |
|:---|:---|:---|
| **API (필수 전환)** | [한국투자증권 REST API](https://apiportal.koreainvestment.com/) | **(핵심)** 미국 주식 거래를 위한 필수 API. REST 방식이므로 C# `HttpClient`로 쉽게 연동 가능. |
| **C# 비동기 래퍼** | [teranum/KHOpenApi.NET](https://github.com/teranum/KHOpenApi.NET) | (참고용) 키움증권 OCX를 비동기(async/await) 방식으로 래핑한 구조. KIS API로 전환 시, 이 프로젝트의 이벤트 처리 및 비동기 패턴을 벤치마킹할 수 있습니다. |
| **생산자-소비자 패턴** | [MSDN: BlockingCollection](https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/blockingcollection-overview) | **(필수 적용)** API 호출 제한을 준수하고, 데이터 처리와 UI 업데이트를 분리하기 위한 핵심 패턴. 이 문서를 바탕으로 Throttling 큐를 구현합니다. |
| **UI 스레딩 모델** | [MSDN: WPF Threading Model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model) | **(필수 적용)** 백그라운드 스레드에서 수신한 데이터를 UI 스레드로 안전하게 업데이트하기 위해 `Dispatcher`를 사용하는 방법을 숙지해야 합니다. |
| **로깅 라이브러리** | [Serilog](https://serilog.net/) | **(권장)** VPS 환경에서의 안정적인 로그 관리를 위한 필수 라이브러리. 파일 자동 분할, 오래된 로그 삭제 등의 기능을 제공합니다. |

이상의 전략과 자료를 바탕으로, **한국투자증권 API를 채택**하고 **Claude 스킬을 적극 활용**하여 프로젝트를 진행하는 것이 가장 효율적이고 성공 확률이 높은 경로임을 제안합니다.
