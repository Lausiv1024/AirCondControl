using System.Text.Json.Serialization;

namespace AEHAFmtSender.IRFormats
{
    public class NP081 : RemoteControlBase
    {
        public NP081() : base("NP081")
        {
            
        }
        //エアコンのリモコンのモデル的なやつ
        const int CUSTOMER_CODE1 = 0x23;
        const int CUSTOMER_CODE2 = 0xcb;
        const int PARITY_DATA0 = 0x26;
        const int MAX_DEGREE = 31;
        const int MIN_DEGREE = 16;
        /// <summary>
        /// 風左右 (byte8 上位4bit)。現状は左(0x2)固定。
        /// </summary>
        const int WIND_HORIZONTAL = 0x2;
        /// <summary>
        /// 冷房時の byte8 下位4bit 固定値
        /// </summary>
        const int DEHUMID_COOLING = 0x6;
        /// <summary>
        /// 暖房・送風時の byte8 下位4bit 固定値
        /// </summary>
        const int DEHUMID_OTHER = 0x0;
        private bool _power;
        /// <summary>
        /// エアコンの電源ステータス
        /// </summary>
        public bool Power { get { return _power; } set { _power = value; } }
        private int coolingDeg = 28;
        /// <summary>
        /// 冷房時設定気温
        /// </summary>
        public int CoolingDegrees { get { return coolingDeg; } set { coolingDeg = CheckDegrees(value) || OperationMode != OperationMode.COOLING ? value : throw new InvalidOperationException("設定気温は16~31でなければいけません"); } }
        private int heatingDeg = 20;
        /// <summary>
        /// 暖房時設定気温
        /// </summary>
        public int Heatingdegrees { get { return heatingDeg; } set { heatingDeg = CheckDegrees(value) || OperationMode != OperationMode.HEATING ? value : throw new InvalidOperationException("設定気温は16~31でなければいけません"); } }
        /// <summary>
        /// 設定気温
        /// </summary>
        public int Degrees
        {
            get { return _mode == OperationMode.COOLING ? CoolingDegrees : _mode == OperationMode.HEATING ? Heatingdegrees : 0; }
            set
            {
                if (_mode == OperationMode.COOLING)
                    CoolingDegrees = value;
                else if (_mode == OperationMode.HEATING)
                    Heatingdegrees = value;
                else
                    throw new InvalidOperationException("冷暖房動作時以外の変更は許可されません");
            }
        }
        private OperationMode _mode = OperationMode.COOLING;
        /// <summary>
        /// 運転モード
        /// </summary>
        public OperationMode OperationMode { get { return _mode; } set { _mode = value; } }
        private TimerMode _timerMode = TimerMode.NONE;
        /// <summary>
        /// タイマー切替
        /// </summary>
        public TimerMode TimerMode { get { return _timerMode; } set { _timerMode = value; } }
        private int _timerLength = 0;
        /// <summary>
        /// タイマー時間 (単位：分)
        /// </summary>
        public int TimerLength { get { return _timerLength; } set { _timerLength = value; } }
        private DehumidificationAdjustments _dehumidification = DehumidificationAdjustments.NORMAL;
        /// <summary>
        /// 除湿強度。除湿運転時のみ信号に反映される。
        /// </summary>
        public DehumidificationAdjustments Dehumidification { get { return _dehumidification; } set { _dehumidification = value; } }

        /// <summary>
        /// byte8 (上位4bit 風左右 / 下位4bit 除湿強度) を組み立てる。
        /// </summary>
        private byte GetWindAndDehumidification()
        {
            int lower = _mode switch
            {
                OperationMode.DEHUMIDIFICATION => (int)_dehumidification,
                OperationMode.COOLING => DEHUMID_COOLING,
                _ => DEHUMID_OTHER
            };
            return (byte)((WIND_HORIZONTAL << 4) | lower);
        }

        public override byte[] GetCurrentSignal()
        {
            byte[] signal =
            [
                CUSTOMER_CODE1,
                CUSTOMER_CODE2,
                PARITY_DATA0,
                0x01,
                0x00,
                (byte)(_power ? 0x20 : 0x0),
                (byte) _mode,
                (byte) (Degrees - 16),
                GetWindAndDehumidification(),
                0x40,
                0x00,
                _timerMode == TimerMode.OFFTIMER ? (byte)(_timerLength / 10) : (byte)0x00, //エアコン上では10分単位で変えるのでそれに合わせる。
                _timerMode == TimerMode.ONTIMER ? (byte)(_timerLength / 10) : (byte)0x00,
                (byte) _timerMode,
                0x14,
                0x00,
                0x00,
                0x00,
            ];
            uint errCorrection = 0;
            foreach (byte b in signal)
            {
                errCorrection += b;
            }
            signal[17] = (byte)errCorrection;//下位8bitなので上を無理やりそぎ落とす
            return signal;
        }

        private bool CheckDegrees(int val)
        {
            return val >= MIN_DEGREE && val <= MAX_DEGREE;
        }

        public bool TimerStatusChanged(NP081? old)
            => old != null && (TimerMode != old.TimerMode);
        public bool PowerStateChanged(NP081? old)
            => (old != null && Power != old.Power);
    }

    public enum OperationMode
    {
        COOLING = 0x58,
        DEHUMIDIFICATION = 0x50,
        HEATING = 0x48,
        VENTILATION = 0x38
    }

    public enum TimerMode
    {
        NONE = 0x00,
        OFFTIMER = 0x03,
        ONTIMER = 0x05
    }
    /// <summary>
    /// 除湿強度 (byte8 下位4bit)
    /// </summary>
    public enum DehumidificationAdjustments
    {
        STRONG = 0x0,
        NORMAL = 0x2,
        WEAK = 0x4
    }
}

