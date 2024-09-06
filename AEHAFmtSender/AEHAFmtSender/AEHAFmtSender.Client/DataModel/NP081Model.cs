namespace AEHAFmtSender.Client.DataModel
{
    public class NP081Model
    {
        public bool Power {  get; set; } = false;
        public int CoolingDegrees { get; set; } = 28;
        public int HeatingDegrees { get; set; } = 20;
        public int Degree { get; set; }

        public EnumOperationMode OperationMode { get; set; } = EnumOperationMode.COOLING;
        public EnumTimerMode TimerMode { get; set; } = EnumTimerMode.NONE;
        public int TimerLength { get; set; } = 0;
        
        public NP081Model() { }
        public enum EnumOperationMode
        {
            COOLING = 0x58,
            DEHUMIDIFICATION = 0x50,
            HEATING = 0x48,
            VENTILATION = 0x38
        }

        public enum EnumTimerMode
        {
            NONE = 0x00,
            OFFTIMER = 0x03,
            ONTIMER = 0x05
        }
    }
}
