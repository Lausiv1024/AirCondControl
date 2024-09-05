using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircondSignalGen
{
    public class NP081 : RemoteControlBase
    {
        const int CUSTOMER_CODE1 = 0x23;
        const int CUSTOMER_CODE2 = 0xcb;
        const int PARITY_DATA0 = 0x26;
        const int MAX_DEGREE = 31;
        const int MIN_DEGREE = 16;
        private bool _power;
        /// <summary>
        /// エアコンの電源ステータス
        /// </summary>
        public bool Power { get { return _power; } set { _power = value; } }
        private int coolingDeg = 28;
        /// <summary>
        /// 冷房時設定気温
        /// </summary>
        public int CoolingDegrees { get{ return coolingDeg; } set { coolingDeg = CheckDegrees(value) ? value : throw new InvalidOperationException("設定気温は16~31でなければいけません"); } }
        private int heatingDeg = 20;
        /// <summary>
        /// 暖房時設定気温
        /// </summary>
        public int Heatingdegrees {  get{ return heatingDeg; } set{ heatingDeg = CheckDegrees(value) ? value : throw new InvalidOperationException("設定気温は16~31でなければいけません"); } }
        /// <summary>
        /// 設定気温
        /// </summary>
        public int Degrees { get{return _mode == OperationMode.COOLING ? CoolingDegrees : _mode == OperationMode.HEATING ? Heatingdegrees : 0;} 
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
        private OperationMode _mode;
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
        /// タイマー時間
        /// </summary>
        public int TimerLength { get { return _timerLength; } set { _timerLength = TimerMode != TimerMode.NONE ? value : 0; } }

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
                (byte) (CoolingDegrees - 16),
                0x26,
                0x40,
                0x00,
                _timerMode == TimerMode.OFFTIMER ? (byte)(_timerLength / 10) : (byte)0x00,
                _timerMode == TimerMode.ONTIMER ? (byte)(_timerLength / 10) : (byte)0x00,
                (byte) _timerMode,
                0x10,
                0x00,
                0x00,
                0x00,
            ];
            uint errCorrection = 0;
            foreach(byte b in signal)
            {
                errCorrection += b;
            }
            signal[17] = (byte) errCorrection;//下位8bitなので上を無理やりそぎ落とす
            return signal;
        }

        private bool CheckDegrees(int val)
        {
            return val >= MIN_DEGREE && val <= MAX_DEGREE;
        }
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
}
