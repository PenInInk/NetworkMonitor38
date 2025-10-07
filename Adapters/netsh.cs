using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace NetworkAdapters
{
    public class netshProcess
    {
        /*
         * Purpose: get the port status of the Ethernet port
         * Use: netsh.exe
         * Output: Adapters.Nic object
         * */
        private static ObservableCollection<NetworkAdapter> _nic;
        private static Process process;

        public static void Start()
        {

            _nic = new ObservableCollection<NetworkAdapter>();

            process = new Process();
            process.StartInfo.FileName = "netsh.exe";
            //string[] args = { "interface", "show", "interface" };
            process.StartInfo.Arguments = "interface show interface";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;

            Init();

        }

        private static void Init()
        {
            try
            {
                process.Start();
            }
            catch 
            {
                return;
            }
                
            // Synchronously read the standard output of the spawned process. 
            
            using (StreamReader reader = process.StandardOutput)
            {
                string sRes = reader.ReadToEnd();
                string[] array = sRes.Split("\r\n".ToCharArray(),StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in array)
                {
                    if (line.Contains("Admin State") || line.Contains("----"))
                    {
                    }
                    else
                    {
                        var resultString = Regex.Replace(line, " {2,}", "\t");
                        var result = resultString.Split('\t');
                        Nics.Add(new NetworkAdapter(result[3]));
                    }
                }
                reader.Close();
                process.WaitForExit();
            }
            try
            {
                process.WaitForExit();
                process.Close();
            }
            catch { }
        }

        public static void Refresh()
        {
            try
            {
                process.Start();
            

                using (StreamReader reader = process.StandardOutput)
                {
                    string sRes = reader.ReadToEnd();
                    string[] array = sRes.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    foreach (NetworkAdapter nic in Nics)
                    {
                        string match = array.FirstOrDefault(x => x.Contains(nic.Name));
                        var resultString = Regex.Replace(match, " {2,}", "\t");
                        var result = resultString.Split('\t');
                        nic.Enabled = result[0] == "Enabled" ? true : false;
                        nic.Connected = result[1] == "Connected" ? true : false;
                        nic.Description = result[2];

                    }
                    reader.Close();
                    process.WaitForExit();
                }
            }
            catch
            {
            }
            try
            {
                process.WaitForExit();
                process.Close();
            }
            catch
            {
            }
        }

        public static NetworkAdapter GetSingleStatus(string nicname)
        {
            NetworkAdapter nicresult = new NetworkAdapter(nicname);
            try
            {
                process.Start();


                using (StreamReader reader = process.StandardOutput)
                {
                    string sRes = reader.ReadToEnd();
                    string[] array = sRes.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                        string match = array.FirstOrDefault(x => x.Contains(nicname));
                        var resultString = Regex.Replace(match, " {2,}", "\t");
                        var result = resultString.Split('\t');
                        nicresult.Enabled = result[0] == "Enabled" ? true : false;
                        nicresult.Connected = result[1] == "Connected" ? true : false;
                        nicresult.Description = result[2];

                    
                    reader.Close();
                    process.WaitForExit();
                    return nicresult;
                }
            }
            catch
            {
            }
            try
            {
                process.WaitForExit();
                process.Close();
            }
            catch
            {
                
            }
            finally
            {
                
            }


            return nicresult;

        }

        private static IPEndPoint ParseIpEndPoint(string str)
        {
            var ipPort = str.Split(':');
            return new IPEndPoint(IPAddress.Parse(ipPort[0]), int.Parse(ipPort[1]));
        }

        public static  ObservableCollection<NetworkAdapter> Nics
        {
            get { return _nic; }
            set { _nic = value; }
        }
    }
}
