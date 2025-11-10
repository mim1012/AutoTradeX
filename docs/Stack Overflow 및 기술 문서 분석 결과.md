# Stack Overflow 및 기술 문서 분석 결과

## 1. BlockingCollection<T> - 생산자-소비자 패턴 (Microsoft 공식 문서)
**URL**: https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/blockingcollection-overview

### 핵심 특징
- **스레드 안전 컬렉션**: 다중 스레드 환경에서 안전하게 데이터 추가/제거 가능
- **생산자-소비자 패턴 구현**: 실시간 데이터 처리에 최적화된 구조
- **Blocking 지원**: 컬렉션이 비어있거나 가득 찰 때 자동으로 대기

### 주요 기능
1. **Bounding (용량 제한)**
   ```csharp
   // 최대 100개 항목만 보관 가능
   BlockingCollection<Data> dataItems = new BlockingCollection<Data>(100);
   ```
   - 메모리 사용량 제어
   - 생산자가 소비자보다 너무 앞서가는 것 방지

2. **Blocking Operations**
   ```csharp
   // 소비자 스레드
   Task.Run(() =>
   {
       while (!dataItems.IsCompleted)
       {
           try
           {
               Data data = dataItems.Take(); // 데이터가 없으면 대기
               Process(data);
           }
           catch (InvalidOperationException) { }
       }
   });

   // 생산자 스레드
   Task.Run(() =>
   {
       while (moreItemsToAdd)
       {
           Data data = GetData();
           dataItems.Add(data); // 컬렉션이 가득 차면 대기
       }
       dataItems.CompleteAdding(); // 더 이상 추가 없음을 알림
   });
   ```

3. **Timed Operations**
   ```csharp
   bool success = bc.TryAdd(itemToAdd, timeout: 2000, cancellationToken);
   ```
   - 지정된 시간 내에 작업이 완료되지 않으면 false 반환
   - 무한 대기 방지

4. **Cancellation Support**
   ```csharp
   try
   {
       success = bc.TryAdd(itemToAdd, 2, cancellationToken);
   }
   catch (OperationCanceledException)
   {
       bc.CompleteAdding();
       break;
   }
   ```

5. **컬렉션 타입 지정**
   ```csharp
   // FIFO: ConcurrentQueue (기본값)
   var fifoCollection = new BlockingCollection<string>();
   
   // LIFO: ConcurrentStack
   var lifoCollection = new BlockingCollection<string>(new ConcurrentStack<string>());
   
   // 순서 없음: ConcurrentBag
   var bagCollection = new BlockingCollection<string>(new ConcurrentBag<string>(), 1000);
   ```

### 프로젝트 적용 방안

#### 우리 프로젝트의 데이터 흐름
```
[실시간 시세 수신 (OnReceiveRealData)]
           ↓
[BlockingCollection에 추가 (생산자)]
           ↓
[Worker 스레드 풀에서 꺼내기 (소비자)]
           ↓
[조건식 계산 및 평가]
           ↓
[후보 풀 업데이트]
```

#### 구체적인 구현 구조
```csharp
// 1. 실시간 데이터를 담을 BlockingCollection 선언
private BlockingCollection<RealTimeData> _realTimeDataQueue;

// 2. 초기화 (용량 제한 설정)
_realTimeDataQueue = new BlockingCollection<RealTimeData>(boundedCapacity: 1000);

// 3. 생산자: OnReceiveRealData 이벤트 핸들러
private void OnReceiveRealData(string code, string realType, string data)
{
    var realTimeData = new RealTimeData 
    { 
        StockCode = code, 
        Price = ParsePrice(data),
        Timestamp = DateTime.Now 
    };
    
    // 큐에 추가 (가득 차면 자동 대기)
    _realTimeDataQueue.Add(realTimeData);
}

// 4. 소비자: Worker 스레드들
private void StartWorkerThreads(int workerCount = 4)
{
    for (int i = 0; i < workerCount; i++)
    {
        Task.Run(() =>
        {
            foreach (var data in _realTimeDataQueue.GetConsumingEnumerable())
            {
                // 조건식 계산
                bool conditionMet = EvaluateConditions(data);
                
                // 후보 풀 업데이트 (ConcurrentDictionary 사용)
                _candidatePool[data.StockCode] = conditionMet;
            }
        });
    }
}
```

---

## 2. ConcurrentDictionary<TKey, TValue> - 스레드 안전 딕셔너리

### 핵심 특징
- **완전한 스레드 안전성**: lock 없이 다중 스레드에서 안전하게 읽기/쓰기 가능
- **높은 성능**: 읽기 작업이 많은 시나리오에서 특히 빠름

