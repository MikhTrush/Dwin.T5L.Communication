namespace Dwin.T5L.Communication
{
    /// <summary>
    /// See the T5L_DGUS_II Application Development Guide
    /// </summary>
    public class DwinDisplayConstants
    {
        public const ushort SystemResetAddress = 0x04; //Length 2
        public const ushort OSUpdateCmdAddress = 0x02; //Length 2
        public const ushort NorFlashRwCmd = 0x08; //Length 4  
        public const ushort UART2ConfigAddress = 0x0c; //Length 2
        public const ushort PageNumberAddress = 0x84; //Length 2
    }
}