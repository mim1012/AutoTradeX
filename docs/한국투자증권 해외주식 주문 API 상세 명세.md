# 한국투자증권 해외주식 주문 API 상세 명세

## 출처
- **공식 문서**: https://apiportal.koreainvestment.com/apiservice (해외주식 주문 API)
- **분석일**: 2025년 11월 10일

---

## 1. 기본 정보

| 항목 | 내용 |
|------|------|
| **API 이름** | 해외주식 주문 |
| **TR ID** | TTTT1002U (미국매수), TTTT1006U (아시아 국가 하단 규격서 참고) |
| **Method** | POST |
| **URL** | `/uapi/overseas-stock/v1/trading/order` |
| **Format** | JSON |
| **실전 Domain** | https://openapi.koreainvestment.com:9443 |
| **모의 Domain** | https://openapivts.koreainvestment.com:29443 |

---

## 2. 주문 가능 시간 (한국 시간 기준)

### 2.1. 미국 주식

| 구분 | 시간 (일반) | 시간 (썸머타임) |
|------|------------|----------------|
| **정규장** | 23:30 ~ 06:00 | 22:30 ~ 05:00 |
| **프리마켓** | 18:00 ~ 23:30 | 17:00 ~ 22:30 |
| **애프터마켓** | 06:00 ~ 07:00 | 05:00 ~ 07:00 |

**중요**: 프리마켓 및 애프터마켓 시간대에도 주문 가능합니다.

### 2.2. 기타 시장

| 시장 | 거래 시간 (한국 시간) |
|------|---------------------|
| **일본** | (오전) 09:00 ~ 11:30, (오후) 12:30 ~ 15:00 |
| **상해** | 10:30 ~ 16:00 |
| **홍콩** | (오전) 10:30 ~ 13:00, (오후) 14:00 ~ 17:00 |

---

## 3. 요청 파라미터 (Request)

### 3.1. Header

| Element | 한글명 | Type | Required | Description |
|---------|--------|------|----------|-------------|
| **content-type** | 컨텐츠타입 | String | N | application/json; charset=utf-8 |
| **authorization** | 접근토큰 | String | Y | Bearer {access_token} |
| **appkey** | 앱키 | String | Y | 발급받은 AppKey |
| **appsecret** | 앱시크릿키 | String | Y | 발급받은 AppSecret |
| **tr_id** | 거래ID | String | Y | TTTT1002U (미국매수), TTTT1006U (아시아) |
| **custtype** | 고객타입 | String | N | P (개인) |

### 3.2. Body (주요 파라미터)

| Element | 한글명 | Type | Required | Description |
|---------|--------|------|----------|-------------|
| **CANO** | 종합계좌번호 | String(8) | Y | 계좌번호 앞 8자리 |
| **ACNT_PRDT_CD** | 계좌상품코드 | String(2) | Y | 계좌번호 뒤 2자리 |
| **OVRS_EXCG_CD** | 해외거래소코드 | String(4) | Y | NASD (나스닥), NYSE (뉴욕), AMEX (아멕스), SEHK (홍콩), SHAA (상해), SZAA (심천), TKSE (도쿄), HASE (하노이), VNSE (호치민) |
| **PDNO** | 상품번호 (종목코드) | String(12) | Y | 종목코드 (예: AAPL, TSLA) |
| **ORD_QTY** | 주문수량 | String(10) | Y | 주문 수량 |
| **OVRS_ORD_UNPR** | 해외주문단가 | String(15) | Y | 주문 가격 (소수점 2자리까지) |
| **ORD_SVR_DVSN_CD** | 주문서버구분코드 | String(2) | Y | "0" 고정 |
| **ORD_DVSN** | 주문구분 | String(2) | Y | **00: 지정가**, **32: LOC (종가 지정가)**, **34: LOO (시가 지정가)**, 31: MOC (종가 시장가), 33: MOO (시가 시장가) |

**중요**: `ORD_DVSN` 파라미터에서 **32 (LOC)**를 사용하면 종가 지정가 주문이 가능합니다!

---

## 4. 응답 파라미터 (Response)

### 4.1. Body

