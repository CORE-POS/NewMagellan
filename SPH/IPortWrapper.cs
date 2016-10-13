namespace SPH
{
    /**
     * This interface exists so that a mock serial
     * port can be swapped in during testing
     */
    public interface IPortWrapper
    {
        void Open();
        void Close();
        int ReadByte();
        void Write(string msg);
        void Write(byte[] msg, int offset, int count);
    }
}
