# 한국투자증권 해외주식 WebSocket 실시간 시세 API

## 출처
- **공식 문서**: https://apiportal.koreainvestment.com/apiservice (WebSocket 실시간 시세)
- **WikiDocs 예제**: https://wikidocs.net/164058
- **분석일**: 2025년 11월 10일

---

## 1. WebSocket 기본 정보

| 항목 | 내용 |
|------|------|
| **프로토콜** | WebSocket |
| **실전 Domain** | ws://ops.koreainvestment.com:21000 |
| **모의 Domain** | ws://ops.koreainvestment.com:31000 |
| **인증 방식** | approval_key (실시간 접속키) |
| **데이터 형식** | JSON (요청), Pipe-delimited (응답) |

---

## 2. 해외주식 실시간 시세 TR ID

### 2.1. 주요 TR ID

| TR ID | 명칭 | 설명 |
|-------|------|------|
| **HDFSCNT0** | 해외주식 실시간체결가 | 실시간 체결가, 체결량, 거래대금 등 |
| **HDFSASP0** | 해외주식 실시간호가 | 실시간 매수/매도 호가 10단계 |
| **HDFC0** | 해외주식 실시간체결통보 | 주문 체결 통보 (HTS ID 기반) |

**프로젝트에서 사용할 TR**: **HDFSCNT0 (실시간 체결가)** - 현재가, 등락률, 거래대금 등을 실시간으로 수신

---

## 3. WebSocket 연결 및 구독 흐름

### 3.1. 전체 흐름

```
1. REST API로 approval_key 발급
   ↓
2. WebSocket 연결 (ws://ops.koreainvestment.com:21000)
   ↓
3. 구독 요청 메시지 전송 (JSON 형식)
   ↓
4. 구독 성공 응답 수신 (iv, key 포함)
   ↓
5. 실시간 데이터 수신 (Pipe-delimited 형식)
   ↓
6. 데이터 파싱 및 처리
```

### 3.2. approval_key 발급 (REST API)

WebSocket 연결 전에 먼저 REST API로 `approval_key`를 발급받아야 합니다.

**Endpoint**: `POST /oauth2/Approval`

**Request**:
```json
{
  "grant_type": "client_credentials",
  "appkey": "{your_appkey}",
  "secretkey": "{your_appsecret}"
}
```

**Response**:
```json
{
  "approval_key": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

---

## 4. WebSocket 구독 요청 메시지

### 4.1. 요청 메시지 형식 (JSON)

```json
{
  "header": {
    "appkey": "{your_appkey}",
    "appsecret": "{your_appsecret}",
    "custtype": "P",
    "tr_type": "1",
    "content-type": "utf-8"
  },
  "body": {
    "input": {
      "tr_id": "HDFSCNT0",
      "tr_key": "AAPL"
    }
  }
}
```

### 4.2. 파라미터 설명

| 파라미터 | 설명 | 값 |
|---------|------|---|
| **appkey** | 발급받은 AppKey | 문자열 |
| **appsecret** | 발급받은 AppSecret | 문자열 |
| **custtype** | 고객타입 | "P" (개인) |
| **tr_type** | 거래 타입 | "1" (등록), "2" (해제) |
| **tr_id** | TR ID | "HDFSCNT0" (해외주식 실시간체결가) |
| **tr_key** | 종목코드 | "AAPL", "TSLA" 등 |

---

## 5. WebSocket 응답 메시지

### 5.1. 구독 성공 응답

```json
{
  "header": {
    "tr_id": "HDFSCNT0",
    "tr_key": "AAPL",
    "encrypt": "N"
  },
  "body": {
    "rt_cd": "0",
    "msg_cd": "OPSP0000",
    "msg1": "SUBSCRIBE SUCCESS",
    "output": {
      "iv": "xxxxxxxxxxxxxxxx",
      "key": "xxxxxxxxxxxxxxxx"
    }
  }
}
```

**중요**: `output.iv`와 `output.key`는 암호화된 데이터를 복호화할 때 사용됩니다.

### 5.2. 실시간 데이터 응답 (Pipe-delimited)

```
0|HDFSCNT0|001|AAPL^133526^0^69200^69300^...
```

**형식**:
```
{암호화유무}|{TR_ID}|{데이터건수}|{응답데이터}
```

- **암호화유무**: `0` (암호화 안됨), `1` (암호화됨)
- **TR_ID**: 구독한 TR ID (예: HDFSCNT0)
- **데이터건수**: 데이터 개수 (예: 001)
- **응답데이터**: `^`로 구분된 필드 값들

### 5.3. 응답 데이터 필드 (HDFSCNT0 - 해외주식 실시간체결가)

| 순서 | 필드명 | 설명 | 예시 |
|------|--------|------|------|
| 0 | 종목코드 | 종목 코드 | AAPL |
| 1 | 체결시각 | HHMMSS | 133526 |
| 2 | 현재가 | 현재 체결가 | 69200 |
| 3 | 전일대비 | 전일 대비 등락 | +100 |
| 4 | 등락률 | 등락률 (%) | 1.45 |
| 5 | 체결량 | 체결 수량 | 1000 |
| 6 | 거래대금 | 누적 거래대금 | 1234567890 |
| ... | ... | ... | ... |

**참고**: 정확한 필드 순서 및 의미는 한국투자증권 공식 API 문서를 참조해야 합니다.

---

## 6. 다중 종목 구독

### 6.1. 구독 제한

한국투자증권 WebSocket은 **하나의 세션당 최대 41개 종목**까지 구독할 수 있습니다.

**프로젝트 요구사항**: 거래대금 상위 **300종목** 실시간 구독

**해결 방법**: **8개의 WebSocket 세션** 생성
- 300 ÷ 41 ≈ 7.3 → **8개 세션** 필요
- 각 세션에서 약 37~38개 종목씩 구독

### 6.2. 다중 세션 구현 전략

```csharp
public class MultiWebSocketManager
{
    private List<WebSocketClient> _clients = new List<WebSocketClient>();
    
