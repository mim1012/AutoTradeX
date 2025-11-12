# 📊 AutoTradeX 로컬 DB 스키마 v1.0

## 🎯 설계 목표

여러 개의 한국투자증권 계좌를 등록하고, 각 계좌마다 독립적인 조건식을 저장하여 활성 계좌 변경 시 자동으로 조건식을 불러올 수 있는 로컬 DB를 설계한다.

### 핵심 요구사항

1. ✅ **다중 계좌 관리**: 여러 개의 한국투자증권 계좌 등록
2. ✅ **활성 계좌 최대 1개**: 라디오 버튼으로 선택
3. ✅ **계좌별 조건식 저장**: 각 계좌마다 1개의 조건식 (최대 10개 조건)
4. ✅ **로컬 DB**: SQLite 사용
5. ✅ **실전 계좌만 지원**: 한국투자증권은 해외주식 모의투자 미지원

---

## 📊 ERD (Entity Relationship Diagram)

```
┌─────────────────────────────────────┐
│         Accounts (계좌)              │
├─────────────────────────────────────┤
│ AccountId (PK)                      │
│ AccountNumber (UNIQUE)              │
│ AccountName                         │
│ AppKey                              │
│ AppSecret                           │
│ IsActive (최대 1개만 1)              │
│ BuyRatio                            │
│ ExcludeOwnedStocks                  │
│ CreatedAt                           │
│ UpdatedAt                           │
└─────────────────────────────────────┘
           │
           │ 1:1
           │
           ▼
┌─────────────────────────────────────┐
│    ConditionSets (조건식)            │
├─────────────────────────────────────┤
│ ConditionSetId (PK)                 │
│ AccountId (FK)                      │
│ Name                                │
│ CreatedAt                           │
│ UpdatedAt                           │
└─────────────────────────────────────┘
           │
           │ 1:N (최대 10개)
           │
           ▼
┌─────────────────────────────────────┐
│       Conditions (조건)              │
├─────────────────────────────────────┤
│ ConditionId (PK)                    │
│ ConditionSetId (FK)                 │
│ ConditionOrder                      │
│ ConditionType                       │
│ Parameters (JSON)                   │
│ LogicOperator (AND/OR)              │
│ CreatedAt                           │
│ UpdatedAt                           │
└─────────────────────────────────────┘
```

---

## 📋 테이블 상세 설계

### 1. Accounts (계좌 테이블)

**목적:** 한국투자증권 계좌 정보 저장

```sql
CREATE TABLE Accounts (
    AccountId INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountNumber TEXT NOT NULL UNIQUE,  -- 계좌번호 (예: 12345678-01)
    AccountName TEXT,                     -- 계좌 별칭 (예: "메인 계좌")
    AppKey TEXT NOT NULL,                 -- KIS API AppKey
    AppSecret TEXT NOT NULL,              -- KIS API AppSecret (암호화 저장 권장)
    IsActive INTEGER DEFAULT 0,           -- 활성 계좌 여부 (0 or 1, 최대 1개만 1)
    BuyRatio REAL DEFAULT 100.0,          -- 매수 비율 (0.0 ~ 100.0)
    ExcludeOwnedStocks INTEGER DEFAULT 0, -- 관심종목 제외 옵션 (0 or 1)
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 인덱스
CREATE UNIQUE INDEX idx_accounts_number ON Accounts(AccountNumber);
CREATE INDEX idx_accounts_active ON Accounts(IsActive);
```

**컬럼 설명:**

