using System.Diagnostics;
using System.Reflection;

namespace TestFramework.Code.FrameworkModules
{
    public static class LogManager
    {
        private static string LogPath;
        private static bool LogOpen = false;
        private static bool DumpToLogFile = false;
        private static StreamWriter? LogFile;

        public enum LogLevel
        {
            None,
            Error,
            OK,
            Warning,
            Debug
        }

        public static LogLevel LogLvl { get; set; }
        public static TestManager? TestManager { get; set; }

        static LogManager()
        {
            LogLvl = LogLevel.Warning;
            LogPath = "";
        }

        public static void LogError(string message)
        {
            if (LogLvl >= LogLevel.Error)
            {
                PrintConsoleLogTimePrefix();
                Console.ForegroundColor = ConsoleColor.Red;
                WriteLog($"[ERROR] {message}", LogLevel.Error);
            }

            if (LogLvl >= LogLevel.Debug) PrintCallStack();
        }

        public static void LogOK(string message)
        {
            if (LogLvl >= LogLevel.OK)
            {
                PrintConsoleLogTimePrefix();
                Console.ForegroundColor = ConsoleColor.Green;
                WriteLog($"[OK] {message}", LogLevel.OK);
            }
        }

        public static void LogWarning(string message)
        {
            if (LogLvl >= LogLevel.Warning)
            {
                PrintConsoleLogTimePrefix();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                WriteLog($"[WARNING] {message}", LogLevel.Warning);
            }
        }

        public static void LogDebug(string message)
        {
            if (LogLvl >= LogLevel.Debug)
            {
                PrintConsoleLogTimePrefix();
                Console.ForegroundColor = ConsoleColor.White;
                WriteLog($"[DEBUG] {message}", LogLevel.Debug);
            }
        }

        public static void LogTestError(string message)
        {
            if (LogLvl >= LogLevel.Error)
            {
                PrintConsoleLogTimePrefix();
                PrintConsoleLogTestPrefix();
                Console.ForegroundColor = ConsoleColor.Red;
                WriteLog($"[ERROR] {message}", LogLevel.Error, true);
            }

            if (LogLvl >= LogLevel.Debug) PrintCallStack();
        }

        public static void LogTestOK(string message)
        {
            if (LogLvl >= LogLevel.OK)
            {
                PrintConsoleLogTimePrefix();
                PrintConsoleLogTestPrefix();
                Console.ForegroundColor = ConsoleColor.Green;
                WriteLog($"[OK] {message}", LogLevel.OK, true);
            }
        }

        public static void LogTestWarning(string message)
        {
            if (LogLvl >= LogLevel.Warning)
            {
                PrintConsoleLogTimePrefix();
                PrintConsoleLogTestPrefix();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                WriteLog($"[WARNING] {message}", LogLevel.Warning, true);
            }
        }

        public static void LogTestDebug(string message)
        {
            if (LogLvl >= LogLevel.Debug)
            {
                PrintConsoleLogTimePrefix();
                PrintConsoleLogTestPrefix();
                Console.ForegroundColor = ConsoleColor.White;
                WriteLog($"[DEBUG] {message}", LogLevel.Debug, true);
            }
        }

        public static void StartLogFile()
        {
            if(!LogOpen && ThisExecutionHasLogFileDump())
            {
                InitLogLevel();
                InitLogPath();
                DeleteOldLogFile();
                CreateTestLogFile();
                LogOpen = true;
                DumpToLogFile = true;
            }
        }

        public static void CloseLogFile()
        {
            if (LogOpen)
            {
                LogFile?.WriteLine("<p class='ok'><span class='time-tag'>" + GetFormatedElapsedTime() + "</span>Cerrando el log...</p>");
                LogFile?.WriteLine("</body>");
                LogFile?.WriteLine("</html>");
                DumpToLogFile = false;
                LogFile?.Close();
                LogOpen = false;
            }
        }

        public static string GetLogPath()
        {
            InitLogPath();
            return LogPath;
        }

        public static bool IsLogFileDumpActive()
        {
            return DumpToLogFile;
        }

        public static bool ThisExecutionHasLogFileDump()
        {
            return ConfigManager.GetConfigParam("DumpLogsToFile") == "true";
        }

        private static void InitLogLevel()
        {
            string logLvlName;
            if ((logLvlName = ConfigManager.GetConfigParam("LogLevel")!) == null)
            {
                LogError("Could not find the 'LogLevel' config param, the log can not be opened, aborting execution");
                Environment.Exit(-1);
            }

            LogLvl = (LogLevel)Enum.Parse(typeof(LogLevel), logLvlName);
        }

