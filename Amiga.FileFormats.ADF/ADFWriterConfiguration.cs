namespace Amiga.FileFormats.ADF;

[Flags]
public enum DiskFullBehavior
{
    Error = 0,
    OmitEmptyDirectories = 0x01
}

public class ADFWriterConfiguration
{
    public FileSystem FileSystem { get; set; } = FileSystem.OFS;

    public bool InternationalMode { get; set; } = false;

    public byte[]? BootCode { get; set; } = null;

    public bool HD { get; set; } = false;

    public DiskFullBehavior DiskFullBehavior { get; set; } = DiskFullBehavior.Error;

    public static ADFWriterConfiguration FromADF(IADF adf)
    {
        return new ADFWriterConfiguration
        {
            FileSystem = adf.FileSystem,
            InternationalMode = adf.InternationalMode,
            BootCode = adf.BootCode,
            HD = adf.DiskSize == ADF.SizeHD
        };
    }
}