| 컬럼명 | 타입 | 제약 조건 | 설명 |
|--------|------|----------|------|
| `AccountId` | INTEGER | PRIMARY KEY | 계좌 고유 ID (자동 증가) |
| `AccountNumber` | TEXT | NOT NULL, UNIQUE | 한국투자증권 계좌번호 (예: 12345678-01) |
| `AccountName` | TEXT | - | 사용자 지정 계좌 별칭 (예: "메인 계좌") |
| `AppKey` | TEXT | NOT NULL | KIS API AppKey |
| `AppSecret` | TEXT | NOT NULL | KIS API AppSecret (암호화 저장 권장) |
| `IsActive` | INTEGER | DEFAULT 0 | 활성 계좌 여부 (0: 비활성, 1: 활성, 최대 1개만 1) |
| `BuyRatio` | REAL | DEFAULT 100.0 | 매수 비율 (0.0 ~ 100.0) |
| `ExcludeOwnedStocks` | INTEGER | DEFAULT 0 | 관심종목 제외 옵션 (0: 미사용, 1: 사용) |
| `CreatedAt` | TEXT | DEFAULT CURRENT_TIMESTAMP | 생성 시간 (ISO 8601 형식) |
| `UpdatedAt` | TEXT | DEFAULT CURRENT_TIMESTAMP | 수정 시간 (ISO 8601 형식) |

**제약 조건:**
- `AccountNumber`는 UNIQUE (중복 계좌 등록 방지)
- `IsActive`는 최대 1개만 1 (트리거로 강제)

**트리거: 활성 계좌 최대 1개 강제**

```sql
-- 새로운 계좌를 활성화할 때 기존 활성 계좌를 자동으로 비활성화
CREATE TRIGGER enforce_single_active_account_insert
AFTER INSERT ON Accounts
WHEN NEW.IsActive = 1
BEGIN
    UPDATE Accounts SET IsActive = 0 WHERE AccountId != NEW.AccountId;
END;

-- 계좌를 활성화할 때 기존 활성 계좌를 자동으로 비활성화
CREATE TRIGGER enforce_single_active_account_update
AFTER UPDATE ON Accounts
WHEN NEW.IsActive = 1
BEGIN
    UPDATE Accounts SET IsActive = 0 WHERE AccountId != NEW.AccountId;
END;
```

---

### 2. ConditionSets (조건식 테이블)

**목적:** 각 계좌의 조건식 메타데이터 저장

```sql
CREATE TABLE ConditionSets (
    ConditionSetId INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountId INTEGER NOT NULL,           -- 외래키: Accounts.AccountId
    Name TEXT DEFAULT '조건식1',          -- 조건식 이름
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId) ON DELETE CASCADE
);

-- 인덱스
CREATE INDEX idx_conditionsets_account ON ConditionSets(AccountId);
```

**컬럼 설명:**

| 컬럼명 | 타입 | 제약 조건 | 설명 |
|--------|------|----------|------|
| `ConditionSetId` | INTEGER | PRIMARY KEY | 조건식 고유 ID (자동 증가) |
| `AccountId` | INTEGER | NOT NULL, FK | 외래키: Accounts.AccountId |
| `Name` | TEXT | DEFAULT '조건식1' | 조건식 이름 |
| `CreatedAt` | TEXT | DEFAULT CURRENT_TIMESTAMP | 생성 시간 (ISO 8601 형식) |
| `UpdatedAt` | TEXT | DEFAULT CURRENT_TIMESTAMP | 수정 시간 (ISO 8601 형식) |

**제약 조건:**
- `AccountId`는 Accounts 테이블의 외래키
- `ON DELETE CASCADE`: 계좌 삭제 시 연결된 조건식도 자동 삭제

---

### 3. Conditions (조건 테이블)

**목적:** 조건식을 구성하는 개별 조건 저장

