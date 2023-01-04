using DTAClient.Enums;

namespace DTAClient.DXGUI
{
    /// <summary>
    /// An interface for all switchable panels.
    /// </summary>
    public interface ISwitchable
    {
        void SwitchOn();

        void SwitchOff();

        string GetSwitchName();
    }
}