### 주요 메서드
```csharp
// 1. 추가 또는 업데이트
_candidatePool.AddOrUpdate(
    key: stockCode,
    addValue: true,
    updateValueFactory: (key, oldValue) => newValue
);

// 2. 안전한 읽기
if (_candidatePool.TryGetValue(stockCode, out bool isCandidate))
{
    // 값 사용
}

// 3. 조건부 업데이트
_candidatePool.TryUpdate(stockCode, newValue: true, comparisonValue: false);
```

### 프로젝트 적용: 후보 풀 관리
```csharp
// 300개 종목의 조건 충족 여부를 실시간으로 관리
private ConcurrentDictionary<string, bool> _candidatePool 
    = new ConcurrentDictionary<string, bool>();

// 여러 Worker 스레드에서 동시에 업데이트 가능
_candidatePool.AddOrUpdate(stockCode, true, (k, v) => true);

// UI 스레드에서 안전하게 읽기
var candidates = _candidatePool.Where(kv => kv.Value == true).Select(kv => kv.Key).ToList();
```

---

## 3. TPL Dataflow (고급 옵션)
**URL**: https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library

### 특징
- BlockingCollection보다 더 복잡한 파이프라인 구성 가능
- 각 단계별 병렬 처리 수준 세밀하게 제어 가능

### 프로젝트 적용 여부
- **현재 프로젝트**: BlockingCollection으로 충분 (단순하고 명확한 생산자-소비자 패턴)
- **향후 확장**: 복잡한 다단계 처리가 필요할 경우 고려

---

## 4. 성능 최적화 권장사항

### 1) Worker 스레드 수 결정
```csharp
// CPU 코어 수에 맞춰 조정
int workerCount = Environment.ProcessorCount; // 예: 4코어 = 4개 스레드
```

### 2) BlockingCollection 용량 설정
```csharp
// 너무 작으면: 생산자가 자주 대기 (처리 속도 저하)
// 너무 크면: 메모리 낭비
// 권장: 초당 예상 데이터 수 × 2~3초 분량
int capacity = 300 * 10; // 300종목 × 초당 10개 업데이트 가정
```

### 3) 조건식 계산 최적화
```csharp
// 무거운 계산은 캐싱
private ConcurrentDictionary<string, List<Candle>> _historicalDataCache;

// 조건식 평가 시 캐시된 데이터 활용
var historicalData = _historicalDataCache[stockCode];
bool result = EvaluateCondition(realtimeData, historicalData);
```


---

## 5. WPF Threading Model 및 Dispatcher (Microsoft 공식 문서)
**URL**: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model

### 핵심 개념
WPF는 **단일 스레드 UI 모델**을 기본으로 하며, UI 요소는 생성된 스레드(일반적으로 메인 UI 스레드)에서만 접근 가능합니다. 다른 스레드에서 UI를 업데이트하려면 **Dispatcher**를 통해 작업을 위임해야 합니다.

### Dispatcher 메서드 비교

| 메서드 | 동작 방식 | 반환 타입 | 사용 시나리오 |
|--------|----------|----------|--------------|
| **Invoke** | **동기적** - 작업이 완료될 때까지 대기 | void | 즉시 결과가 필요한 경우 (주의: 데드락 위험) |
| **InvokeAsync** | **비동기적** - 즉시 반환 | `DispatcherOperation` 또는 `Task` | 대부분의 경우 권장 (응답성 유지) |
| **BeginInvoke** | **비동기적** - 즉시 반환 (레거시) | `DispatcherOperation` | InvokeAsync와 유사 (구버전 호환성) |

### Dispatcher 우선순위 (DispatcherPriority)
Dispatcher 큐에 작업을 추가할 때 우선순위를 지정할 수 있습니다. 총 10단계의 우선순위가 있으며, 높은 우선순위 작업이 먼저 실행됩니다.

```csharp
// 낮은 우선순위로 작업 예약 (UI 이벤트가 우선)
Dispatcher.InvokeAsync(() => 
{
    // 계산 작업
}, DispatcherPriority.SystemIdle);
```

### 키움증권 API와 Dispatcher 사용 패턴

#### 문제 상황
```csharp
// ❌ 잘못된 예: Worker 스레드에서 직접 API 호출
Task.Run(() => 
{
    // COM 객체는 생성된 스레드(UI 스레드)에서만 접근 가능
    axKHOpenAPI.CommRqData(...); // InvalidOperationException 발생!
});
```

#### 해결 방법 1: Dispatcher.InvokeAsync (권장)
```csharp
// ✅ 올바른 예: Dispatcher를 통한 비동기 호출
private async Task<string> RequestDataAsync(string trCode)
{
    return await Application.Current.Dispatcher.InvokeAsync(() => 
    {
        // UI 스레드에서 안전하게 API 호출
        return axKHOpenAPI.CommRqData(trCode, ...);
    });
}

// Worker 스레드에서 사용
Task.Run(async () => 
{
    string result = await RequestDataAsync("OPT10032");
    ProcessData(result);
});
```

