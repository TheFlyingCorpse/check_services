using Icinga;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;

namespace check_services
{
    internal class Checks
    {
        public static List<string> listPerfData = new List<string>();
        public static List<string> listServiceOutput = new List<string>();

        public static string outputServices = "";

        private static bool errorServices = false;

        public static int Services(int returncode)
        {
            bool temp;
            outputServices = "";

            bool bDelayedGracePeriod = false;
            bool bMatchedService = false;
            bool bIncludeCategoryInOutput = false;
            bool bWarningForServiceCategory = false;

            temp = Inventory.ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            temp = Inventory.ServicesOnMachine();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Time since bootup
            if (GetUpTime() < Settings.iDelayedGraceDuration)
                bDelayedGracePeriod = true;

            if (Settings.bDoHideCategoryFromOuput == false && Settings.Categories.Length >= 2)
                bIncludeCategoryInOutput = true;

            // Find Services that we have that is in the definition.
            Dictionary<String, WinServiceDefined> listOverDefinedServices = Inventory.listWinServicesFromDefinition.ToDictionary(o => o.ServiceName, o => o);
            Dictionary<String, WinServiceActual> listOverActualServices = Inventory.listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var Actualservices in listOverActualServices)
            {
                bMatchedService = false;
                bWarningForServiceCategory = false;

                WinServiceActual ActualService = Actualservices.Value;

                if (Settings.WarnCategories.Contains(ActualService.ServiceCategory))
                    bWarningForServiceCategory = true;

                // Single check services should bypass bDoCheckAllStartTypes check further down.
                if (Settings.bDoSingleCheck == true)
                {
                    returncode = CheckExpectedService(returncode, ActualService, bWarningForServiceCategory, bDelayedGracePeriod);
                    PerfData.ServiceStatusCounting(ActualService.CurrentStatus);
                    Program.listServicePerfCounters.Add(ActualService.ServiceName);
                    PerfData.iNumberOfServices++;
                    bMatchedService = true;
                    break;
                }

                // Skip past this service if we only check for services with Automatic StartMode regardless of anything else.
                if (Settings.bDoCheckAllStartTypes == false && ActualService.StartType != (string)ServiceStartMode.Automatic.ToString())
                {
                    if (Settings.bVerbose == true)
                        Console.WriteLine("Skipping, Service is not 'Automatic': " + ActualService.ServiceName);
                    continue;
                }

                // If match for stopped service
                if (Settings.StoppedServices.Contains(ActualService.ServiceName))
                {
                    returncode = CheckStoppedService(returncode, ActualService, bIncludeCategoryInOutput);
                    PerfData.ServiceStatusCounting(ActualService.CurrentStatus);
                    Program.listServicePerfCounters.Add(ActualService.ServiceName);
                    PerfData.iNumberOfServices++;
                    bMatchedService = true;
                    continue;
                }
                // If match for started service
                else if (Settings.RunningServices.Contains(ActualService.ServiceName))
                {
                    returncode = CheckRunningService(returncode, ActualService, bIncludeCategoryInOutput);
                    PerfData.ServiceStatusCounting(ActualService.CurrentStatus);
                    Program.listServicePerfCounters.Add(ActualService.ServiceName);
                    PerfData.iNumberOfServices++;
                    bMatchedService = true;
                    continue;
                }

                // Match for Defined service.
                foreach (var Definedservices in listOverDefinedServices)
                {
                    WinServiceDefined DefinedService = Definedservices.Value;

                    // If we have a match for a defined service.
                    if (ActualService.ServiceName == DefinedService.ServiceName)
                    {
                        returncode = CheckDefinedServices(returncode, ActualService, DefinedService, bDelayedGracePeriod, bIncludeCategoryInOutput, bWarningForServiceCategory);
                        PerfData.ServiceStatusCounting(ActualService.CurrentStatus);
                        Program.listServicePerfCounters.Add(ActualService.ServiceName);
                        PerfData.iNumberOfServices++;
                        bMatchedService = true;
                        break;
                    }

                    // Did not match, trying until end of list, will continue until match found (break) or no found (match Settings.categories)
                }

                // If match for the Category and it is a service that starts Automatically.
                if (Settings.Categories.Contains(ActualService.ServiceCategory) && ActualService.StartType == ServiceStartMode.Automatic.ToString() && bMatchedService == false)
                {
                    returncode = CheckCategories(returncode, ActualService, bDelayedGracePeriod, bIncludeCategoryInOutput, bWarningForServiceCategory);
                    PerfData.ServiceStatusCounting(ActualService.CurrentStatus);
                    Program.listServicePerfCounters.Add(ActualService.ServiceName);
                    PerfData.iNumberOfServices++;
                    continue;
                }
            }

            if (errorServices == false)
            {
                if (PerfData.iNumberOfServices == 0)
                {
                    outputServices = "No Services matched the filters given, or none exist on this server.";
                    returncode = (int)ServiceState.ServiceUnknown;
                }
                else if (PerfData.iNumberOfServices == 1)
                {
                    string tempOutput = string.Join(",", listServiceOutput.ToArray());
                    outputServices = tempOutput;
                }
                else
                {
                    outputServices = "All Services are in their correct states.";
                }
            }

