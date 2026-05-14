using Colossal.Logging;

namespace TreesReduceNoisePollution
{
    public static class Log
    {
        public static readonly ILog instance = LogManager.GetLogger("TreesReduceNoisePollution").SetShowsErrorsInUI(false);

        public static void Info(string message) => instance.Info(message);
        public static void Warn(string message) => instance.Warn(message);
        public static void Error(string message) => instance.Error(message);

        public static void Verbose(string message)
        {
            if (Mod.m_Setting != null && Mod.m_Setting.VerboseLogging)
            {
                instance.Info($"[VERBOSE] {message}");
            }
        }
    }
}