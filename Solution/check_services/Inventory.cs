using Icinga;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;

namespace check_services
{
    internal class Inventory
    {
        private enum StartType : int
        {
            Boot = 0,
            System = 1,
            Automatic = 2,
            Manual = 3,
            Disabled = 4
        }

        public static List<WinServiceDefined> listWinServicesFromDefinition = new List<WinServiceDefined>();
        public static List<WinServiceActual> listWinServicesOnComputer = new List<WinServiceActual>();
        public static List<string> listServicesWithElevatedNeeds = new List<string>();
        public static List<string> listServicesWithoutPerfCounters = new List<string>();

        public static bool ServicesOnMachine()
        {
            ServiceController[] scServices;
            scServices = ServiceController.GetServices();

            int z = 1;
            int y = 1;
            int test = 0;

            string strCategory = "ThirdParty";

            // Read the actual state of things.
            foreach (ServiceController scService in scServices)
            {
                try
                {
                    string sServiceName = scService.ServiceName.ToString();
                    string sDisplayName = scService.DisplayName.ToString();
                    bool bMatchedService = false;

                    // Skip service for inventory if we only scare about running services in the inventory output.
                    if (Settings.bDoInvAllRunningOnly == true && Settings.bDoInventory == true && scService.Status.ToString() != ServiceControllerStatus.Running.ToString())
                    {
                        if (Settings.bVerbose == true)
                            Console.WriteLine("INFO: Service is not running, skipping: " + sServiceName);

                        continue;
                    }

                    // Skip all services that match excluderules
                    if (Settings.ExcludedServices.Contains(sServiceName) && Settings.bDefaultExcludeList == false)
                    {
                        if (Settings.bVerbose == true)
                            Console.WriteLine("INFO: Service in exclude list, skipping: " + sServiceName);

                        continue;
                    }

                    // Skip all services not set to include
                    if (Settings.bDefaultIncludeList == true)
                    {
                        if (Settings.bDebug == true)
                            Console.WriteLine("DEBUG: Included service: " + sServiceName);

                        bMatchedService = true;
                    }
                    else if (!Settings.IncludedServices.Contains(sServiceName) && Settings.bDefaultIncludeList == false)
                    {
                        if (Settings.bDebug == true)
                            Console.WriteLine("INFO: Service not in Settings.IncludedServices: " + sServiceName);

                        continue;
                    }
                    else if (Settings.IncludedServices.Contains(sServiceName) && Settings.bDefaultIncludeList == false)
                    {
                        if (Settings.bVerbose == true)
                            Console.WriteLine("INFO: Included service: " + sServiceName);

                        bMatchedService = true;
                    }

                    strCategory = ServiceCategoryLookup(sServiceName);

                    // Match if the returned category matches the service, if it does not then skip if its not already included
                    if (Settings.Categories.Contains(strCategory))
                    {
                        if (Settings.bVerbose)
                        {
                            Console.WriteLine("DEBUG: Service '" + sServiceName + "'matching category '" + strCategory + "'");
                        }
                    }
                    else if (Settings.bDefaultCategoriesList == false && Settings.bDoSingleCheck == false)
                    {
                        if (Settings.bVerbose)
                            Console.WriteLine("INFO: Skipping service due to category not matched: " + sServiceName);
                        continue;
                    }
                    else if (bMatchedService == false)
                    {
                        if (Settings.bVerbose)
                            Console.WriteLine("INFO: Skipping service '" + sServiceName + "' due to not included via Category or IncludedServices");

                        continue;
                    }

                    List<string> listDependentServices = new List<string>();
                    List<string> listServicesDependedOn = new List<string>();

                    foreach (var DependentService in scService.DependentServices)
                    {
                        try
                        {
                            listDependentServices.Add(DependentService.ServiceName.ToString());
                        }
                        catch (Exception e)
                        {
                            if (Settings.bVerbose || Settings.bDebug)
                                Console.WriteLine("ERROR: Looking up DependentService for '" + sServiceName + "' resulted in an exception, it is likely not installed:" + e);
                        }
                    }

                    foreach (var ServiceDependedOn in scService.ServicesDependedOn)
                    {
                        try
                        {
                            listServicesDependedOn.Add(ServiceDependedOn.ServiceName.ToString());
                        }
                        catch (Exception e)
                        {
                            if (Settings.bVerbose || Settings.bDebug)
                                Console.WriteLine("ERROR: Looking up DepdendendOn for '" + sServiceName + "' resulted in an exception, it is likely not installed:" + e);
                        }
                    }

                    Array dependentServices = listDependentServices.ToArray();
                    Array servicesDependedOn = listServicesDependedOn.ToArray();

                    int iRegKeyStart = 0;
                    bool bRegKeyDelayedAutoStart;
                    bool bRegKeyWOW64;
                    string strObjectName;
                    string strFileOwner = "";
                    string strImagePath = "";
                    string strResolvedImagePath = "";

                    test = ServiceStartupMode(sServiceName, out iRegKeyStart, out bRegKeyDelayedAutoStart, out bRegKeyWOW64, out strObjectName, out strFileOwner, out strImagePath, out strResolvedImagePath);
                    ServiceControllerStatus serviceStatus = scService.Status;

                    // Store the service to the list.
                    Inventory.listWinServicesOnComputer.Add(new WinServiceActual(sServiceName, sDisplayName, Enum.GetName(typeof(StartType), iRegKeyStart), bRegKeyDelayedAutoStart, bRegKeyWOW64, strObjectName, serviceStatus.ToString(), strCategory, strFileOwner, strImagePath, strResolvedImagePath, dependentServices, servicesDependedOn));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception occured: " + e);
                    return false;
                }
            }
            return true;
        }