```sql
CREATE TABLE Conditions (
    ConditionId INTEGER PRIMARY KEY AUTOINCREMENT,
    ConditionSetId INTEGER NOT NULL,      -- 외래키: ConditionSets.ConditionSetId
    ConditionOrder INTEGER NOT NULL,      -- 조건 순서 (1, 2, 3, ...)
    ConditionType TEXT NOT NULL,          -- 조건 타입 (PriceChange, MovingAverage, TradeVolume, PriceComparison)
    Parameters TEXT NOT NULL,             -- JSON 형태의 파라미터
    LogicOperator TEXT,                   -- 다음 조건과의 논리 연산자 (AND, OR, NULL for last)
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ConditionSetId) REFERENCES ConditionSets(ConditionSetId) ON DELETE CASCADE
);

-- 인덱스
CREATE INDEX idx_conditions_conditionset ON Conditions(ConditionSetId);
CREATE INDEX idx_conditions_order ON Conditions(ConditionSetId, ConditionOrder);
```

**컬럼 설명:**

| 컬럼명 | 타입 | 제약 조건 | 설명 |
|--------|------|----------|------|
| `ConditionId` | INTEGER | PRIMARY KEY | 조건 고유 ID (자동 증가) |
| `ConditionSetId` | INTEGER | NOT NULL, FK | 외래키: ConditionSets.ConditionSetId |
| `ConditionOrder` | INTEGER | NOT NULL | 조건 순서 (1, 2, 3, ..., 최대 10) |
| `ConditionType` | TEXT | NOT NULL | 조건 타입 (PriceChange, MovingAverage, TradeVolume, PriceComparison) |
| `Parameters` | TEXT | NOT NULL | JSON 형태의 파라미터 (조건 타입별 상이) |
| `LogicOperator` | TEXT | - | 다음 조건과의 논리 연산자 (AND, OR, 마지막 조건은 NULL) |
| `CreatedAt` | TEXT | DEFAULT CURRENT_TIMESTAMP | 생성 시간 (ISO 8601 형식) |
| `UpdatedAt` | TEXT | DEFAULT CURRENT_TIMESTAMP | 수정 시간 (ISO 8601 형식) |

**제약 조건:**
- `ConditionSetId`는 ConditionSets 테이블의 외래키
- `ON DELETE CASCADE`: 조건식 삭제 시 연결된 조건도 자동 삭제
- `ConditionOrder`는 1부터 시작하며 최대 10

---

## 📝 조건 타입별 Parameters JSON 스키마

### 1. PriceChange (등락률)

**설명:** 특정 봉 대비 등락률 조건

**JSON 스키마:**
```json
{
  "candle": "daily",        // 기준봉: daily, weekly, monthly
  "offset": 0,              // 몇 봉 전 (0: 현재봉, 1: 1봉 전, ...)
  "range_min": -7.0,        // 최소 등락률 (%)
  "range_max": 0.0          // 최대 등락률 (%)
}
```

**예시:**
```json
{
  "candle": "daily",
  "offset": 0,
  "range_min": -7.0,
  "range_max": 0.0
}
```
→ "일봉 0봉 전 종가 대비 -7.0% ~ 0.0%"

---

### 2. MovingAverage (이동평균선)

**설명:** 현재가와 이동평균선 대비 등락률 조건

**JSON 스키마:**
```json
{
  "period": 20,             // 이동평균선 기간 (일)
  "range_min": -3.0,        // 최소 등락률 (%)
  "range_max": 3.0          // 최대 등락률 (%)
}
```

**예시:**
```json
{
  "period": 20,
  "range_min": -3.0,
  "range_max": 3.0
}
```
→ "현재가가 20일 이평선 대비 -3.0% ~ +3.0% 이내"

---

### 3. TradeVolume (거래대금)

**설명:** 거래대금 조건

**JSON 스키마:**
```json
{
  "min_amount": 10000000.0  // 최소 거래대금 (달러)
}
```

**예시:**
```json
{
  "min_amount": 10000000.0
}
```
→ "거래대금 1,000만 달러 이상"

---

### 4. PriceComparison (주가 비교)

**설명:** 특정 봉의 가격(시가/고가/저가/종가) 비교 조건

