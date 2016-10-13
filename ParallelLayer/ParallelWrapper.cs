using System.IO;

namespace ParallelLayer {

public interface ParallelWrapper 
{

    /**
     * Get a handle for paralell device file
     * @param filename the name of the file 
     * @param report_size [optional] report size in bytes
     * @return open write FileStream
     */
    FileStream GetLpHandle(string filename);
    void CloseLpHandle();

}

}