        public static string ServiceCategoryLookup(string serviceName)
        {
            if (serviceName != "")
            {
                if (Settings.bDefaultSystemCategory == false && Settings.services_in_system_category.Contains(serviceName))
                    return "System";

                if (Settings.bDefaultEssentialCategory == false && Settings.services_in_essential_category.Contains(serviceName))
                    return "Essential";

                if (Settings.bDefaultRoleCategory == false && Settings.services_in_role_category.Contains(serviceName))
                    return "Role";

                if (Settings.bDefaultSupportingCategory == false && Settings.services_in_supporting_category.Contains(serviceName))
                    return "Supporting";

                if (Settings.bDefaultThirdPartyCategory == false && Settings.services_in_thirdparty_category.Contains(serviceName))
                    return "ThirdParty";

                if (Settings.bDefaultIgnoredCategory == false && Settings.services_in_ignored_category.Contains(serviceName))
                    return "Ignored";

                Dictionary<String, WinServiceDefined> listOverDefinedServices = Inventory.listWinServicesFromDefinition.ToDictionary(o => o.ServiceName, o => o);
                foreach (var DefinedService in listOverDefinedServices)
                {
                    var DefinedServiceValue = DefinedService.Value;
                    if (DefinedServiceValue.ServiceName == serviceName)
                    {
                        if (Settings.bDebug)
                            Console.WriteLine("DEBUG: Found service '" + serviceName + "' with service definition '" + DefinedServiceValue.ServiceCategory.ToString() + "'");

                        return DefinedServiceValue.ServiceCategory.ToString();
                    }
                }

                return "ThirdParty";
            }
            return "errorInServiceCategoryLookup";
        }