**JSON 스키마:**
```json
{
  "left_candle": 0,         // 좌측 봉 (0: 현재봉, 1: 1봉 전, ...)
  "left_price": "open",     // 좌측 가격 (open, high, low, close)
  "operator": ">",          // 비교 연산자 (>, <, >=, <=, ==)
  "right_candle": 1,        // 우측 봉
  "right_price": "low"      // 우측 가격
}
```

**예시:**
```json
{
  "left_candle": 0,
  "left_price": "open",
  "operator": ">",
  "right_candle": 1,
  "right_price": "low"
}
```
→ "0봉 시가 > 1봉 저가"

---

## 🔧 데이터 예시

### 예시 1: 계좌 2개 등록

```sql
INSERT INTO Accounts (AccountNumber, AccountName, AppKey, AppSecret, IsActive, BuyRatio, ExcludeOwnedStocks)
VALUES 
  ('12345678-01', '메인 계좌', 'PSxxx...', 'yyy...', 0, 100.0, 0),
  ('87654321-01', '서브 계좌', 'PSaaa...', 'bbb...', 1, 100.0, 1);
```

**결과:**
- 계좌1: 비활성 (IsActive = 0)
- 계좌2: 활성 (IsActive = 1) ← 현재 자동매매 실행 중

---

### 예시 2: 계좌1의 조건식 (3개 조건)

**ConditionSets:**
```sql
INSERT INTO ConditionSets (AccountId, Name)
VALUES (1, '조건식1');
```

**Conditions:**
```sql
INSERT INTO Conditions (ConditionSetId, ConditionOrder, ConditionType, Parameters, LogicOperator)
VALUES 
  (1, 1, 'PriceChange', '{"candle":"daily","offset":0,"range_min":-7.0,"range_max":0.0}', 'AND'),
  (1, 2, 'PriceChange', '{"candle":"weekly","offset":1,"range_min":5.0,"range_max":999.0}', 'OR'),
  (1, 3, 'MovingAverage', '{"period":20,"range_min":-3.0,"range_max":3.0}', NULL);
```

**논리식:**
```
(조건1 AND 조건2) OR 조건3
```

---

### 예시 3: 계좌2의 조건식 (2개 조건)

**ConditionSets:**
```sql
INSERT INTO ConditionSets (AccountId, Name)
VALUES (2, '조건식1');
```

**Conditions:**
```sql
INSERT INTO Conditions (ConditionSetId, ConditionOrder, ConditionType, Parameters, LogicOperator)
VALUES 
  (2, 1, 'TradeVolume', '{"min_amount":10000000.0}', 'AND'),
  (2, 2, 'PriceComparison', '{"left_candle":0,"left_price":"open","operator":">","right_candle":1,"right_price":"low"}', NULL);
```

**논리식:**
```
조건1 AND 조건2
```

---

## 🔒 보안 고려사항

### AppSecret 암호화

**권장 사항:**
- `AppSecret`은 평문으로 저장하지 말고 암호화 저장
- .NET의 `System.Security.Cryptography.ProtectedData` 사용 권장
- 또는 `AES-256` 암호화 사용