    public async Task SubscribeTop300Stocks(List<string> symbols)
    {
        int sessionCount = (int)Math.Ceiling(symbols.Count / 41.0);  // 8개 세션
        
        for (int i = 0; i < sessionCount; i++)
        {
            var sessionSymbols = symbols.Skip(i * 41).Take(41).ToList();
            
            var client = new WebSocketClient();
            await client.ConnectAsync("ws://ops.koreainvestment.com:21000");
            
            foreach (var symbol in sessionSymbols)
            {
                await client.SubscribeAsync("HDFSCNT0", symbol);
            }
            
            _clients.Add(client);
        }
    }
}
```

---

## 7. C# WebSocket 클라이언트 구현 예제

### 7.1. 기본 구조

```csharp
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;

public class KisWebSocketClient
{
    private ClientWebSocket _webSocket;
    private readonly string _appKey;
    private readonly string _appSecret;
    private CancellationTokenSource _cts;

    public event EventHandler<RealtimeStockData> OnDataReceived;

    public KisWebSocketClient(string appKey, string appSecret)
    {
        _appKey = appKey;
        _appSecret = appSecret;
        _webSocket = new ClientWebSocket();
    }

    public async Task ConnectAsync(string wsUrl)
    {
        await _webSocket.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
        _cts = new CancellationTokenSource();
        
        // 수신 루프 시작
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public async Task SubscribeAsync(string trId, string symbol)
    {
        var subscribeMessage = new
        {
            header = new
            {
                appkey = _appKey,
                appsecret = _appSecret,
                custtype = "P",
                tr_type = "1",  // 등록
                content_type = "utf-8"
            },
            body = new
            {
                input = new
                {
                    tr_id = trId,
                    tr_key = symbol
                }
            }
        };

        var json = JsonConvert.SerializeObject(subscribeMessage);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ProcessMessage(message);
            }
        }
    }

    private void ProcessMessage(string message)
    {
        // Pipe-delimited 데이터 파싱
        var parts = message.Split('|');
        
        if (parts.Length < 4) return;
        
        var isEncrypted = parts[0] == "1";
        var trId = parts[1];
        var dataCount = parts[2];
        var data = parts[3];

        if (trId == "HDFSCNT0")
        {
            var fields = data.Split('^');
            
            var stockData = new RealtimeStockData
            {
                Symbol = fields[0],
                Time = fields[1],
                CurrentPrice = decimal.Parse(fields[2]),
                Change = decimal.Parse(fields[3]),
                ChangeRate = decimal.Parse(fields[4]),
                Volume = long.Parse(fields[5]),
                TradeAmount = long.Parse(fields[6])
            };

            OnDataReceived?.Invoke(this, stockData);
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        await _webSocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Closing",
            CancellationToken.None);
    }
}

public class RealtimeStockData
{
    public string Symbol { get; set; }
    public string Time { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangeRate { get; set; }
    public long Volume { get; set; }
    public long TradeAmount { get; set; }
}
```

---

## 8. 프로젝트 적용 시나리오

### 8.1. 거래대금 상위 300종목 실시간 감시

```csharp
public class Top300StockMonitor
{
    private MultiWebSocketManager _wsManager;
    private List<string> _top300Symbols;

    public async Task StartMonitoringAsync()
    {
        // 1. 거래대금 상위 300종목 조회 (REST API)
        _top300Symbols = await GetTop300SymbolsAsync();

        // 2. WebSocket 8개 세션으로 구독
        _wsManager = new MultiWebSocketManager();
        await _wsManager.SubscribeTop300Stocks(_top300Symbols);

        // 3. 실시간 데이터 수신 이벤트 처리
        _wsManager.OnDataReceived += (sender, data) =>
        {
            // 조건식 평가
            bool conditionMet = EvaluateConditions(data);
            
            if (conditionMet)
            {
                // 후보 리스트에 추가
                AddToCandidate(data.Symbol);
            }
        };
    }
}
```

---

## 9. 주의사항

### 9.1. 세션 관리

- WebSocket 연결은 장시간 유지되므로, 연결 끊김 시 자동 재연결 로직 필요
- 각 세션의 상태를 모니터링하여 비정상 종료 시 복구

### 9.2. 데이터 처리 성능

- 300종목 × 초당 수십 건의 데이터 수신 가능
- 비동기 처리 및 큐 기반 처리로 성능 최적화 필요

### 9.3. 거래소 휴장 시간

- 거래소 휴장 시간에는 WebSocket 연결을 유지하지 않고 종료
- 장 시작 전 자동 재연결

---

## 10. 참고 자료

- **공식 API 문서**: https://apiportal.koreainvestment.com/apiservice
- **WikiDocs WebSocket 예제**: https://wikidocs.net/164058
- **GitHub 샘플 코드**: https://github.com/koreainvestment/open-trading-api

---

## 11. 결론

한국투자증권 WebSocket API는 해외주식 실시간 시세를 안정적으로 제공하며, 프로젝트의 요구사항인 **거래대금 상위 300종목 실시간 감시**를 완벽하게 지원합니다.

**핵심 요약**:
- TR ID: `HDFSCNT0` (해외주식 실시간체결가)
- 세션당 최대 41종목 구독 가능
- 300종목 감시를 위해 8개 세션 필요
- Pipe-delimited 형식의 실시간 데이터 수신
- C# `ClientWebSocket` 클래스로 구현 가능

이제 이 명세를 바탕으로 실시간 시세 수신 로직을 구현할 수 있습니다.