        public static int ServiceStartupMode(string service, out int iRegKeyStart, out bool bRegKeyDelayedAutoStart, out bool bRegKeyWOW64, out string strObjectName, out string strFileOwner, out string strImagePath, out string strResolvedImagePath)
        {
            string key;

            //string strImagePath = "";

            // Read Start value
            key = "Start";
            RegReadIntFromHKLMService(service, key, out iRegKeyStart);

            key = "DelayedAutoStart";
            RegReadBoolFromHKLMService(service, key, out bRegKeyDelayedAutoStart);

            key = "ObjectName";
            RegReadStringFromHKLMService(service, key, out strObjectName);

            if (Settings.strInventoryLevel == "full")
            {
                // Read WOW64 value
                key = "WOW64";
                //RegReadIntFromHKLMService(service, key, out iRegKeyWOW64);
                RegReadBoolFromHKLMService(service, key, out bRegKeyWOW64);

                // Read ImagePath value
                key = "ImagePath";
                RegReadStringFromHKLMService(service, key, out strImagePath);

                // Try to find the correct path to the service, so we can find the owner of the file.
                try
                {
                    // Clean up any arguments from the ImagePath
                    strResolvedImagePath = strImagePath.Trim('"');
                    strResolvedImagePath = strResolvedImagePath.Substring(0, strResolvedImagePath.IndexOf(".exe") + 4);
                    strResolvedImagePath = strResolvedImagePath.Trim();

                    if (!File.Exists(@strResolvedImagePath))
                    {
                        // If WOW64 flag set, this is usually only in 64bit environments, if its 32bit it "should" have been found in the previous test already.
                        if (bRegKeyWOW64)
                        {
                            // Trying to guess if it is inside syswow64 due to 32bit flag set (64bit system expected).
                            string WinDir = Environment.ExpandEnvironmentVariables("%WinDir%");
                            string SYSWOW64Path = WinDir + @"\syswow64";
                            string SYSTEM32Path = WinDir + @"\system32";
                            string resolvedpath = strResolvedImagePath.Replace(SYSTEM32Path, SYSWOW64Path);

                            if (!File.Exists(@resolvedpath))
                            {
                                strResolvedImagePath = "Unable to locate file" + @strResolvedImagePath;
                                strFileOwner = "";
                                return (int)ServiceState.ServiceUnknown;
                            }
                            else
                            {
                                strResolvedImagePath = @resolvedpath;
                            }
                        }
                        else
                        {
                            // Console.WriteLine("Unable to find file:" + strImagePath + "!");
                            strResolvedImagePath = "Unable to locate file" + @strResolvedImagePath;
                            strFileOwner = "";
                            return (int)ServiceState.ServiceUnknown;
                        }
                    }

                    //Console.WriteLine("\tResolvedImagePath:\t" + strImagePath);
                    var fs = File.GetAccessControl(@strResolvedImagePath);

                    var sid = fs.GetOwner(typeof(SecurityIdentifier));

                    var ntAccount = sid.Translate(typeof(NTAccount));
                    strFileOwner = ntAccount.ToString();
                    return (int)ServiceState.ServiceOK;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error while finding the executable or owner:" + e);
                }
            }
            strFileOwner = "";
            strImagePath = "";
            strResolvedImagePath = "";
            bRegKeyWOW64 = false;
            return (int)ServiceState.ServiceOK;
        }