**예시 코드 (C#):**
```csharp
using System.Security.Cryptography;
using System.Text;

public static string EncryptString(string plainText)
{
    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
    byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
    return Convert.ToBase64String(encryptedBytes);
}

public static string DecryptString(string encryptedText)
{
    byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
    byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(plainBytes);
}
```

---

## 📦 DB 초기화 스크립트

**파일명:** `init_database.sql`

```sql
-- 테이블 생성
CREATE TABLE IF NOT EXISTS Accounts (
    AccountId INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountNumber TEXT NOT NULL UNIQUE,
    AccountName TEXT,
    AppKey TEXT NOT NULL,
    AppSecret TEXT NOT NULL,
    IsActive INTEGER DEFAULT 0,
    BuyRatio REAL DEFAULT 100.0,
    ExcludeOwnedStocks INTEGER DEFAULT 0,
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ConditionSets (
    ConditionSetId INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountId INTEGER NOT NULL,
    Name TEXT DEFAULT '조건식1',
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Conditions (
    ConditionId INTEGER PRIMARY KEY AUTOINCREMENT,
    ConditionSetId INTEGER NOT NULL,
    ConditionOrder INTEGER NOT NULL,
    ConditionType TEXT NOT NULL,
    Parameters TEXT NOT NULL,
    LogicOperator TEXT,
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ConditionSetId) REFERENCES ConditionSets(ConditionSetId) ON DELETE CASCADE
);

-- 인덱스 생성
CREATE UNIQUE INDEX IF NOT EXISTS idx_accounts_number ON Accounts(AccountNumber);
CREATE INDEX IF NOT EXISTS idx_accounts_active ON Accounts(IsActive);
CREATE INDEX IF NOT EXISTS idx_conditionsets_account ON ConditionSets(AccountId);
CREATE INDEX IF NOT EXISTS idx_conditions_conditionset ON Conditions(ConditionSetId);
CREATE INDEX IF NOT EXISTS idx_conditions_order ON Conditions(ConditionSetId, ConditionOrder);

-- 트리거 생성: 활성 계좌 최대 1개 강제
CREATE TRIGGER IF NOT EXISTS enforce_single_active_account_insert
AFTER INSERT ON Accounts
WHEN NEW.IsActive = 1
BEGIN
    UPDATE Accounts SET IsActive = 0 WHERE AccountId != NEW.AccountId;
END;

CREATE TRIGGER IF NOT EXISTS enforce_single_active_account_update
AFTER UPDATE ON Accounts
WHEN NEW.IsActive = 1
BEGIN
    UPDATE Accounts SET IsActive = 0 WHERE AccountId != NEW.AccountId;
END;
```

---

## 🧪 테스트 쿼리

### 1. 활성 계좌 조회

```sql
SELECT * FROM Accounts WHERE IsActive = 1;
```

### 2. 특정 계좌의 조건식 조회

```sql
SELECT 
    c.ConditionOrder,
    c.ConditionType,
    c.Parameters,
    c.LogicOperator
FROM Conditions c
JOIN ConditionSets cs ON c.ConditionSetId = cs.ConditionSetId
WHERE cs.AccountId = 1
ORDER BY c.ConditionOrder;
```

### 3. 활성 계좌의 조건식 조회

```sql
SELECT 
    a.AccountNumber,
    a.AccountName,
    c.ConditionOrder,
    c.ConditionType,
    c.Parameters,
    c.LogicOperator
FROM Accounts a
JOIN ConditionSets cs ON a.AccountId = cs.AccountId
JOIN Conditions c ON cs.ConditionSetId = c.ConditionSetId
WHERE a.IsActive = 1
ORDER BY c.ConditionOrder;
```

### 4. 계좌 삭제 (CASCADE 테스트)

```sql
DELETE FROM Accounts WHERE AccountId = 1;
-- ConditionSets 및 Conditions도 자동 삭제됨
```

---

## ✅ 체크리스트

- [x] Accounts 테이블 설계
- [x] ConditionSets 테이블 설계
- [x] Conditions 테이블 설계
- [x] 활성 계좌 최대 1개 트리거
- [x] 외래키 CASCADE 설정
- [x] 조건 타입별 JSON 스키마 정의
- [x] 인덱스 설계
- [x] 보안 고려사항 (AppSecret 암호화)
- [x] DB 초기화 스크립트
- [x] 테스트 쿼리
- [x] 실전/모의 옵션 제거 (한투는 해외주식 실전만 지원)

---

## 📝 다음 단계

1. ✅ DB 스키마 설계 완료
2. ⏭️ C# Entity 모델 클래스 작성
3. ⏭️ SQLite 연동 Repository 클래스 작성
4. ⏭️ XAML UI 구현
5. ⏭️ ViewModel 및 DB 연동 구현