        private static void InitLogPath()
        {
            if (LogPath != null && LogPath != "") return;

            if ((LogPath = ConfigManager.GetConfigParam("LogPath")!) == null)
            {
                LogError("Could not find the 'LogPath' config param, the log can not be opened, aborting execution");
                Environment.Exit(-1);
            }
            
            if (!Path.IsPathRooted(LogPath)) LogPath = Path.Combine(Environment.CurrentDirectory, LogPath);
        }

        private static void PrintCallStack()
        {
            StackTrace stackTrace = new();

            Console.ForegroundColor = ConsoleColor.White;
            WriteLog("Call Stack:\n{", LogLevel.Debug);
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                StackFrame? frame = stackTrace.GetFrame(i);
                MethodBase? method = frame.GetMethod();
                WriteLog($"\t- {method?.DeclaringType}.{method?.Name}", LogLevel.Debug);
            }
            WriteLog("}", LogLevel.Debug);
        }

        private static void PrintConsoleLogTimePrefix()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(GetFormatedElapsedTime());
        }

        private static void PrintConsoleLogTestPrefix()
        {
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.Write(GetLogTestPrefix());
        }

        private static string GetLogTestPrefix(bool isForHTML = false)
        {
            if (TestManager?.CurrentTest != null && TestManager?.CurrentTest?.CurrentTestCase != null)
            {
                if (isForHTML) return $"&lt;{TestManager?.CurrentTest.CurrentTestCase.ID}:{TestManager?.CurrentTest.CurrentTestCase.CurrentStep}&gt; ";
                else return $"<{TestManager?.CurrentTest.CurrentTestCase.ID}:{TestManager?.CurrentTest.CurrentTestCase.CurrentStep}> ";
            }
            return "";
        }

        private static string GetFormatedElapsedTime()
        {
            return "|" + TimeManager.AppClock.Elapsed.TotalSeconds.ToString("0.00") + "| ";
        }

        private static void DeleteOldLogFile()
        {
            if (File.Exists(LogPath)) File.Delete(LogPath);
        }

        private static void CreateTestLogFile()
        {
            LogFile = new(LogPath, append: true);

            string htmlHeader = @"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    /* Estilo para los párrafos */
                    p {
                        margin: 5px 0;
                        font-size: 13px;
                        font-weight: bold;
                        font-family: 'Arial', sans-serif;
                    }
                    /* Estilo del fondo */
                    body { background-color: #111; color: white; }
                    /* Estilos para los diferentes tags de logs */
                    .error { color: rgb(220, 69, 69); }
                    .ok { color: rgb(60, 185, 60); }
                    .warning { color: rgb(220, 180, 80) }
                    .debug { color: rgb(230, 230, 230); }
                    .test-log-prefix { color: rgb(160, 100, 220); }
                    .time-tag { color: rgb(160, 160, 160); }
                </style>
            </head>
            <body>";
            LogFile.WriteLine(htmlHeader);

            // Manejador de eventos para cerrar el log en el cierre de la aplicación
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => CloseLogFile();
        }

        private static void WriteLog(string message, LogLevel lvl, bool printPrefixOnFile = false)
        {
            Console.WriteLine(message);
            if (DumpToLogFile) WriteLogOnFile(message, lvl, printPrefixOnFile);
        }

        private static void WriteLogOnFile(string message, LogLevel lvl, bool printPrefix)
        {
            string logClassName;
            switch (lvl)
            {
                case LogLevel.Error:
                    logClassName = "error";
                    break;

                case LogLevel.OK:
                    logClassName = "ok";
                    break;

                case LogLevel.Warning:
                    logClassName = "warning";
                    break;

                case LogLevel.Debug:
                    logClassName = "debug";
                    break;

                default:
                    logClassName = "error";
                    message = "[ERROR: ESTE LOG NO TIENE NINGÚN TIPO DEFINIDO] - " + message;
                    break;
            }

            if (printPrefix) LogFile?.WriteLine("<p class='" + logClassName + "'><span class='time-tag'>" + GetFormatedElapsedTime() + "</span><span class='test-log-prefix'>" + GetLogTestPrefix(true) + "</span> " + message + "</p>");
            else LogFile?.WriteLine("<p class='" + logClassName + "'><span class='time-tag'>" + GetFormatedElapsedTime() + "</span>" + message + "</p>");
        }
    }
}