| Element | 한글명 | Type | Description |
|---------|--------|------|-------------|
| **rt_cd** | 성공 실패 여부 | String | "0": 성공, "1": 실패 |
| **msg_cd** | 응답코드 | String | 응답 메시지 코드 |
| **msg1** | 응답메세지 | String | 응답 메시지 내용 |
| **output.ODNO** | 주문번호 | String | 주문 고유 번호 (주문 추적용) |
| **output.ORD_TMD** | 주문시각 | String | 주문 접수 시각 (HHMMSS) |

---

## 5. LOC (Limit on Close) 주문 상세

### 5.1. LOC란?

**LOC (Limit on Close)**는 **종가 지정가 주문**으로, 장 마감 시점에 지정한 가격 이상/이하로 체결되는 주문 방식입니다.

**동작 원리**:
- **매수 LOC**: 종가가 지정가 **이하**일 때만 체결
- **매도 LOC**: 종가가 지정가 **이상**일 때만 체결

**예시 (매수)**:
- LOC 매수 주문: $70 지정
- 종가: $72 → **체결 안됨** (종가가 지정가보다 높음)
- 종가: $68 → **체결됨** (종가가 지정가보다 낮음, $68에 체결)

### 5.2. PRD 요구사항과의 관계

PRD v2.1에서는 **"LOC 주문가 +5%"**로 명시되어 있습니다.

**구현 방법**:
```csharp
// 현재가가 $100일 경우
decimal currentPrice = 100.00m;
decimal locPrice = currentPrice * 1.05m;  // $105.00

// LOC 주문 파라미터
var orderRequest = new
{
    ORD_DVSN = "32",  // LOC 주문
    OVRS_ORD_UNPR = locPrice.ToString("F2")  // "105.00"
};
```

**의미**:
- 현재가 $100인 종목을 LOC $105로 매수 주문
- 종가가 $105 이하일 때만 체결됨
- 종가가 $106이면 체결 안됨, $104이면 $104에 체결됨

### 5.3. LOC 주문 시 주의사항

1. **마감 10분 전 제한 가능성**: 일부 거래소에서는 마감 10분 전부터 LOC/MOC 주문을 제한할 수 있습니다. (PRD에서는 마감 10분 전부터 조건 검사 시작)
2. **체결 불확실성**: 종가가 지정가를 벗어나면 체결되지 않습니다.
3. **프리마켓/애프터마켓**: LOC 주문은 정규장 종가 기준이므로, 프리마켓/애프터마켓에서는 사용하지 않습니다.

---

## 6. 주문 구분 코드 (ORD_DVSN) 전체 목록

| 코드 | 명칭 | 설명 |
|------|------|------|
| **00** | 지정가 | 지정한 가격으로 주문 |
| **31** | MOC (Market on Close) | 종가 시장가 (종가에 무조건 체결) |
| **32** | LOC (Limit on Close) | 종가 지정가 (종가가 지정가 이하일 때 체결) ⭐ |
| **33** | MOO (Market on Open) | 시가 시장가 (시가에 무조건 체결) |
| **34** | LOO (Limit on Open) | 시가 지정가 (시가가 지정가 이하일 때 체결) |

**프로젝트에서 사용할 코드**: **32 (LOC)**

---

## 7. 모의투자 제한사항

**중요**: 모의투자에서는 **모든 해외 종목 매매가 지원되지 않습니다.** 일부 종목만 매매 가능합니다.

**해결 방법**:
- 초기 개발 단계: 모의투자 계좌로 API 호출 구조 테스트
- 실제 주문 테스트: 실전 계좌로 소액 테스트 (조회는 무료)

---

## 8. 에러 처리

### 8.1. 주요 에러 코드

| 에러 코드 | 메시지 | 원인 | 해결 방법 |
|----------|--------|------|----------|
| **EGW00123** | 초당 거래건수를 초과하였습니다 | Rate Limit 초과 | Throttling Queue 사용, 재시도 로직 구현 |
| **APBK0013** | 주문가능시간이 아닙니다 | 거래소 운영 시간 외 호출 | 거래소 세션 시간 확인 후 호출 |
| **APBK0917** | 주문수량이 부족합니다 | 매수 가능 금액 부족 | 매수 가능 금액 조회 후 주문 |

### 8.2. 재시도 전략

