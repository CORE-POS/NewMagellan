using System.IO;

namespace USBLayer
{
    public interface USBWrapper
    {
        /**
         * Get a handle for USB device file
         * @param filename the name of the file OR vendor and device ids formatted as "vid&pid"
         * @param report_size [optional] report size in bytes
         * @return open read/write FileStream
         */
        Stream GetUSBHandle(string filename, int report_size);

        void CloseUSBHandle();
    }
}
