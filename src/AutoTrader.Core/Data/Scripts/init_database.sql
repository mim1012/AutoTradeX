-- AutoTradeX 데이터베이스 초기화 스크립트
-- SQLite 3.x

-- ========================================
-- 테이블 생성
-- ========================================

-- Accounts 테이블
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

-- ConditionSets 테이블
CREATE TABLE IF NOT EXISTS ConditionSets (
    ConditionSetId INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountId INTEGER NOT NULL,
    Name TEXT DEFAULT '조건식1',
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId) ON DELETE CASCADE
);

-- Conditions 테이블
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

-- ========================================
-- 인덱스 생성
-- ========================================

CREATE UNIQUE INDEX IF NOT EXISTS idx_accounts_number ON Accounts(AccountNumber);
CREATE INDEX IF NOT EXISTS idx_accounts_active ON Accounts(IsActive);
CREATE INDEX IF NOT EXISTS idx_conditionsets_account ON ConditionSets(AccountId);
CREATE INDEX IF NOT EXISTS idx_conditions_conditionset ON Conditions(ConditionSetId);
CREATE INDEX IF NOT EXISTS idx_conditions_order ON Conditions(ConditionSetId, ConditionOrder);

-- ========================================
-- 트리거 생성: 활성 계좌 최대 1개 강제
-- ========================================

-- INSERT 시 활성 계좌 최대 1개 강제
CREATE TRIGGER IF NOT EXISTS enforce_single_active_account_insert
AFTER INSERT ON Accounts
WHEN NEW.IsActive = 1
BEGIN
    UPDATE Accounts SET IsActive = 0 WHERE AccountId != NEW.AccountId;
END;

-- UPDATE 시 활성 계좌 최대 1개 강제
CREATE TRIGGER IF NOT EXISTS enforce_single_active_account_update
AFTER UPDATE ON Accounts
WHEN NEW.IsActive = 1
BEGIN
    UPDATE Accounts SET IsActive = 0 WHERE AccountId != NEW.AccountId;
END;
