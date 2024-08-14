using System.ComponentModel;
using System.Text;

namespace Amiga.FileFormats.LHA
{
	internal class CheckedWriter : BinaryWriter
	{
		public class DiskFullException : Exception { }

		public DriveInfo? DriveInfo { get; }

		public CheckedWriter(Stream stream, Encoding encoding, bool leaveOpen, DriveInfo? driveInfo)
			: base(stream, encoding, leaveOpen)
		{
			DriveInfo = driveInfo;
		}

		public override void Write(byte value)
		{
			CheckForDiskError(() => base.Write(value), 1);
		}

		public override void Write(byte[] buffer)
		{
			CheckForDiskError(() => base.Write(buffer), buffer.Length);
		}

		private static bool DiskFull(DriveInfo? driveInfo, int requiredSize)
		{
			return driveInfo != null && driveInfo.AvailableFreeSpace < requiredSize;
		}

		private void CheckForDiskError(Action writeOperation, int writeSize)
		{
			try
			{
				writeOperation();
			}
			catch (Win32Exception)
			{
				throw new DiskFullException();
			}
			catch (IOException)
			{
				if (DiskFull(DriveInfo, writeSize))
					throw new DiskFullException();

				throw new UnauthorizedAccessException();
			}
		}

	}
}