#### 해결 방법 2: 전용 API 통신 스레드 (프로젝트 채택 방식)
```csharp
// API 통신 전용 스레드 생성 (STA 모드)
private Thread _apiThread;
private Dispatcher _apiDispatcher;

private void InitializeApiThread()
{
    _apiThread = new Thread(() =>
    {
        // STA(Single-Threaded Apartment) 모드 설정 (COM 객체 필수)
        _apiDispatcher = Dispatcher.CurrentDispatcher;
        
        // COM 객체 생성 (이 스레드에서만 접근 가능)
        axKHOpenAPI = new AxKHOpenAPI();
        
        // 메시지 루프 시작
        Dispatcher.Run();
    });
    
    _apiThread.SetApartmentState(ApartmentState.STA);
    _apiThread.Start();
}

// 다른 스레드에서 API 호출
private async Task<string> CallApiAsync(string trCode)
{
    return await _apiDispatcher.InvokeAsync(() => 
    {
        return axKHOpenAPI.CommRqData(trCode, ...);
    });
}
```

### Task.Run과 Dispatcher의 조합 (비동기 패턴)

#### 예제: 날씨 데이터 가져오기 (Microsoft 공식 예제 응용)
```csharp
private async void FetchButton_Click(object sender, RoutedEventArgs e)
{
    // UI 업데이트 (UI 스레드에서 실행)
    StatusText.Text = "데이터 가져오는 중...";
    
    // 백그라운드 스레드에서 무거운 작업 수행
    var result = await Task.Run(() => 
    {
        // 시간이 오래 걸리는 계산 또는 네트워크 작업
        return FetchDataFromServer();
    });
    
    // 자동으로 UI 스레드로 돌아와서 UI 업데이트
    StatusText.Text = $"완료: {result}";
}
```

### 프로젝트 적용: 실시간 데이터 처리 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    UI Thread (Main)                         │
│  - WPF UI 렌더링                                            │
│  - 사용자 입력 처리                                          │
│  - Dispatcher를 통한 UI 업데이트                            │
└─────────────────────────────────────────────────────────────┘
                              ↕ Dispatcher.InvokeAsync
┌─────────────────────────────────────────────────────────────┐
│              API Communication Thread (STA)                 │
│  - 키움 API COM 객체 생성 및 관리                           │
│  - 모든 TR 요청/응답 처리                                    │
│  - OnReceiveRealData 이벤트 수신                            │
│  - Throttling Queue 관리                                    │
└─────────────────────────────────────────────────────────────┘
                              ↓ BlockingCollection
┌─────────────────────────────────────────────────────────────┐
│              Worker Thread Pool (4 threads)                 │
│  - 실시간 데이터 큐에서 데이터 꺼내기                        │
│  - 조건식 계산 (CPU 집약적 작업)                            │
│  - ConcurrentDictionary 업데이트                            │
└─────────────────────────────────────────────────────────────┘
```

### 데드락 방지 주의사항

#### ❌ 데드락 발생 가능한 코드
```csharp
// UI 스레드에서 실행
private void Button_Click(object sender, RoutedEventArgs e)
{
    // Dispatcher.Invoke는 동기 호출 - 완료될 때까지 대기
    var result = Dispatcher.Invoke(() => 
    {
        // 이 작업이 다시 UI 스레드를 필요로 하면 데드락!
        return SomeMethodThatNeedsUIThread();
    });
}
```

#### ✅ 데드락 방지 코드
```csharp
// async/await 사용
private async void Button_Click(object sender, RoutedEventArgs e)
{
    // InvokeAsync는 비동기 호출 - 즉시 반환
    var result = await Dispatcher.InvokeAsync(() => 
    {
        return SomeMethodThatNeedsUIThread();
    });
}
```

### 성능 최적화 팁

1. **작은 작업 단위로 분할**
   ```csharp
   // ❌ 나쁜 예: 한 번에 큰 작업
   Dispatcher.InvokeAsync(() => 
   {
       for (int i = 0; i < 1000; i++)
       {
           UpdateUI(i); // UI 스레드 장시간 점유
       }
   });
   
   // ✅ 좋은 예: 작은 단위로 분할
   for (int i = 0; i < 1000; i++)
   {
       int index = i;
       Dispatcher.InvokeAsync(() => UpdateUI(index), DispatcherPriority.Background);
   }
   ```

2. **우선순위 활용**
   - 중요한 UI 업데이트: `DispatcherPriority.Normal` (기본값)
   - 백그라운드 계산 결과 표시: `DispatcherPriority.Background`
   - 시스템 유휴 시에만 실행: `DispatcherPriority.SystemIdle`

3. **Dispatcher 처리량 극대화**
   - 작업 항목을 작게 유지하여 큐에서 대기하는 시간 최소화
   - 사용자 입력과 응답 사이의 지연을 줄임

