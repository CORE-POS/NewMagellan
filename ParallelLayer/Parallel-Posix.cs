using System;
using System.IO;

namespace ParallelLayer {

public class ParallelWrapper_Posix : ParallelWrapper {

    public FileStream GetLpHandle(string filename)
    { 
        FileStream fs = null;
        try {
            fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        } catch(Exception) { }

        return fs;
    }

    public void CloseLpHandle(){ }

}

}
