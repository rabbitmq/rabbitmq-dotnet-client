using System;
using System.Globalization;

namespace Test
{
    public static class Util
    {
        private static readonly bool s_isWindows = false;

        static Util()
        {
            s_isWindows = InitIsWindows();
        }

        public static string Now => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        public static bool IsWindows => s_isWindows;

        private static bool InitIsWindows()
        {
            PlatformID platform = Environment.OSVersion.Platform;
            if (platform == PlatformID.Win32NT)
            {
                return true;
            }

            string os = Environment.GetEnvironmentVariable("OS");
            if (os != null)
            {
                os = os.Trim();
                return os == "Windows_NT";
            }

            return false;
        }
    }
}
