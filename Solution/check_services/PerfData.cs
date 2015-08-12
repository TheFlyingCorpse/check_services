using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace check_services
{
    public static class PerfData
    {
        public static int iNumberOfServices = 0;
        public static int iNumberOfRunningServices = 0;
        public static int iNumberOfStoppedServices = 0;
        public static int iNumberOfPendingServices = 0;
        public static int iNumberOfPausedServices = 0;
        public static int iNumberOfUnknownServices = 0;
        public static int iNumberOfCorrectServices = 0;
        public static int iNumberOfWrongServices = 0;

        public static Dictionary<int, string> counterMapEnglish = new Dictionary<int, string>();
        public static Dictionary<int, string> counterMapCurrentLanguage = new Dictionary<int, string>();

        private static PerformanceCounter pCounter = null;
        private static CounterSample pFirstSample;
        private static CounterSample pSecondSample;

        public static void ServiceStatusCounting(string status)
        {
            // Calculate perfdata only for matches
            if (status == ServiceControllerStatus.Running.ToString())
            {
                iNumberOfRunningServices++;
            }
            else if (status == ServiceControllerStatus.Stopped.ToString())
            {
                iNumberOfStoppedServices++;
            }
            else if (status == ServiceControllerStatus.Paused.ToString())
            {
                iNumberOfPausedServices++;
            }
            else if (status == ServiceControllerStatus.StartPending.ToString() ||
                status == ServiceControllerStatus.StopPending.ToString() ||
                status == ServiceControllerStatus.PausePending.ToString() ||
                status == ServiceControllerStatus.ContinuePending.ToString())
            {
                iNumberOfPendingServices++;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal sealed class SERVICE_STATUS_PROCESS
        {
            [MarshalAs(UnmanagedType.U4)]
            public uint dwServiceType;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwCurrentState;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwControlsAccepted;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwWin32ExitCode;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwServiceSpecificExitCode;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwCheckPoint;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwWaitHint;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwProcessId;

            [MarshalAs(UnmanagedType.U4)]
            public uint dwServiceFlags;
        }

        internal const int ERROR_INSUFFICIENT_BUFFER = 0x7a;
        internal const int SC_STATUS_PROCESS_INFO = 0;

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool QueryServiceStatusEx(SafeHandle hService, int infoLevel, IntPtr lpBuffer, uint cbBufSize, out uint pcbBytesNeeded);

        public static bool GetPerformanceCounterByServiceName(string ServiceName)
        {
            ServiceController[] scServices;
            scServices = ServiceController.GetServices();
            foreach (ServiceController service in scServices)
            {
                if (service.ServiceName != ServiceName)
                    continue;

                int pid = GetServiceProcessId(service);

                if (pid == -1 || pid == 0)
                    return false;

                string instanceName = GetProcessInstanceName(pid);

                if (instanceName == "missing")
                    return false;

                string perfCategory = "Process";
                string perfCounterName = "Handle Count";
                string perfCounterType = "";
                float perfCounterValue = GetPerformanceCounterValueByInstance(perfCategory, perfCounterName, instanceName, out perfCounterType, 5);

                AddPerfCounterToPerfData(ServiceName, perfCategory, perfCounterName, perfCounterValue);

                perfCounterName = "Thread Count";
                perfCounterValue = GetPerformanceCounterValueByInstance(perfCategory, perfCounterName, instanceName, out perfCounterType, 5);

                AddPerfCounterToPerfData(ServiceName, perfCategory, perfCounterName, perfCounterValue);


                break;
            }
            return true;
        }

        

        public static void AddPerfCounterToPerfData(string ServiceName, string perfCategory, string perfCounterName, float perfCounterValue)
        {
            Checks.listPerfData.Add(ReplaceSpaceWithUnderscore(ServiceName) + "-" + ReplaceSpaceWithUnderscore(perfCategory) + "-" + ReplaceSpaceWithUnderscore(perfCounterName)
                    + "::" + ReplaceSpaceWithUnderscore(perfCategory) + "-" + ReplaceSpaceWithUnderscore(perfCounterName)
                    + "::'" + ReplaceSpaceWithUnderscore(perfCounterName) + "'=" + perfCounterValue + " ");
        }

        public static string ReplaceSpaceWithUnderscore(string input)
        {
            return input.Replace(" ", "_");
        }

        public static int GetServiceProcessId(ServiceController sc)
        {
            if (sc == null)
                throw new ArgumentNullException("sc");

            IntPtr zero = IntPtr.Zero;

            try
            {
                UInt32 dwBytesNeeded;
                // Call once to figure the size of the output buffer.
                QueryServiceStatusEx(sc.ServiceHandle, SC_STATUS_PROCESS_INFO, zero, 0, out dwBytesNeeded);
                if (Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
                {
                    // Allocate required buffer and call again.
                    zero = Marshal.AllocHGlobal((int)dwBytesNeeded);

                    if (QueryServiceStatusEx(sc.ServiceHandle, SC_STATUS_PROCESS_INFO, zero, dwBytesNeeded, out dwBytesNeeded))
                    {
                        var ssp = new SERVICE_STATUS_PROCESS();
                        Marshal.PtrToStructure(zero, ssp);
                        return (int)ssp.dwProcessId;
                    }
                }
            }
            catch (Exception e)
            {
                if (Settings.bDebug)
                    Console.WriteLine("Exception: " + e); 
            }
            finally
            {
                if (zero != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(zero);
                }
            }
            return -1;
        }

        public static void PopulateCounterMaps()
        {
            if (Settings.bVerbose)
                Console.WriteLine("INFO: Populating CounterMaps for Localization");
            using (var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Perflib\009"))
            {
                var counter = regKey.GetValue("Counter") as string[];
                for (var i = 0; i < counter.Count() - 1; i += 2)
                {
                    counterMapEnglish.Add(Convert.ToInt32(counter[i]), counter[i + 1]);
                }
            }

            using (var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Perflib\CurrentLanguage"))
            {
                var counter = regKey.GetValue("Counter") as string[];
                for (var i = 0; i < counter.Count() - 1; i += 2)
                {
                    counterMapCurrentLanguage.Add(Convert.ToInt32(counter[i]), counter[i + 1]);
                }
            }
        }


        public static float GetUpTime()
        {
            string perfCategory = "System";
            string perfCounterName = "System Up Time";
            string perfCounterType = "";
            float result = GetPerformanceCounterValue(perfCategory, perfCounterName, out perfCounterType, 0);

            if (Settings.bVerbose)
                Console.WriteLine("System\\System Up Time: " + result + " type: " + perfCounterType);

            return -1;
        }

        public static string GetProcessInstanceName(int pid)
        {
            string perfCategory = "Process";
            string perfCounterName = "ID Process";
            try
            {
                PerformanceCounterCategory cat = new PerformanceCounterCategory(perfCategory);

                string[] instances = cat.GetInstanceNames();
                foreach (string instance in instances)
                {
                    using (PerformanceCounter cnt = new PerformanceCounter(perfCategory, perfCounterName, instance, true))
                    {
                        int val = (int)cnt.RawValue;
                        if (val == pid)
                        {
                            return instance;
                        }
                    }
                }
                throw new Exception("Could not find performance counter " +
                        "instance name for current process. This is truly strange ...");

            }
            catch
            {
                try
                {
                    perfCategory = LookupPerfNameByName(perfCategory);
                    perfCounterName = LookupPerfNameByName(perfCounterName);

                    PerformanceCounterCategory cat = new PerformanceCounterCategory(perfCategory);

                    string[] instances = cat.GetInstanceNames();
                    foreach (string instance in instances)
                    {
                        using (PerformanceCounter cnt = new PerformanceCounter(perfCategory, perfCounterName, instance, true))
                        {
                            int val = (int)cnt.RawValue;
                            if (val == pid)
                            {
                                return instance;
                            }
                        }
                    }
                    throw new Exception("Could not find performance counter " +
                        "instance name for current process. This is truly strange ...");
                }
                catch (Exception f)
                {
                    Console.WriteLine(f);
                }
            }
            return "missing";

        }

        private static float GetPerformanceCounterValueByInstance(string perfCategory, string perfCounterName, string perfInstanceName, out string PerfCounterType, int sleep = 50)
        {
            PerfCounterType = "";

            try
            {
                PerformanceCounter pc = new PerformanceCounter(perfCategory, perfCounterName, perfInstanceName);
                pc.NextValue();
                Thread.Sleep(sleep);
                try
                {
                    PerfCounterType = pc.CounterType.ToString();
                    return pc.NextValue();
                }
                finally
                {
                    pc.Close();
                }
            }
            catch
            {
                try
                {
                    // Translate the category and countername to CurrentLanguage
                    perfCategory = LookupPerfNameByName(perfCategory);
                    perfCounterName = LookupPerfNameByName(perfCounterName);

                    PerformanceCounter pc = new PerformanceCounter(perfCategory, perfCounterName, perfInstanceName);
                    pc.NextValue();
                    Thread.Sleep(sleep);
                    try
                    {
                        PerfCounterType = pc.CounterType.ToString();
                        return pc.NextValue();
                    }
                    finally
                    {
                        pc.Close();
                    }
                }
                catch
                {
                    // I give up, didnt manage to figure out the correct name for the PerformanceCounter.
                    Console.WriteLine("ERROR: Error looking up PerformanceCounter '" + perfCategory + "\\" + perfCounterName + "' for " + perfInstanceName + "'");
                    return -1;
                }
            }
        }

        //private static float GetPerformanceCounterValueByInstanceDeux(string perfCategory, string perfCounterName, string perfInstanceName, out string PerfCounterType, int sleep = 50)
        //{
        //    pCounter = new PerformanceCounter();
        //    PerfCounterType = "";

        //    try
        //    {
        //        pCounter.CategoryName = perfCategory;
        //        pCounter.CounterName = perfCounterName;
        //        pCounter.InstanceName = perfInstanceName;

        //        try
        //        {
        //            pCounter.NextValue();
        //            pFirstSample = pCounter.NextSample();
        //            Thread.Sleep(sleep);
        //            float nv = pCounter.NextValue();
        //            pSecondSample = pCounter.NextSample();
        //            float avg = CounterSample.Calculate(pFirstSample, pSecondSample);

        //            Console.WriteLine("{0}, {1}", nv, avg);
        //            return avg;
        //        }
        //        finally
        //        {
        //            pCounter.Dispose();
        //        }
        //    }
        //    catch
        //    {
        //        try
        //        {
        //            PerformanceCounter pc = new PerformanceCounter(LookupPerfNameByName(perfCategory), LookupPerfNameByName(perfCounterName), perfInstanceName);
        //            pc.NextValue();
        //            Thread.Sleep(sleep);
        //            try
        //            {
        //                PerfCounterType = pc.CounterType.ToString();
        //                return pc.NextValue();
        //            }
        //            finally
        //            {
        //                pc.Dispose();
        //            }
        //        }
        //        catch
        //        {
        //            // I give up, didnt manage to figure out the correct name for the PerformanceCounter.
        //            Console.WriteLine("ERROR: Error looking up PerformanceCounter '" + perfCategory + "\\" + perfCounterName + "' for " + perfInstanceName + "'");
        //            return -1;
        //        }
        //    }
        //}

        public static string GetPerformanceCounterValueAsString(string perfCategory, string perfCounterName, out string PerfCounterType, int sleep = 50)
        {
            //float badresult = -1;
            try
            {
                PerformanceCounter pc = new PerformanceCounter(perfCategory, perfCounterName);
                pc.NextValue();
                Console.WriteLine(pc.NextSample());
                Thread.Sleep(sleep);
                try
                {
                    PerfCounterType = pc.CounterType.ToString();
                    return pc.NextValue().ToString("0.0");
                }
                finally
                {
                    pc.Dispose();
                }

                //return pc.RawValue;
            }
            catch
            {
                try
                {
                    PerformanceCounter pc = new PerformanceCounter(LookupPerfNameByName(perfCategory), LookupPerfNameByName(perfCounterName));
                    pc.NextValue();
                    Thread.Sleep(sleep);

                    try {                       
                        PerfCounterType = pc.CounterType.ToString();
                        return pc.NextValue().ToString("0.0");
                    }
                    finally
                    {
                        pc.Dispose();
                    }
                }
                catch
                {
                    // I give up, didnt manage to figure out the correct name for the PerformanceCounter.
                    Console.WriteLine("ERROR: Error looking up PerformanceCounter '" + perfCategory + "\\" + perfCounterName + "'");
                    PerfCounterType = "missing";
                    return "missing";
                }
            }
        }

        public static float GetPerformanceCounterValue(string perfCategory, string perfCounterName, out string PerfCounterType, int sleep = 50)
        {
            //float badresult = -1;
            try
            {
                PerformanceCounter pc = new PerformanceCounter(perfCategory, perfCounterName);
                pc.NextValue();
                Thread.Sleep(sleep);
                PerfCounterType = pc.CounterType.ToString();
                return pc.NextValue();

                //return pc.RawValue;
            }
            catch
            {
                try
                {
                    PerformanceCounter pc = new PerformanceCounter(LookupPerfNameByName(perfCategory), LookupPerfNameByName(perfCounterName));
                    pc.NextValue();
                    Thread.Sleep(sleep);
                    PerfCounterType = pc.CounterType.ToString();
                    return pc.NextValue(); //pc.RawValue;
                }
                catch
                {
                    // I give up, didnt manage to figure out the correct name for the PerformanceCounter.
                    Console.WriteLine("ERROR: Error looking up PerformanceCounter '" + perfCategory + "\\" + perfCounterName + "'");
                    PerfCounterType = "missing";
                    return -1;
                }
            }
        }

        private static string LookupPerfNameByName(string translateString)
        {
            int index = LookupPerfIndexByName(translateString, counterMapEnglish);
            string resultString = LookupPerfNameByIndex(index, counterMapCurrentLanguage);

            if (Settings.bDebug)
                Console.WriteLine("INFO: Translating PerfCounterText: '" + translateString + "' => '" + index + "' => '" + resultString + "'");

            return resultString;
        }

        private static int LookupPerfIndexByName(string Name, Dictionary<int, string> dict)
        {
            foreach (var entry in dict)
            {
                if (Name == entry.Value)
                    return Convert.ToInt32(entry.Key);
            }
            return 0; // No indexes have been found to be 0, so this should be checked for if is returned
        }

        private static string LookupPerfNameByIndex(int Index, Dictionary<int, string> dict)
        {
            foreach (var entry in dict)
            {
                if (Index == entry.Key)
                    return entry.Value;
            }
            return "missing";
        }
    }
}