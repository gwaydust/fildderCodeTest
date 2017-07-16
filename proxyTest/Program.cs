using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Fiddler;
using BasicFormats;
using System.Configuration;

namespace proxyTest
{
    class Program
    {
        private static List<Session> sessions = new List<Fiddler.Session>();
        private static bool isDone = false;
        private static string procName = "";
        private static string exportFilename = $"{System.IO.Path.GetTempPath()}\\temp{DateTime.Now.ToString("yyyyMMddhhmmss")}.har";
        static void Main(string[] args)
        {
            if (args.Length<1)
            {
                String exeName = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Console.WriteLine($"Usage: {exeName} <process name or partial name>");
                Console.WriteLine($"\t e.g. {exeName} microsoftedgecp");
                Console.ReadLine();
            }
            procName = args[0];
            Console.WriteLine($"Process to watch: {procName}");
            InstallCertificate();
            FiddlerApplication.SetAppDisplayName("FiddlerCoreDemoApp");            
            FiddlerApplication.BeforeRequest += FiddlerApplication_BeforeRequest;
            FiddlerApplication.OnNotification += FiddlerApplication_OnNotification;
            //FiddlerApplication.AfterSessionComplete += FiddlerApplication_AfterSessionComplete;
            Console.CancelKeyPress += Console_CancelKeyPress;
            Fiddler.CONFIG.IgnoreServerCertErrors = true;
            FiddlerApplication.Startup(9093, true, true, false);
            Console.WriteLine($"Fiddler is started? {FiddlerApplication.IsStarted().ToString()}");
            do
            {
                Console.ReadLine();
            } while (!isDone);
        }

        public static bool InstallCertificate()
        {
            if (!string.IsNullOrEmpty(GetSetting("proxyKey")))
            {
                FiddlerApplication.Prefs.SetStringPref("fiddler.certmaker.bc.key", GetSetting("proxyKey"));
                FiddlerApplication.Prefs.SetStringPref("fiddler.certmaker.bc.cert", GetSetting("proxyCert"));
            }

            if (!CertMaker.rootCertExists())
            {
                if (!CertMaker.createRootCert())
                    return false;
                if (!CertMaker.trustRootCert())
                    return false;

                SetSetting("proxyCert", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.bc.cert", ""));
                SetSetting("proxyKey", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.bc.key", ""));
            }
            return true;
        }

        private static void FiddlerApplication_BeforeRequest(Session oSession)
        {                      
            Monitor.Enter(sessions);
            if (oSession.LocalProcess.ToLower().Contains(procName))
            {
                sessions.Add(oSession);
            }
            Monitor.Exit(sessions); 
        }       
        
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            isDone = true;
            HTTPArchiveFormatExport harExport = new HTTPArchiveFormatExport();                        
            System.Collections.Generic.Dictionary<String, object> dictOption = new Dictionary<string, object>();
            dictOption.Add("Filename", exportFilename);
            if (!harExport.ExportSessions("HTTPArchive v1.2", sessions.ToArray(), dictOption, null))
            {
                Console.WriteLine("Failed to export to har, press enter to exit");
                
            } else
            {
                Console.WriteLine($"File {exportFilename} created, press enter to exit");
            }
            //FiddlerApplication.AfterSessionComplete -= FiddlerApplication_AfterSessionComplete;

            sessions.Clear();           
            if (FiddlerApplication.IsStarted())
                FiddlerApplication.Shutdown();
            Console.ReadLine();
        }

        private static void FiddlerApplication_OnNotification(object sender, Fiddler.NotificationEventArgs e)
        {
            throw new NotImplementedException();
        }

        private static string GetSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        private static void SetSetting(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location);

            config.AppSettings.Settings.Remove(key);
            config.AppSettings.Settings.Add(key, value);

            config.Save(ConfigurationSaveMode.Modified);           
        }
    }
}
