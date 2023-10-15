using System.Reflection;
using TestFramework.Code.Test;

namespace TestFramework.Code.FrameworkModules
{
    public class TestManager
    {
        public FrameworkTest? CurrentTest { get; set; }
        
        private static string OutputRootPath;
        private CancellationTokenSource? TestCancellationTokenSource;
        private Thread? CurrentTestThread;

        private static TestManager? _instance;
        private TestManager()
        {
            LogManager.TestManager = this;
            OutputRootPath = "";
        }

        public static TestManager Instance
        {
            get
            {
                _instance ??= new();
                return _instance;
            }
        }

        public async Task LaunchTest(string testClassName)
        {
            ConfigManager.LoadTestFrameworkMainConfig();
            LogManager.StartLogFile();

            Type? testClass = GetTestClass(testClassName);
            if (testClass == null) Environment.Exit(-1);

            CurrentTest = GetTestInstance(testClass);
            if (CurrentTest == null) Environment.Exit(-1);

            MethodInfo? testLaunchMethod = GetTestLaunchMethod(testClass);
            if (testLaunchMethod == null) Environment.Exit(-1);
            
            CreateTestThread(testLaunchMethod);
            await CreateTestTimeoutAwaitThreadAsync();
        }

        public static string GetOutputRootPath()
        {
            InitOutputRootPath();
            return OutputRootPath;
        }

        private static void InitOutputRootPath()
        {
            if (OutputRootPath != null && OutputRootPath != "") return;

            if ((OutputRootPath = ConfigManager.GetConfigParam("TestFrameworkOutputRootPath")!) == null)
            {
                LogManager.LogError("Could not find the 'TestFrameworkOutputRootPath' config param, the test can not continue, aborting");
                Environment.Exit(-1);
            }
            
            if (!Path.IsPathRooted(OutputRootPath)) OutputRootPath = Path.Combine(Environment.CurrentDirectory, OutputRootPath);
        }

        private static Type? GetTestClass(string testClassName)
        {
            if (testClassName == null)
            {
                LogManager.LogError($"No se ha indicado la clase del test que se quiere ejecutar");
                return null;
            }
            string fullTestClassName = "TestFramework.FrameworkTests." + testClassName;

            Type? testClass = Type.GetType(fullTestClassName);
            if (testClass == null)
            {
                LogManager.LogError($"No se ha encontrado un test con la clase indicada: {fullTestClassName}");
                return null;
            }

            return testClass;
        }

        private static FrameworkTest? GetTestInstance(Type testClass)
        {
            FrameworkTest? testInstance = (FrameworkTest?)Activator.CreateInstance(testClass);
            if (testInstance == null)
            {
                LogManager.LogError($"No se ha podido crear una instancia de la clase del test: {testClass.Name}");
                return null;
            }
            return testInstance;
        }

        private static MethodInfo? GetTestLaunchMethod(Type testClass)
        {
            MethodInfo? testLaunchMethod = testClass.GetMethod("Launch");
            if (testLaunchMethod == null)
            {
                LogManager.LogError($"No se ha encontrado el método 'Launch' en la clase del test: {testClass.Name}");
                return null;
            }
            return testLaunchMethod;
        }

        private void CreateTestThread(MethodInfo testLaunchMethod)
        {
            LogManager.LogOK($"Se va a crear un nuevo Thread para ejecutar el test: {CurrentTest?.Name}");

            TestCancellationTokenSource = new();

            CurrentTestThread = new(() =>
            {
                object[] arguments = new object[] {TestCancellationTokenSource.Token};
                testLaunchMethod.Invoke(CurrentTest, arguments);
            });
            CurrentTestThread.Start();
        }

        private async Task CreateTestTimeoutAwaitThreadAsync()
        {
            LogManager.LogOK($"Se va a crear un nuevo Thread para gestionar el timeout del test: {CurrentTest?.Name}");
            await WaitForCurrentTest(CurrentTest?.Timeout ?? 0);
        }

        private async Task WaitForCurrentTest(float timeout)
        {
            float elapsedSecs = 0;
            while(CurrentTestThread?.IsAlive ?? false)
            {
                if (elapsedSecs < timeout)
                {
                    elapsedSecs++;
                    await Task.Delay(1000);
                }
                else
                {
                    LogManager.LogError($"El test '{CurrentTest?.Name}' ha dado timeout tras {CurrentTest?.Timeout}s, se va a detener. Si no se detiene en 10s, se forzara su detencion");
                    // Primero, se envía un aviso para que el test se cierre de forma legal
                    TestCancellationTokenSource?.Cancel();
                    // Si el test no se ha cerrado tras 10s, se fuerza el fin de su Thread
                    if(!(CurrentTestThread?.Join(10000) ?? true))
                    {
                        LogManager.LogError($"El test '{CurrentTest?.Name}' no se ha cerrado limpiamente tras 10s del envío de la señal. Se va a cerrar el programa");
                        Environment.Exit(-2);
                    }
                    return;
                }
            }
            LogManager.LogOK($"El test '{CurrentTest?.Name}' ha terminado correctamente dentro de su plazo maximo, la ejecucion ha terminado");
            Environment.Exit(0);
        }
    }
}