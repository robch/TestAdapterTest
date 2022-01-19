using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            File.AppendAllText("log", $"{DateTime.Now}: {text}\n");

            #if DEBUG
            logger?.SendMessage(TestMessageLevel.Informational, $"{DateTime.Now}: {text}");
            #endif
        }

        #region private data
        private static IMessageLogger logger = null;
        #endregion
    }
}
