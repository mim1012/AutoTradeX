# AutoTradeX — C# 미국주식 자동매매 시스템

한국투자증권 OpenAPI 기반으로 미국 주식을 조건 스크리닝 → 자동 매수/매도하는 Windows 데스크톱(.NET/WPF) 자동매매 시스템입니다.

## 주요 기능
- 한국투자증권 해외주식 API 연동 (시세·주문·잔고)
- WebSocket 실시간 시세 수신
- 거래대금 상위 스크리닝 → 조건 충족 시 자동 매수, 목표가/손절가 자동 매도
- 대시보드 UI로 포지션·체결 내역 모니터링

## 기술 스택
- C# / .NET / WPF (AutoTrader.sln)
- 한국투자증권 OpenAPI, WebSocket 실시간 시세

## 문서
`docs/` 폴더에 PRD·TRD, 증권사 API 분석, DB 스키마 등 설계 문서가 정리되어 있습니다.
