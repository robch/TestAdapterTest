using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace TestAdapterTest
{
    public class Logger
    {
        public static void Log(IMessageLogger logger)
        {
            Logger.logger = logger;
        }

        public static void Log(string text)
        {
            LogInfo(text);
            Logger.DbgTraceInfo(text);
        }

        public static void LogIf(bool log, string text)
        {
            if (log) Log(text);
        }

        #region log methods

        public static void LogInfo(string text)
        {
            using (var mutex = new Mutex(false, "Logger Mutex"))
            {
                mutex.WaitOne();
                File.AppendAllText(_logPath, $"{DateTime.Now}: INFO: {text}\n");
                mutex.ReleaseMutex();
            }
        }

        public static void LogWarning(string text)
        {
            using (var mutex = new Mutex(false, "Logger Mutex"))
            {
                mutex.WaitOne();
                File.AppendAllText(_logPath, $"{DateTime.Now}: WARNING: {text}\n");
                mutex.ReleaseMutex();
            }
        }

        public static void LogError(string text)
        {
            using (var mutex = new Mutex(false, "Logger Mutex"))
            {
                mutex.WaitOne();
                File.AppendAllText(_logPath, $"{DateTime.Now}: ERROR: {text}\n");
                mutex.ReleaseMutex();
            }
        }

        #endregion

        #region dbg trace methods

        public static void DbgTraceInfo(string text)
        {
#if DEBUG
            TraceInfo(text);
#endif
        }

        public static void DbgTraceWarning(string text)
        {
#if DEBUG
            TraceWarning(text);
#endif
        }

        public static void DbgTraceError(string text)
        {
#if DEBUG
            TraceError(text);
#endif
        }

        #endregion

        #region trace methods

        public static void TraceInfo(string text)
        {
            logger?.SendMessage(TestMessageLevel.Informational, $"{DateTime.Now}: {text}");
        }

        public static void TraceWarning(string text)
        {
            logger?.SendMessage(TestMessageLevel.Warning, $"{DateTime.Now}: {text}");
        }

        public static void TraceError(string text)
        {
            logger?.SendMessage(TestMessageLevel.Error, $"{DateTime.Now}: {text}");
        }

        #endregion

        #region private methods and data

        private static string GetLogPath()
        {
            var pid = Process.GetCurrentProcess().Id.ToString();
            var time = DateTime.Now.ToFileTime().ToString();
            Directory.CreateDirectory("./logs");
            return $"./logs/log-test-adapter-{time}-{pid}.log";
        }

        private static IMessageLogger logger = null;

        private static string _logPath = GetLogPath();

        #endregion
    }
}
