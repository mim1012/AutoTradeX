# GitHub 리포지토리 분석 결과

## 1. KHOpenApi.NET (teranum)
**URL**: https://github.com/teranum/KHOpenApi.NET

### 핵심 특징
- **비동기 지원**: TaskCompletionSource를 사용한 현대적인 async/await 패턴 구현
- **크로스 플랫폼 UI 지원**: WinUI3, WPF, Winforms 모두 지원
- **개발 환경**: Visual Studio 2022, netstandard2.0

### 주요 기능
1. **비동기 로그인 및 TR 요청**
   - `CommConnectAsync()` - 비동기 로그인
   - `CommRqDataAsync()` - 비동기 TR 요청
   - `CommKwRqDataAsync()` - 비동기 복수 종목 조회
   - `GetConditionLoadAsync()` - 비동기 조건식 로드
   - `SendConditionAsync()` - 비동기 조건 검색

2. **비동기 주문**
   - `SendOrderAsync()` - 일반 주문
   - `SendOrderFOAsync()` - 선물옵션 주문
   - `SendOrderCreditAsync()` - 신용 주문

3. **간편 TR 요청 (RequestTrAsync)**
   ```csharp
   var response = await axKHOpenAPI.RequestTrAsync(
       "OPT10081", 
       indatas, 
       ["종목코드"], 
       ["일자", "현재가", "거래량"]
   );
   ```

### 프로젝트 적용 방안
1. **비동기 래퍼 구조 벤치마킹**: 우리 프로젝트의 API 통신 레이어에 이 구조를 그대로 적용하여 개발 시간 단축
2. **OPT10032 래퍼 생성**: 거래대금 상위 요청 TR을 이 패턴으로 구현
3. **해외주식 TR 확장**: 이 라이브러리는 국내 주식 중심이므로, 해외주식 TR(`opw20xxx`)에 대해 동일한 패턴으로 확장 구현

---

## 2. 추가 검색 대상 리포지토리
- smok95/KiwoomTrader: C# 연동 샘플
- xyz37/Kats: 자동매매 시스템 구현 사례
- seomse-kiwoom-api: 자바 통신용 C# 버전


## 2. KiwoomTrader (smok95)
**URL**: https://github.com/smok95/KiwoomTrader

### 핵심 특징
- 키움증권 Open API+ C# 연동 기본 예제
- OnReceiveTrData, GetCommData 구현 포함
- 실제 동작하는 샘플 코드 제공

### 주요 폴더 구조
- **KiwoomApi**: API 래퍼 클래스
- **KiwoomTrader**: 메인 애플리케이션
- **kiwoom-cli**: CLI 도구

### 개발 가이드 문서
- 공식 가이드: https://download.kiwoom.com/web/openapi/kiwoom_openapi_plus_devguide_ver_1.5.pdf

### 프로젝트 적용 방안
1. **기본 구조 참고**: COM Interop 설정 및 이벤트 핸들링의 기본 패턴 학습
2. **OnReceiveTrData 처리**: TR 응답 데이터 파싱 로직 참고
3. **실시간 데이터 수신**: OnReceiveRealData 이벤트 처리 방식 벤치마킹