```csharp
public async Task<OrderResponse> PlaceOrderWithRetryAsync(OrderRequest request, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await _throttlingQueue.EnqueueAsync(() => PlaceOrderAsync(request));
        }
        catch (ApiRateLimitException ex)
        {
            if (i == maxRetries - 1) throw;
            
            var delaySeconds = Math.Pow(2, i);  // 1초, 2초, 4초...
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }
    }
    
    throw new Exception("Max retries exceeded");
}
```

---

## 9. C# 코드 예제 (LOC 주문)

```csharp
public class KisOrderClient
{
    private readonly HttpClient _httpClient;
    private readonly string _appKey;
    private readonly string _appSecret;
    private string _accessToken;

    public async Task<OrderResponse> PlaceLOCBuyOrderAsync(
        string accountNo,
        string symbol,
        int quantity,
        decimal locPrice)
    {
        var url = "https://openapi.koreainvestment.com:9443/uapi/overseas-stock/v1/trading/order";

        var requestBody = new
        {
            CANO = accountNo.Substring(0, 8),
            ACNT_PRDT_CD = accountNo.Substring(8, 2),
            OVRS_EXCG_CD = GetExchangeCode(symbol),  // "NASD" or "NYSE"
            PDNO = symbol,
            ORD_QTY = quantity.ToString(),
            OVRS_ORD_UNPR = locPrice.ToString("F2"),  // 소수점 2자리
            ORD_SVR_DVSN_CD = "0",
            ORD_DVSN = "32"  // LOC 주문 ⭐
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        
        // Header 설정
        request.Headers.Add("authorization", $"Bearer {_accessToken}");
        request.Headers.Add("appkey", _appKey);
        request.Headers.Add("appsecret", _appSecret);
        request.Headers.Add("tr_id", "TTTT1002U");  // 미국 매수
        request.Headers.Add("custtype", "P");

        request.Content = new StringContent(
            JsonConvert.SerializeObject(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<OrderResponse>(jsonString);
    }

    private string GetExchangeCode(string symbol)
    {
        // 간단한 예시: 실제로는 종목 마스터 파일에서 조회
        return "NASD";  // 나스닥 기본값
    }
}

public class OrderResponse
{
    public string rt_cd { get; set; }
    public string msg_cd { get; set; }
    public string msg1 { get; set; }
    public OrderOutput output { get; set; }
}

public class OrderOutput
{
    public string ODNO { get; set; }  // 주문번호
    public string ORD_TMD { get; set; }  // 주문시각
}
```

---

## 10. 프로젝트 적용 시 체크리스트

- [ ] 한국투자증권 계좌 개설 및 해외주식 서비스 신청
- [ ] Open API 앱키/시크릿 발급
- [ ] Access Token 발급 로직 구현
- [ ] 거래소 운영 시간 확인 로직 구현 (썸머타임 자동 반영)
- [ ] LOC 주문 파라미터 설정 (`ORD_DVSN = "32"`)
- [ ] 주문가 계산 로직 구현 (현재가 × 1.05)
- [ ] Throttling Queue 구현 (초당 20회 제한 준수)
- [ ] 에러 처리 및 재시도 로직 구현
- [ ] 주문 결과 로깅 (주문번호, 시각, 체결 여부)

---

## 11. 참고 자료

- **공식 API 문서**: https://apiportal.koreainvestment.com/apiservice
- **GitHub 샘플 코드**: https://github.com/koreainvestment/open-trading-api
- **LOC 주문 설명**: https://melobooboo.tistory.com/108
- **한국투자증권 거래 가능 시간**: https://securities.koreainvestment.com/main/bond/research/_static/TF03ca050001.jsp

---

## 12. 결론

한국투자증권 API는 **LOC (종가 지정가) 주문을 완벽하게 지원**하며, PRD v2.1의 요구사항을 모두 충족합니다.

**핵심 요약**:
- LOC 주문 코드: `ORD_DVSN = "32"`
- 주문가: 현재가 × 1.05 (PRD 요구사항)
- 거래 시간: 미국 정규장 23:30 ~ 06:00 (썸머타임 22:30 ~ 05:00)
- 마감 10분 전부터 조건 검사 시작 (PRD 요구사항)

이제 이 명세를 바탕으로 C# 프로젝트의 주문 로직을 구현할 수 있습니다.