        public static int RegReadIntFromHKLMService(string service, string key, out int value)
        {
            value = 0;
            try
            {
                using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + service))
                {
                    value = (int)regKey.GetValue(key);
                }
            }
            catch (Exception e)
            {
                if (Settings.bDebug)
                    Console.WriteLine("DEBUG: Did not read " + key + " from Registry, it is likely missing, returning value of '" + value + "', this is normal.");
                if (Settings.bDebug && Settings.bVerbose)
                    Console.WriteLine("DEBUG: Stacktrace: " + e);
            }
            return value;
        }

        public static bool RegReadBoolFromHKLMService(string service, string key, out bool value)
        {
            value = false;
            try
            {
                using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + service))
                {
                    value = (bool)regKey.GetValue(key);
                }
            }
            catch (Exception e)
            {
                if (Settings.bDebug)
                    Console.WriteLine("DEBUG: Did not read " + key + " from Registry, it is likely missing, returning value of '" + value + "', this is normal.");
                if (Settings.bDebug && Settings.bVerbose)
                    Console.WriteLine("DEBUG: Stacktrace: " + e);
            }
            return value;
        }

        public static string RegReadStringFromHKLMService(string service, string key, out string value)
        {
            value = "missing value";
            try
            {
                using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + service))
                {
                    value = (string)regKey.GetValue(key);
                }
            }
            catch (Exception e)
            {
                if (Settings.bDebug)
                    Console.WriteLine("DEBUG: Did not read " + key + " from Registry, it is likely missing, returning value of '" + value + "', this is normal.");
                if (Settings.bDebug && Settings.bVerbose)
                    Console.WriteLine("DEBUG: Stacktrace: " + e);
            }
            return value;
        }

        public static bool InsertDefaultServiceDefinitions()
        {
            // Adds default services

            // Adds default services

            // System
            listWinServicesFromDefinition.Add(new WinServiceDefined("BFE", "Base Filtering Engine", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("BrokerInfrastructure", "Background Tasks Infrastructure Service", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DcomLaunch", "DCOM Server Process Launcher", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LSM", "Local Session Manager", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Power", "Power", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SamSs", "Security Accounts Manager", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Schedule", "Task Scheduler", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinDefend", "Windows Defender Service", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("BITS", "Background Intelligent Transfer Service", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("COMSysApp", "COM+ System Application", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DPS", "Diagnostic Policy Service", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("EventLog", "Windows Event Log", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("EventSystem", "COM+ Event System", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("iphlpsvc", "IP Helper", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LanmanServer", "Server", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LanmanWorkstation", "Workstation", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MpsSvc", "Windows Firewall", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Netlogon", "Netlogon", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("pla", "Performance Logs & Alerts", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ProfSvc", "User Profile Service", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RpcEptMapper", "RPC Endpoint Mapper", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RpcSs", "Remote Procedure Call (RPC)", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Winmgmt", "Windows Management Instrumentation", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinRM", "Windows Remote Management (WS-Management)", "System", "Automatic", "Running"));

            // Role
            listWinServicesFromDefinition.Add(new WinServiceDefined("adfssrv", "Active Directory Federation Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ADWS", "Active Directory Web Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("BITSCompactServer", "BITS Compact Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("c2wts", "Claims to Windows Token Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CertSvc", "Active Directory Certificate Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ClusSvc", "Cluster Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ddpsvc", "Data Deduplication Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ddpvssvc", "Data Deduplication Volume Shadow Copy Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Dfs", "DFS Namespace", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DFSR", "DFS Replication", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DNS", "DNS Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("drs", "Device Registration Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Eaphost", "Extensible Authentication Protocol", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Fax", "Fax", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("fdPHost", "Function Discovery Provider Host", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("FDResPub", "Function Discovery Resource Publication", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("fssagent", "Microsoft File Server Shadow Copy Agent Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("hkmsvc", "Health Key and Certificate Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IAS", "Network Policy Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IISADMIN", "IIS Admin Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IKEEXT", "IKE and AuthIP IPsec Keying Modules", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IsmServ", "Intersite Messaging", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Kdc", "Kerberos Key Distribution Center", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KdsSvc", "Microsoft Key Distribution Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KeyIso", "CNG Key Isolation", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KPSSVC", "KDC Proxy Server service (KPS)", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KtmRm", "KtmRm for Distributed Transaction Coordinator", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LPDSVC", "LPD Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSDTC", "Distributed Transaction Coordinator", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSiSCSI", "Microsoft iSCSI Initiator Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSiSNS", "Microsoft iSNS Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSMQ", "Message Queuing", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSMQTriggers", "Message Queuing Triggers", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSSQL$MICROSOFT##WID", "Windows Internal Database", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSStrgSvc", "Windows Standards-Based Storage Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetMsmqActivator", "Net.Msmq Listener Adapter", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetPipeActivator", "Net.Pipe Listener Adapter", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetTcpActivator", "Net.Tcp Listener Adapter", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetTcpPortSharing", "Net.Tcp Port Sharing Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NTDS", "Active Directory Domain Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NfsService", "Server for NFS", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NtFrs", "File Replication", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("OcspSvc", "Online Responder Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PeerDistSvc", "BranchCache", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PNRPAutoReg", "PNRP Machine Name Publication Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PNRPsvc", "Peer Name Resolution Protocol", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PrintNotify", "Printer Extensions and Notifications", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RaMgmtSvc", "Remote Access Management service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RasAuto", "Remote Access Auto Connection Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RasMan", "Remote Access Connection Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RDMS", "Remote Desktop Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RemoteAccess", "Routing and Remote Access", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RPCHTTPLBS", "RPC/HTTP Load Balancing Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RpcLocator", "Remote Procedure Call (RPC) Locator", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("rqs", "Remote Access Quarantine Agent", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SessionEnv", "Remote Desktop Configuration", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("simptcp", "Simple TCP/IP Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SmbHash", "SMB Hash Generation Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SmbWitness", "SMB Witness", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("smphost", "Microsoft Storage Spaces SMP", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SMTPSVC", "Simple Mail Transfer Protocol (SMTP)", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SNMP", "SNMP Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SrmReports", "File Server Storage Reports Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SrmSvc", "File Server Resource Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SstpSvc", "Secure Socket Tunneling Protocol Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("stisvc", "Windows Image Acquisition (WIA)", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("StorSvc", "Storage Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("svsvc", "Spot Verifier", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SyncShareSvc", "Windows Sync Share", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SyncShareTTSvc", "Sync Share Token Translation Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TapiSrv", "Telephony", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TermService", "Remote Desktop Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TermServLicensing", "Remote Desktop Licensing", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("THREADORDER", "Thread Ordering Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TieringEngineService", "Storage Tiers Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TimeBroker", "Time Broker", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TlntSvr", "Telnet", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TPAutoConnSvc", "TP AutoConnect Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TPVCGateway", "TP VC Gateway Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TrkWks", "Distributed Link Tracking Client", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TScPubRPC", "RemoteApp and Desktop Connection Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TSGateway", "Remote Desktop Gateway", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Tssdis", "Remote Desktop Connection Broker", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("UmRdpService", "Remote Desktop Services UserMode Port Redirector", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("w3logsvc", "W3C Logging Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("W3SVC", "World Wide Web Publishing Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WAS", "Windows Process Activation Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wbengine", "Block Level Backup Engine Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WbioSrvc", "Windows Biometric Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WDSServer", "Windows Deployment Services Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WEPHOSTSVC", "Windows Encryption Provider Host Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wercplsupport", "Problem Reports and Solutions Control Panel Support", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WerSvc", "Windows Error Reporting Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WFFSvc", "Windows Feedback Forwarder Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WiaRpc", "Still Image Acquisition Events", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WIDWriter", "Windows Internal Database VSS Writer", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinTarget", "Microsoft iSCSI Software Target", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WSusCertServer", "WSUS Certificate Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WsusService", "WSUS Service", "Role", "Automatic", "Running"));

            // Supporting
            listWinServicesFromDefinition.Add(new WinServiceDefined("AeLookupSvc", "Application Experience", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppHostSvc", "Application Host Helper Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppIDSvc", "Application Identity", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Appinfo", "Application Information", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppMgmt", "Application Management", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("aspnet_state", "ASP.NET State Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AudioEndpointBuilder", "Windows Audio Endpoint Builder", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Audiosrv", "Windows Audio", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AxInstSV", "ActiveX Installer (AxInstSV)", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("BDESVC", "BitLocker Drive Encryption Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Browser", "Computer Browser", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CertPropSvc", "Certificate Propagation", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CryptSvc", "Cryptographic Services", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CscService", "Offline Files", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("defragsvc", "Optimize drives", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DeviceAssociationService", "Device Association Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DeviceInstall", "Device Install Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Dhcp", "DHCP Client", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DiagTrack", "Diagnostics Tracking Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("FontCache", "Windows Font Cache Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("gpsvc", "Group Policy Client", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IEEtwCollectorService", "Internet Explorer ETW Collector Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("lmhosts", "TCP/IP NetBIOS Helper", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MMCSS", "Multimedia Class Scheduler", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("napagent", "Network Access Protection Agent", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NcaSvc", "Network Connectivity Assistant", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NcbService", "Network Connection Broker", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Netman", "Network Connections", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("netprofm", "Network List Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NlaSvc", "Network Location Awareness", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("nsi", "Network Store Interface Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("p2pimsvc", "Peer Networking Identity Manager", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PerfHost", "Performance Counter DLL Host", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PlugPlay", "Plug and Play", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PolicyAgent", "IPsec Policy Agent", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("QWAVE", "Quality Windows Audio Video Experience", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RemoteRegistry", "Remote Registry", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RSoPProv", "Resultant Set of Policy Provider", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("sacsvr", "Special Administration Console Helper", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SCardSvr", "Smart Card", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ScDeviceEnum", "Smart Card Device Enumeration Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SCPolicySvc", "Smart Card Removal Policy", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("seclogon", "Secondary Logon", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SENS", "System Event Notification Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SharedAccess", "Internet Connection Sharing (ICS)", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ShellHWDetection", "Shell Hardware Detection", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Spooler", "Print Spooler", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("sppsvc", "Software Protection", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SSDPSRV", "SSDP Discovery", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("swprv", "Microsoft Software Shadow Copy Provider", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SystemEventsBroker", "System Events Broker", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Themes", "Themes", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("UALSVC", "User Access Logging Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("UI0Detect", "Interactive Services Detection", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("VaultSvc", "Credential Manager", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vds", "Virtual Disk", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicguestinterface", "Hyper-V Guest Service Interface", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmickvpexchange", "Hyper-V Data Exchange Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicrdv", "Hyper-V Remote Desktop Virtualization Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("W32Time", "Windows Time", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Wcmsvc", "Windows Connection Manager", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WcsPlugInService", "Windows Color System", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WdiServiceHost", "Diagnostic Service Host", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WdiSystemHost", "Diagnostic System Host", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WebClient", "WebClient", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinHttpAutoProxySvc", "WinHTTP Web Proxy Auto-Discovery Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wlidsvc", "Microsoft Account Sign-in Assistant", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wmiApSrv", "WMI Performance Adapter", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WPDBusEnum", "Portable Device Enumerator Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WSearch", "Windows Search", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WSService", "Windows Store Service (WSService)", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wuauserv", "Windows Update", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wudfsvc", "Windows Driver Foundation - User-mode Driver Framework", "Supporting", "Automatic", "Running"));

            // Managed services
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetBackup Client Service", "NetBackup Client Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("mtstrmd", "NetBackup Deduplication Multi-Threaded Agent", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetBackup Discovery Framework", "NetBackup Discovery Framework", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetBackup Legacy Client Service", "NetBackup Legacy Client Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetBackup Legacy Network Service", "NetBackup Legacy Network Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetBackup Proxy Service", "NetBackup Proxy Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetBackup SAN Client Fibre Transport Service", "NetBackup SAN Client Fibre Transport Service", "Managed", "Automatic", "Stopped"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("puppet", "Puppet Agent", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("nxlog", "nxlog", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("icinga2", "Icinga 2", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SnowInventoryClient", "Snow Inventory Client", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SepMasterService", "Symantec Endpoint Protection", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SmcService", "Symantec Management Client", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SNAC", "Symantec Network Access Control", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("VRTSpbx", "Symantec Private Branch Exchange", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicheartbeat", "Hyper-V Heartbeat Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicshutdown", "Hyper-V Guest Shutdown Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmictimesync", "Hyper-V Time Synchronization Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("VMTools", "VMware Tools", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ietsms", "Intel Ethernet thermal Sensor Monitor Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("HpAmsStor", "HP AMS Storage Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CIMnotify", "HP Insight Event Notifier", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CqMgHost", "P Insight Foundation Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CpqNicMgmt", "HP Insight NIC Agents", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CqMgServ", "HP Insight Server Agents", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CqMgStor", "HP Insight Storage Agents", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("hpqams", "HP ProLiant Agentless Management Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ProLiantMonitor", "HP ProLiant Health Monitor Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("sysdown", "HP Proliant System Shutdown Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Cissesrv", "HP Smart Array SAS/SATA Event Notification Service", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SysMgmtHp", "HP System Management Homepage", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("cpqvcagent", "HP Version Control Agent", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("HPWMISTOR", "HP WMI Storage Providers", "Managed", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CcmExec", "SMS Agent Host", "Managed", "Automatic", "Stopped"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ccmsetup", "ccmsetup", "Managed", "Automatic", "Stopped"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("EMET_​Service", "Microsoft EMET Service", "Managed", "Automatic", "Running"));

            // Ignored services
            listWinServicesFromDefinition.Add(new WinServiceDefined("ALG", "Application Layer Gateway Service", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppReadiness", "App Readiness", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppXSvc", "AppX Deployment Service (AppXSVC)", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("dot3svc", "Wired AutoConfig", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DsmSvc", "Device Setup Manager", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("EFS", "Encrypting File System (EFS)", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("hidserv", "Human Interface Device Service", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("lltdsvc", "Link-Layer Topology Discovery Mapper", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("msiserver", "Windows Installer", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SNMPTRAP", "SNMP Trap", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SysMain", "Superfetch", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TabletInputService", "Touch Keyboard and Handwriting Panel Service", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TrustedInstaller", "Windows Modules Installer", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("upnphost", "UPnP Device Host", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicvss", "Hyper-V Volume Shadow Copy Requestor", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmvss", "VMware Snapshot Provider", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("VSS", "Volume Shadow Copy", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Wecsvc", "Windows Event Collector", "Ignored", "Automatic", "Running"));

            listWinServicesFromDefinition.Add(new WinServiceDefined("gupdate", "Google-​update-service (gupdate)​", "Ignored", "Automatic", "Stopped"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("clr_​optimization_​v4.​0.30319_​32", "Microsoft .NET Framework NGEN v4.​0.30319_​X86", "Ignored", "Automatic", "Stopped"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("clr_​optimization_​v4.​0.30319_​64", "Microsoft .NET Framework NGEN v4.​0.30319_​X64", "Ignored", "Automatic", "Stopped"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Dnscache", "DNS Client", "Ignored", "Automatic", "Running"));

            return true;
        }

        public static bool ImportServiceDefinitions()
        {
            if (Settings.strCategoryFilePath == "unspecified")
            {
                bool temp = InsertDefaultServiceDefinitions();
                return temp;
            }

            if (Settings.strCategoryFileFormat == "CSV")
                return ImportServiceDefinitionsCSV();

            return false;
        }

        public static bool ImportServiceDefinitionsCSV()
        {
            try
            {
                // Import the definition file.
                DataTable csvTable = GetDataTabletFromCSVFile(Settings.strCategoryFilePath);
                foreach (DataRow csvRow in csvTable.Rows)
                {
                    string sServiceNameCSV = (string)csvRow["ServiceName"];
                    string sDisplayNameCSV = (string)csvRow["DisplayName"];
                    string sStartTypeCSV = (string)csvRow["StartType"];
                    string sExpectedStatusCSV = (string)csvRow["ExpectedStatus"];
                    string sServiceCategoryCSV = (string)csvRow["ServiceCategory"];
                    string sExpectedStatus = CleanStatus(sExpectedStatusCSV);

                    listWinServicesFromDefinition.Add(new WinServiceDefined(sServiceNameCSV, sDisplayNameCSV, sServiceCategoryCSV, sStartTypeCSV, sExpectedStatus));
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occured: " + e);
                return false;
            }
        }

        public static int OutputReadable()
        {
            bool temp = true;

            // Import service definitions
            temp = Inventory.ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = Inventory.ServicesOnMachine();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Dictionary<String, WinServiceActual> listOverActualServices = Inventory.listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var ActualService in listOverActualServices)
            {
                var LocalService = ActualService.Value;
                string readable = ReadableSerializer.Serialize(LocalService, Settings.bDoHideEmptyVars);
                Console.WriteLine("Service: " + LocalService.ServiceName + ":\n" + readable);
                Console.WriteLine("");
            }

            return (int)ServiceState.ServiceOK;
        }

        public static int OutputCSV()
        {
            bool temp = true;

            // Import service definitions
            temp = Inventory.ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = Inventory.ServicesOnMachine();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Console.WriteLine("ServiceName,DisplayName,StartType,DelayedAutostart,ObjectName,ExpectedStatus,ServiceCategory,WOW64,FileOwner,ImagePath,ResolvedImagePath");

            Dictionary<String, WinServiceActual> listOverActualServices = Inventory.listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var ActualService in listOverActualServices)
            {
                var LocalService = ActualService.Value;

                Console.WriteLine("\"" + LocalService.ServiceName + "\",\"" + LocalService.DisplayName + "\"," + LocalService.StartType + "," +
                    LocalService.DelayedAutostart + ",\"" + LocalService.ObjectName + "\"," + LocalService.CurrentStatus + "," + LocalService.ServiceCategory +
                    "," + LocalService.WOW64 + ",\"" + LocalService.FileOwner + "\",\"" + LocalService.ImagePath + "\",\"" + LocalService.ResolvedImagePath + "\"");
            }

            return (int)ServiceState.ServiceOK;
        }

        public static int OutputJSON()
        {
            bool temp = true;

            // Import service definitions
            temp = Inventory.ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = Inventory.ServicesOnMachine();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Dictionary<String, WinServiceActual> listOverActualServices = Inventory.listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            string json = JsonConvert.SerializeObject(Inventory.listWinServicesOnComputer, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Console.WriteLine("Json: " + json);

            return (int)ServiceState.ServiceOK;
        }

        public static int OutputI2Conf()
        {
            bool temp = true;

            // Import service definitions
            temp = Inventory.ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = Inventory.ServicesOnMachine();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Dictionary<String, WinServiceActual> listOverActualServices = Inventory.listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var ActualService in listOverActualServices)
            {
                var LocalService = ActualService.Value;
                string i2conf = IcingaSerializer.Serialize(LocalService, Settings.bDoHideEmptyVars);
                Console.WriteLine("  vars.inv.windows.service[\"" + LocalService.ServiceName + "\"] = " + i2conf);
                Console.WriteLine("");
            }

            return (int)ServiceState.ServiceOK;
        }

        public static DataTable GetDataTabletFromCSVFile(string strCSVFilePath)
        {
            DataTable csvData = new DataTable();
            try
            {
                using (TextFieldParser csvReader = new TextFieldParser(strCSVFilePath))
                {
                    csvReader.SetDelimiters(new string[] { "," });
                    csvReader.HasFieldsEnclosedInQuotes = true;
                    //read column names
                    string[] colFields = csvReader.ReadFields();
                    foreach (string column in colFields)
                    {
                        DataColumn datecolumn = new DataColumn(column);
                        datecolumn.AllowDBNull = true;
                        csvData.Columns.Add(datecolumn);
                    }
                    while (!csvReader.EndOfData)
                    {
                        string[] fieldData = csvReader.ReadFields();
                        //Making empty value as null
                        for (int i = 0; i < fieldData.Length; i++)
                        {
                            if (fieldData[i] == "")
                            {
                                fieldData[i] = null;
                            }
                        }
                        csvData.Rows.Add(fieldData);
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return csvData;
        }

        public static string CleanStatus(string status)
        {
            // First test one way
            if (ServiceControllerStatus.Stopped.ToString() == status || ServiceControllerStatus.ContinuePending.ToString() == status
                || ServiceControllerStatus.Paused.ToString() == status || ServiceControllerStatus.PausePending.ToString() == status
                || ServiceControllerStatus.StartPending.ToString() == status || ServiceControllerStatus.StopPending.ToString() == status
                || ServiceControllerStatus.Running.ToString() == status)
            {
                return status;
            }
            // We need to try to guess the status and set the proper String for the OS language.
            else if (status == "Running")
                return ServiceControllerStatus.Running.ToString();
            else if (status == "Stopped")
                return ServiceControllerStatus.Stopped.ToString();
            else if (status == "ContinuePending")
                return ServiceControllerStatus.ContinuePending.ToString();
            else if (status == "Paused")
                return ServiceControllerStatus.Paused.ToString();
            else if (status == "PausedPending")
                return ServiceControllerStatus.PausePending.ToString();
            else if (status == "StartPending")
                return ServiceControllerStatus.StartPending.ToString();
            else if (status == "StopPending")
                return ServiceControllerStatus.StopPending.ToString();
            else
                Console.WriteLine("Unable to match Status '" + status + "' to ServiceControllerStatus");
            return "Unknown";
        }
    }
}