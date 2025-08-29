using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Pcnt
{
    /// <summary>
    /// 메인 화면에 바인딩되는 ViewModel.
    /// - 상태(State): 성공/실패 카운트, 실행여부, 시작/종료시각
    /// - 파생표시(Derived): 성공률/요약 텍스트, 경과시간, 분당 처리량
    /// - 행동(Command): Start/Finish/Reset/Success/Fail
    /// - 타이머: 1초마다 Elapsed/Throughput 갱신 (실행 중에만)
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        // -----------------------------
        // 1) 상태(State)
        // -----------------------------
        private int _success;
        private int _fail;
        private bool _isRunning;
        private DateTime? _t0;   // 시작 시각
        private DateTime? _t1;   // 종료 시각 (Finish 시 고정)

        // UI 스레드에서 안전하게 틱이 발생하는 타이머
        private readonly DispatcherTimer _timer;

        // -----------------------------
        // 2) 파생표시(Derived Properties)
        //    - UI에 직접 바인딩되는 읽기전용 get 속성들
        // -----------------------------
        public int SuccessCount => _success;
        public int FailCount => _fail;
        public int TotalCount => _success + _fail;

        public double SuccessRate => TotalCount == 0
            ? 0.0
            : (double)_success / TotalCount * 100.0;

        public string SummaryText =>
            $"성공률: {SuccessRate:F1}% ({SuccessCount}/{TotalCount})";

        // 경과시간 계산: 시작 전이면 0, 실행 중이면 현재-시작, 종료 후면 종료-시작
        private TimeSpan CurrentElapsed =>
            _t0 is null ? TimeSpan.Zero : ((_t1 ?? DateTime.Now) - _t0.Value);

        public string ElapsedText => $"경과시간 {CurrentElapsed:hh\\:mm\\:ss}";

        // 분당 처리량: 총 처리 건수 / (경과초/60)
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

        // 실행중 여부(상태). 값이 바뀔 때 CanExecute도 함께 갱신해야 버튼 활성/비활성이 즉시 반영됨.
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                OnPropertyChanged();   // IsRunning 바인딩 갱신
                RaiseCanExecutes();    // 관련 커맨드 활성 조건 즉시 갱신
            }
        }

        // -----------------------------
        // 3) 커맨드(Command = 행동)
        // -----------------------------
        public ICommand StartCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SuccessCommand { get; }
        public ICommand FailCommand { get; }

        public MainViewModel()
        {
            // 타이머: 1초 간격으로 시간/처리량 텍스트만 갱신
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) => RaiseTimeRelated();

            // 각 커맨드에 실행 핸들러 + 활성조건(CanExecute) 연결
            StartCommand = new RelayCommand(_ => Start(), _ => !IsRunning);
            FinishCommand = new RelayCommand(_ => Finish(), _ => IsRunning);
            ResetCommand = new RelayCommand(_ => Reset(), _ => true);       // 항상 가능
            SuccessCommand = new RelayCommand(_ => OnSuccess(), _ => IsRunning);
            FailCommand = new RelayCommand(_ => OnFail(), _ => IsRunning);

            // 초기 UI 표시 강제 갱신(0값 기준 표시가 바로 보이도록)
            RaiseAll();
        }

        // -----------------------------
        // 4) 로직(행동 구현부)
        // -----------------------------
        private void Start()
        {
            if (IsRunning) return;        // 중복 방지
            _t0 = DateTime.Now;
            _t1 = null;                   // 종료시각 초기화
            IsRunning = true;             // 버튼 활성 조건들이 여기서 뒤바뀜
            _timer.Start();               // 1초마다 시간/처리량 갱신
            RaiseTimeRelated();           // 즉시 1회 갱신하여 눈에 보이게
        }

        private void Finish()
        {
            if (!IsRunning) return;
            _t1 = DateTime.Now;           // 종료시각 고정 → 이후 경과시간/처리량도 고정
            IsRunning = false;
            _timer.Stop();                 // 더 이상 틱 불필요
            RaiseTimeRelated();            // 종료 시점 텍스트로 최종 갱신
        }

        private void Reset()
        {
            // 모든 상태 초기화
            _success = 0;
            _fail = 0;
            _t0 = null;
            _t1 = null;
            IsRunning = false;
            _timer.Stop();

            // 카운트/시간/표시 전체 리프레시
            RaiseAll();
        }

        private void OnSuccess()
        {
            _success++;
            RaiseCountsAndDerived();  // 카운트와 파생 표시(SummaryText 등) 갱신
            RaiseTimeRelated();       // 처리량도 함께 변할 수 있으니 시간 관련 표시 갱신
        }

        private void OnFail()
        {
            _fail++;
            RaiseCountsAndDerived();
            RaiseTimeRelated();
        }

        // -----------------------------
        // 5) 변경 알림(화면 갱신 묶음)
        // -----------------------------
        // 카운트와 그로부터 파생되는 표시들 알림
        private void RaiseCountsAndDerived()
        {
            OnPropertyChanged(nameof(SuccessCount));
            OnPropertyChanged(nameof(FailCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(SuccessRate));
            OnPropertyChanged(nameof(SummaryText));
        }

        // 시간/처리량 텍스트 알림
        private void RaiseTimeRelated()
        {
            OnPropertyChanged(nameof(ElapsedText));
            OnPropertyChanged(nameof(ThroughputText));
        }

        // 모든 표시 강제 갱신(초기화 등 큰 상태 전환 시 사용)
        private void RaiseAll()
        {
            RaiseCountsAndDerived();
            RaiseTimeRelated();
        }

        // -----------------------------
        // 6) INotifyPropertyChanged
        // -----------------------------
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // -----------------------------
        // 7) CanExecute 갱신(버튼 활성/비활성 즉시 반영)
        // -----------------------------
        private void RaiseCanExecutes()
        {
            (StartCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FinishCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SuccessCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FailCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 재사용 가능한 단순 ICommand 구현체.
    /// - execute: 실제 실행 액션
    /// - canExecute: 실행 가능 여부(버튼 활성 조건)
    /// - RaiseCanExecuteChanged(): View에 CanExecute 변경 사실을 알림
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

        // View/Button이 CanExecute를 다시 평가하도록 트리거
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
