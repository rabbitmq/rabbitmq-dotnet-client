using System.IO;
using System.Runtime.CompilerServices;

using Microsoft.IO;

namespace RabbitMQ.Util
{
    public static class PooledMemoryStream
    {
        private static RecyclableMemoryStreamManager s_manager = new RecyclableMemoryStreamManager();
        public static MemoryStream GetMemoryStream([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            return s_manager.GetStream(memberName);
        }

        public static MemoryStream GetMemoryStream(int initialSize, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            return s_manager.GetStream(memberName, initialSize);
        }
    }
}
