using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amiga.FileFormats.ADF
{
    public interface IADF
    {
        FileSystem FileSystem { get; }
        bool InternationalMode { get; }
        bool DirectoryCache { get; }
        bool LongFileNames { get; }
        string BootCode { get; }
        IDirectory RootDirectory { get; }
        DateTime LastModificationDate { get; }
        DateTime CreationDate { get; }
    }
}
