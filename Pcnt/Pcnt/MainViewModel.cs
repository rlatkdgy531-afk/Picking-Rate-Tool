using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Pcnt
{
    /// <summary>
    /// Picking Rate 화면용 ViewModel
    /// - 상태(State): 성공/실패, 실행여부, 시작/종료시각, Site/Outlet 선택
    /// - 파생 표시(Derived): 성공률, 요약 텍스트, 경과시간, 분당 처리량
    /// - 행동(Command): Start/Finish/Reset/Success/Fail/Save, Site 추가, Outlet 설정
    /// - 타이머: 1초마다 시간 관련 표시 갱신 (실행 중일 때만)
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        // ========= 1) 상태 (State) =========
        private int _success;
        private int _fail;
        private bool _isRunning;
        private DateTime? _t0;     // Start 시각
        private DateTime? _t1;     // Finish 시각 (Finish 시 고정)

        // Site/Outlet(드롭다운) 상태
        public ObservableCollection<string> Sites { get; } =
            new ObservableCollection<string> { "성남", "제주", "송도" };

        private string _selectedSite;
        public string SelectedSite
        {
            get => _selectedSite;
            set { _selectedSite = value; OnPropertyChanged(); RaiseCanExecutes(); }
        }

        private string _newSite;
        public string NewSite
        {
            get => _newSite;
            set { _newSite = value; OnPropertyChanged(); RaiseCanExecutes(); }
        }

        // XAML 바인딩명이 ItemsSource="{Binding Outlet}" 이므로 컬렉션 이름을 Outlet로 맞춤
        public ObservableCollection<string> Outlet { get; } =
            new ObservableCollection<string> { "PP", "PET", "PS" };

        private string _selectedOutlet;
        public string SelectedOutlet
        {
            get => _selectedOutlet;
            set { _selectedOutlet = value; OnPropertyChanged(); RaiseCanExecutes(); }
        }

        private string _newOutlet;
        public string NewOutlet
        {
            get => _newOutlet;
            set { _newOutlet = value; OnPropertyChanged(); RaiseCanExecutes(); }
        }

        // UI 타이머: UI 스레드에서 안전하게 Tick됨
        private readonly DispatcherTimer _timer;

        // ========= 2) 파생 표시 (Derived Properties) =========
        public int SuccessCount => _success;
        public int FailCount => _fail;
        public int TotalCount => _success + _fail;

        public double SuccessRate => TotalCount == 0 ? 0.0 : (double)_success / TotalCount * 100.0;

        public string SummaryText => $"성공률: {SuccessRate:F1}% ({SuccessCount}/{TotalCount})";

        private TimeSpan CurrentElapsed =>
            _t0 is null ? TimeSpan.Zero : ((_t1 ?? DateTime.Now) - _t0.Value);

        public string ElapsedText => $"경과시간 {CurrentElapsed:hh\\:mm\\:ss}";

        private double ItemsPerMinute
        {
            get
            {
                var sec = CurrentElapsed.TotalSeconds;
                if (sec <= 0) return 0.0;
                return TotalCount * 60.0 / sec;
            }
        }
        public string ThroughputText => $"분당 처리량 {ItemsPerMinute:F1} items/min";

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                OnPropertyChanged();     // IsRunning 바인딩 갱신
                RaiseCanExecutes();      // 버튼 활성/비활성 즉시 재평가
            }
        }

        // ========= 3) 커맨드 (Commands) =========
        public ICommand StartCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SuccessCommand { get; }
        public ICommand FailCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand AddSiteCommand { get; }
        public ICommand SetOutletCommand { get; }

        // ========= 4) 생성자 =========
        public MainViewModel()
        {
            // 1초마다 경과/처리량 텍스트 갱신
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) => RaiseTimeRelated();

            // 실행/정지/리셋
            StartCommand = new RelayCommand(_ => Start(), _ => !IsRunning);
            FinishCommand = new RelayCommand(_ => Finish(), _ => IsRunning);
            ResetCommand = new RelayCommand(_ => Reset(), _ => true);

            // 카운트(실행 중에만)
            SuccessCommand = new RelayCommand(_ => OnSuccess(), _ => IsRunning);
            FailCommand = new RelayCommand(_ => OnFail(), _ => IsRunning);

            // 저장(Finish 후, Site/Outlet 선택되어 있고 Total > 0)
            SaveCommand = new RelayCommand(_ => SaveCsv(), _ => CanSave());

            // Site 추가(빈칸 금지)
            AddSiteCommand = new RelayCommand(_ => AddSite(), _ => !string.IsNullOrWhiteSpace(NewSite));

            // Outlet 설정(의미: NewOutlet을 목록에 추가하고 선택)
            SetOutletCommand = new RelayCommand(_ => SetOutlet(), _ => !string.IsNullOrWhiteSpace(NewOutlet));

            // 초기 표시
            RaiseAll();
        }

        // ========= 5) 로직 (Actions) =========
        private void Start()
        {
            if (IsRunning) return;
            _t0 = DateTime.Now;
            _t1 = null;               // Finish 전까지는 실시간 경과
            IsRunning = true;
            _timer.Start();
            RaiseTimeRelated();       // 즉시 1회 갱신
        }

        private void Finish()
        {
            if (!IsRunning) return;
            _t1 = DateTime.Now;       // 종료 시각 고정 → 이후 경과/처리량 고정됨
            IsRunning = false;
            _timer.Stop();
            RaiseTimeRelated();       // 종료 시점 값으로 갱신
        }

        private void Reset()
        {
            _success = 0;
            _fail = 0;
            _t0 = null;
            _t1 = null;
            IsRunning = false;
            _timer.Stop();
            RaiseAll();               // 모든 표시 리프레시
        }

        private void OnSuccess()
        {
            _success++;
            RaiseCountsAndDerived();
            RaiseTimeRelated();
        }

        private void OnFail()
        {
            _fail++;
            RaiseCountsAndDerived();
            RaiseTimeRelated();
        }

        private void AddSite()
        {
            var s = (NewSite ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return;
            if (!Sites.Contains(s)) Sites.Add(s);
            SelectedSite = s;         // 추가와 동시에 선택
            NewSite = string.Empty;   // 입력 비움
        }

        private void SetOutlet()
        {
            var o = (NewOutlet ?? "").Trim();
            if (string.IsNullOrEmpty(o)) return;
            if (!Outlet.Contains(o)) Outlet.Add(o);
            SelectedOutlet = o;       // 추가와 동시에 선택
            NewOutlet = string.Empty;
        }

        // ========= 6) 저장 (CSV Append) =========
        private bool CanSave()
        {
            // Finish 이후(실행 중이 아니고), 카운트가 있고, Site/Outlet이 선택되어 있을 때
            return !IsRunning
                   && TotalCount > 0
                   && !string.IsNullOrWhiteSpace(SelectedSite)
                   && !string.IsNullOrWhiteSpace(SelectedOutlet);
        }

        private void SaveCsv()
        {
            try
            {
                if (!CanSave()) return;

                // 경로: 문서\PickingRateLogs\{Site}\{yyyy}\{yyyyMM}\yyyyMMdd_{Site}.csv
                var site = SanitizeForPath(SelectedSite);
                var now = _t1 ?? DateTime.Now;
                var y = now.ToString("yyyy");
                var ym = now.ToString("yyyyMM");
                var ymd = now.ToString("yyyyMMdd");

                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PickingRateLogs");
                var dir = Path.Combine(root, site, y, ym);
                Directory.CreateDirectory(dir);

                var file = Path.Combine(dir, $"{ymd}_{site}.csv");
                var hasHeader = File.Exists(file) && new FileInfo(file).Length > 0;

                var guid8 = Guid.NewGuid().ToString("N").Substring(0, 8);
                var sessionId = $"{ymd}-{DateTime.Now:HHmmss}-{guid8}";

                var startIso = _t0?.ToString("s") ?? "";
                var endIso = _t1?.ToString("s") ?? "";
                var duration = CurrentElapsed.ToString(@"hh\:mm\:ss");
                var ratePct = SuccessRate.ToString("F1");
                var ipm = ItemsPerMinute.ToString("F1");

                var sb = new StringBuilder();
                if (!hasHeader)
                {
                    sb.AppendLine("SessionId,Site,Outlet,StartTime,EndTime,Success,Fail,Total,SuccessRate(%),ItemsPerMinute,Duration,AppVersion");
                }
                sb.AppendLine(string.Join(",",
                    Quote(sessionId),
                    Quote(site),
                    Quote(SelectedOutlet ?? ""),
                    Quote(startIso),
                    Quote(endIso),
                    SuccessCount,
                    FailCount,
                    TotalCount,
                    ratePct,
                    ipm,
                    Quote(duration),
                    "1.0.0"
                ));

                // UTF-8 with BOM: 엑셀/메모장 모두 잘 열림
                File.AppendAllText(file, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                MessageBox.Show("CSV 저장 완료", "Save", MessageBoxButton.OK, MessageBoxImage.Information);

                // (선택) 한 번 저장 후 다시 저장 못 하게 막고 싶으면 주석 해제
                // _t0 = null; _t1 = null; _success = _fail = 0; RaiseAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 실패: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string SanitizeForPath(string name)
        {
            var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var rx = new Regex($"[{Regex.Escape(invalid)}]");
            var cleaned = rx.Replace(name ?? "", "_").Trim();
            return string.IsNullOrEmpty(cleaned) ? "SITE" : cleaned;
        }

        private static string Quote(object v)
        {
            // CSV 안전하게: 콤마/따옴표 포함 시 감싸고 이스케이프
            var s = v?.ToString() ?? "";
            if (s.Contains(",") || s.Contains("\""))
                s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        // ========= 7) 변경 알림 묶음 =========
        private void RaiseCountsAndDerived()
        {
            OnPropertyChanged(nameof(SuccessCount));
            OnPropertyChanged(nameof(FailCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(SuccessRate));
            OnPropertyChanged(nameof(SummaryText));
        }

        private void RaiseTimeRelated()
        {
            OnPropertyChanged(nameof(ElapsedText));
            OnPropertyChanged(nameof(ThroughputText));
        }

        private void RaiseAll()
        {
            RaiseCountsAndDerived();
            RaiseTimeRelated();
            RaiseCanExecutes();
        }

        private void RaiseCanExecutes()
        {
            (StartCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FinishCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SuccessCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FailCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddSiteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SetOutletCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // ========= 8) INotifyPropertyChanged =========
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 단순 ICommand 구현체 (동기용)
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
