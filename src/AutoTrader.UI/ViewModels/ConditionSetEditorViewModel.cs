using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Market;
using AutoTrader.UI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace AutoTrader.UI.ViewModels
{
    /// <summary>
    /// 조건식 설정 ViewModel
    /// </summary>
    public class ConditionSetEditorViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ConditionSetRepository _conditionSetRepository;
        private readonly KisMarketDataService _marketDataService;
        private int? _accountId;
        private int? _conditionSetId;

        // DI 생성자 사용 권장 (현재는 직접 생성)
        public ConditionSetEditorViewModel()
        {
            // Configuration 로드 (실행 파일 위치 기준)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // DbContext 초기화 (appsettings.json에서 데이터베이스 타입 읽기)
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var databaseType = configuration.GetValue<string>("ConnectionStrings:DatabaseType") ?? "SQLite";

            switch (databaseType.ToUpper())
            {
                case "SQLITE":
                    optionsBuilder.UseSqlite("Data Source=autotrader.db");
                    break;
                case "MYSQL":
                    var mysqlConnectionString = configuration.GetConnectionString("DefaultConnection");
                    var serverVersion = ServerVersion.AutoDetect(mysqlConnectionString);
                    optionsBuilder.UseMySql(mysqlConnectionString, serverVersion);
                    break;
                case "POSTGRESQL":
                case "POSTGRES":
                    var postgresConnectionString = configuration.GetConnectionString("DefaultConnection");
                    optionsBuilder.UseNpgsql(postgresConnectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database type: {databaseType}");
            }

            _dbContext = new AppDbContext(optionsBuilder.Options);

            // Repository 초기화
            _conditionSetRepository = new ConditionSetRepository(_dbContext);

            // Service 초기화 (TODO: DI로 변경 필요)
            // _marketDataService = new KisMarketDataService(...); 
            // 현재는 null 상태로 두고, 실제 런타임에서는 DI를 통해 주입받거나 해야 함.
            // 데모 목적상 null 체크 후 동작하도록 수정.
        }

        // DI 생성자 추가
        public ConditionSetEditorViewModel(
            KisMarketDataService marketDataService,
            ConditionSetRepository conditionSetRepository)
        {
            _marketDataService = marketDataService;
            _conditionSetRepository = conditionSetRepository;

            Conditions = new ObservableCollection<ConditionItemViewModel>();
            FormulaTokens = new ObservableCollection<FormulaToken>();
            MatchedStocks = new ObservableCollection<StockPreviewItem>();

            LoadPreviewData();
        }

        #region Properties

        /// <summary>
        /// 조건 목록
        /// </summary>
        public ObservableCollection<ConditionItemViewModel> Conditions { get; } = new ObservableCollection<ConditionItemViewModel>();

        /// <summary>
        /// 조건식 토큰 목록
        /// </summary>
        public ObservableCollection<FormulaToken> FormulaTokens { get; } = new ObservableCollection<FormulaToken>();

        /// <summary>
        /// 조건 충족 종목 미리보기 목록
        /// </summary>
        public ObservableCollection<StockPreviewItem> MatchedStocks { get; } = new ObservableCollection<StockPreviewItem>();

        #endregion

        #region Methods

        /// <summary>
        /// 특정 계좌의 조건식 로드
        /// </summary>
        public async void LoadConditionSet(int accountId)
        {
            _accountId = accountId;

            try
            {
                var conditionSet = await _conditionSetRepository.GetConditionSetByAccountIdAsync(accountId);
                if (conditionSet != null)
                {
                    _conditionSetId = conditionSet.ConditionSetId;

                    // 조건 목록 로드
                    Conditions.Clear();
                    foreach (var condition in conditionSet.Conditions.OrderBy(c => c.ConditionOrder))
                    {
                        var conditionItem = new ConditionItemViewModel
                        {
                            ConditionId = GetConditionIdLetter(condition.ConditionOrder),
                            ConditionType = condition.ConditionType,
                            Description = GetConditionDescription(condition),
                            IsEnabled = true
                        };
                        Conditions.Add(conditionItem);
                    }

                    // 조건식 파싱 (TODO: 실제 조건식 파싱 로직 구현)
                    ParseFormula("G and H and I and J and K");
                }
            }
            catch (Exception ex)
            {
                // 로그 출력
                Console.WriteLine($"조건식 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 조건 추가
        /// </summary>
        public void AddCondition(ConditionItemViewModel condition)
        {
            condition.ConditionId = GetConditionIdLetter(Conditions.Count);
            Conditions.Add(condition);
        }

        /// <summary>
        /// 조건 제거
        /// </summary>
        public void RemoveCondition(ConditionItemViewModel condition)
        {
            Conditions.Remove(condition);

            // 조건 ID 재할당
            for (int i = 0; i < Conditions.Count; i++)
            {
                Conditions[i].ConditionId = GetConditionIdLetter(i);
            }

            // 조건식에서도 제거
            RemoveConditionFromFormula(condition.ConditionId);
        }

        /// <summary>
        /// 조건 위로 이동
        /// </summary>
        public void MoveConditionUp(ConditionItemViewModel condition)
        {
            int index = Conditions.IndexOf(condition);
            if (index > 0)
            {
                Conditions.Move(index, index - 1);

                // 조건 ID 재할당
                for (int i = 0; i < Conditions.Count; i++)
                {
                    Conditions[i].ConditionId = GetConditionIdLetter(i);
                }
            }
        }

        /// <summary>
        /// 조건 아래로 이동
        /// </summary>
        public void MoveConditionDown(ConditionItemViewModel condition)
        {
            int index = Conditions.IndexOf(condition);
            if (index < Conditions.Count - 1)
            {
                Conditions.Move(index, index + 1);

                // 조건 ID 재할당
                for (int i = 0; i < Conditions.Count; i++)
                {
                    Conditions[i].ConditionId = GetConditionIdLetter(i);
                }
            }
        }

        /// <summary>
        /// 조건식 토큰 제거
        /// </summary>
        public void RemoveFormulaToken(FormulaToken token)
        {
            FormulaTokens.Remove(token);
        }

        /// <summary>
        /// 연산자 업데이트
        /// </summary>
        public void UpdateOperator(FormulaToken token, string newOperator)
        {
            token.Value = newOperator;
        }

        /// <summary>
        /// 여는 괄호 추가
        /// </summary>
        public void AddOpenParen()
        {
            FormulaTokens.Add(new FormulaToken(FormulaTokenType.OpenParen, "("));
        }

        /// <summary>
        /// 닫는 괄호 추가
        /// </summary>
        public void AddCloseParen()
        {
            FormulaTokens.Add(new FormulaToken(FormulaTokenType.CloseParen, ")"));
        }

        /// <summary>
        /// 마지막 토큰 제거
        /// </summary>
        public void RemoveLastToken()
        {
            if (FormulaTokens.Count > 0)
            {
                FormulaTokens.RemoveAt(FormulaTokens.Count - 1);
            }
        }

        /// <summary>
        /// 조건식 미리보기 생성
        /// </summary>
        public string GetFormulaPreview()
        {
            if (FormulaTokens.Count == 0)
                return "조건식이 비어있습니다";

            var sb = new StringBuilder();
            foreach (var token in FormulaTokens)
            {
                sb.Append(token.Value);
                if (token.Type != FormulaTokenType.OpenParen && token.Type != FormulaTokenType.CloseParen)
                {
                    sb.Append(" ");
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// 조건식 유효성 검사
        /// </summary>
        public bool ValidateFormula()
        {
            // 괄호 균형 검사
            int openCount = FormulaTokens.Count(t => t.Type == FormulaTokenType.OpenParen);
            int closeCount = FormulaTokens.Count(t => t.Type == FormulaTokenType.CloseParen);

            if (openCount != closeCount)
                return false;

            // TODO: 추가 유효성 검사 (연산자 연속 등)

            return true;
        }

        /// <summary>
        /// 조건식 저장
        /// </summary>
        public async void SaveConditionSet()
        {
            if (!_accountId.HasValue)
                throw new InvalidOperationException("계좌 ID가 설정되지 않았습니다.");

            // ConditionSet 생성
            var conditionSet = new ConditionSet
            {
                ConditionSetId = _conditionSetId ?? 0,
                AccountId = _accountId.Value,
                Name = "조건식1",
                Conditions = new List<Core.Models.Database.Condition>()
            };

            // Condition 목록 생성
            for (int i = 0; i < Conditions.Count; i++)
            {
                var condition = Conditions[i];
                var dbCondition = new Core.Models.Database.Condition
                {
                    ConditionOrder = i + 1,
                    ConditionType = condition.ConditionType,
                    Parameters = System.Text.Json.JsonSerializer.Serialize(condition.Parameters ?? new Dictionary<string, object>()),
                    LogicOperator = i < Conditions.Count - 1 ? "AND" : null
                };
                conditionSet.Conditions.Add(dbCondition);
            }

            // DB에 저장
            await _conditionSetRepository.UpsertConditionSetAsync(_accountId.Value, conditionSet.Name, conditionSet.Conditions.ToList());
        }

        /// <summary>
        /// 조건 충족 종목 미리보기 데이터 로드
        /// </summary>
        public async void LoadPreviewData()
        {
            if (_marketDataService == null) return;

            try
            {
                // 실제 API 호출하여 거래대금 상위 300 종목 가져오기
                var stocks = await _marketDataService.GetTop300StocksAsync();

                MatchedStocks.Clear();

                if (stocks != null && stocks.Any())
                {
                    // 조건식 평가하여 충족 종목 필터링 (임시로 전체 표시)
                    foreach (var stock in stocks.Take(100)) // 상위 100개만 표시
                    {
                        MatchedStocks.Add(new StockPreviewItem
                        {
                            Rank = stock.Rank,
                            Symbol = stock.Symbol,
                            Name = stock.Name,
                            CurrentPrice = stock.CurrentPrice,
                            ChangePercent = (double)stock.ChangePercent,
                            TradeVolume = stock.TradeVolume
                        });
                    }
                }
                else
                {
                    // API 연결 실패 시 임시 데이터 표시
                    MatchedStocks.Add(new StockPreviewItem
                    {
                        Rank = 0,
                        Symbol = "INFO",
                        Name = "데이터가 없습니다.",
                        CurrentPrice = 0m,
                        ChangePercent = 0,
                        TradeVolume = 0m
                    });
                }
            }
            catch (Exception ex)
            {
                // 오류 발생 시 오류 메시지 표시
                MatchedStocks.Clear();
                MatchedStocks.Add(new StockPreviewItem
                {
                    Rank = 0,
                    Symbol = "ERROR",
                    Name = $"데이터 로드 실패: {ex.Message}",
                    CurrentPrice = 0m,
                    ChangePercent = 0,
                    TradeVolume = 0m
                });
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 조건 순서를 알파벳 ID로 변환
        /// </summary>
        private string GetConditionIdLetter(int order)
        {
            if (order < 0 || order >= 26)
                return "?";

            return ((char)('A' + order)).ToString();
        }

        /// <summary>
        /// 조건 설명 생성
        /// </summary>
        private string GetConditionDescription(Core.Models.Database.Condition condition)
        {
            // TODO: 실제 조건 파라미터 파싱하여 설명 생성
            return $"{condition.ConditionType} 조건";
        }

        /// <summary>
        /// 조건식 파싱
        /// </summary>
        private void ParseFormula(string formula)
        {
            FormulaTokens.Clear();

            // 간단한 파싱 로직 (공백 기준)
            var tokens = formula.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (token == "(")
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.OpenParen, "("));
                }
                else if (token == ")")
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.CloseParen, ")"));
                }
                else if (token.ToLower() == "and" || token.ToLower() == "or")
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.Operator, token.ToLower()));
                }
                else if (token.Length == 1 && char.IsLetter(token[0]))
                {
                    FormulaTokens.Add(new FormulaToken(FormulaTokenType.ConditionId, token.ToUpper()));
                }
            }
        }

        /// <summary>
        /// 조건식에서 특정 조건 제거
        /// </summary>
        private void RemoveConditionFromFormula(string conditionId)
        {
            var tokensToRemove = FormulaTokens.Where(t => t.Type == FormulaTokenType.ConditionId && t.Value == conditionId).ToList();
            foreach (var token in tokensToRemove)
            {
                FormulaTokens.Remove(token);
            }
        }

        #endregion
    }
}
