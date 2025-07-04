namespace AEHAFmtSender.IRFormats
{
    public abstract class RemoteControlBase
    {
        public string Name { get;}
        public RemoteControlBase(string name)
        {
            this.Name = name;
        }

        public abstract byte[] GetCurrentSignal();
    }
}