            // Add perfdata to global PerfData list, we are done with checking what we must check.
            listPerfData.Add(" 'NumberOfServices'=" + PerfData.iNumberOfServices);
            listPerfData.Add(" 'NumberOfRunningServices'=" + PerfData.iNumberOfRunningServices + ";;;0;" + PerfData.iNumberOfServices);
            listPerfData.Add(" 'NumberOfStoppedServices'=" + PerfData.iNumberOfStoppedServices + ";;;0;" + PerfData.iNumberOfServices);
            listPerfData.Add(" 'NumberOfPendingServices'=" + PerfData.iNumberOfPendingServices + ";;;0;" + PerfData.iNumberOfServices);
            listPerfData.Add(" 'NumberOfPausedServices'=" + PerfData.iNumberOfPausedServices + ";;;0;" + PerfData.iNumberOfServices);
            listPerfData.Add(" 'NumberOfUnknownServices'=" + PerfData.iNumberOfUnknownServices + ";;;0;" + PerfData.iNumberOfServices);
            listPerfData.Add(" 'NumberOfCorrectServices'=" + PerfData.iNumberOfCorrectServices + ";;;0;" + PerfData.iNumberOfServices);
            listPerfData.Add(" 'NumberOfWrongServices'=" + PerfData.iNumberOfWrongServices + ";;;0;" + PerfData.iNumberOfServices);

            return returncode;
        }

        private static int CheckExpectedService(int returncode, WinServiceActual ActualService, bool bWarningForServiceCategory, bool bDelayedGracePeriod)
        {
            if (ActualService.CurrentStatus == Settings.strExpectedState)
            {
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                PerfData.iNumberOfCorrectServices++;
            }
            else if (ActualService.StartType == ServiceStartMode.Automatic.ToString() && ActualService.DelayedAutostart == true && bDelayedGracePeriod == true)
            {
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not yet in the expected state of '" + ActualService.CurrentStatus.ToString() + "', it is currently in '" + Settings.strExpectedState + "', it is within its grace period to start.");
                PerfData.iNumberOfPendingServices++;
            }
            else if (bWarningForServiceCategory == true)
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + Settings.strExpectedState + "'");
                returncode = (int)ServiceState.ServiceWarning;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + Settings.strExpectedState + "'");
                returncode = (int)ServiceState.ServiceCritical;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }

            return returncode;
        }

        private static int CheckDefinedServices(int returncode, WinServiceActual ActualService, WinServiceDefined DefinedService, bool bDelayedGracePeriod, bool bIncludeCategoryInOutput, bool bWarningForServiceCategory)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (DefinedService.ExpectedStatus == ActualService.CurrentStatus)
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                PerfData.iNumberOfCorrectServices++;
            }
            else if (ActualService.StartType == ServiceStartMode.Automatic.ToString() && ActualService.DelayedAutostart == true && bDelayedGracePeriod == true)
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not yet in the expected state '" + DefinedService.ExpectedStatus + "', it is within its grace period to start.");
                PerfData.iNumberOfPendingServices++;
            }
            else if (bWarningForServiceCategory == true)
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + DefinedService.ExpectedStatus + "'");
                returncode = (int)ServiceState.ServiceWarning;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + DefinedService.ExpectedStatus + "'");
                returncode = (int)ServiceState.ServiceCritical;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }

            return returncode;
        }

        private static int CheckCategories(int returncode, WinServiceActual ActualService, bool bDelayedGracePeriod, bool bIncludeCategoryInOutput, bool bWarningForSupportingService)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (ActualService.StartType == ServiceStartMode.Automatic.ToString() && ActualService.DelayedAutostart == true && bDelayedGracePeriod == true)
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "', it is currently '" + ActualService.CurrentStatus + "', it is within its grace period to start.");
                PerfData.iNumberOfPendingServices++;
            }
            else if (ActualService.CurrentStatus == ServiceControllerStatus.Running.ToString())
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is '" + ActualService.CurrentStatus.ToString() + "'");
                PerfData.iNumberOfCorrectServices++;
            }
            else if (bWarningForSupportingService == true)
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "', it is currently '" + ActualService.CurrentStatus + "'");
                returncode = (int)ServiceState.ServiceWarning;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "', it is currently '" + ActualService.CurrentStatus + "'");
                returncode = (int)ServiceState.ServiceCritical;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }
            return returncode;
        }

        private static int CheckRunningService(int returncode, WinServiceActual ActualService, bool bIncludeCategoryInOutput)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (ActualService.CurrentStatus == ServiceControllerStatus.Running.ToString())
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                PerfData.iNumberOfCorrectServices++;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "'");
                returncode = (int)ServiceState.ServiceCritical;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }
            return returncode;
        }

        private static int CheckStoppedService(int returncode, WinServiceActual ActualService, bool bIncludeCategoryInOutput)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (ActualService.CurrentStatus == ServiceControllerStatus.Stopped.ToString())
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                PerfData.iNumberOfCorrectServices++;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Stopped.ToString() + "'");
                returncode = (int)ServiceState.ServiceCritical;
                PerfData.iNumberOfWrongServices++;
                errorServices = true;
            }
            return returncode;
        }

        public static int GetUpTime()
        {
            PerformanceCounter pc = new PerformanceCounter("System", "System Up Time");
            pc.NextValue();
            return (int)pc.NextValue();
        }
    }
}