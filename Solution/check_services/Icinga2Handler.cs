using Icinga;
using System.Collections.Generic;

namespace check_services
{
    internal class Icinga2Handler
    {
        public static CheckResult Parse(CheckResult I2CheckResult, string[] args)
        {
            int returncode = 0;

            returncode = Handler.ParseArgs(returncode, args);
            // Return if something is not ok.
            if (returncode > (int)ServiceState.ServiceOK)
            {
                I2CheckResult.State = ServiceState.ServiceUnknown;
                return I2CheckResult;
            }

            // Inventory is blocked from running at the same time as other checks, thus it is run first if specified.
            if (Settings.bDoInventory == true && Settings.strInventoryFormat == "i2conf")
            {
                returncode = Inventory.OutputI2Conf();
                I2CheckResult.State = ServiceState.ServiceOK;
                return I2CheckResult;
            }
            else if (Settings.bDoInventory == true)
            {
                I2CheckResult.Output = "Unknown inventory, format: '" + Settings.strInventoryFormat + "', level: '" + Settings.strInventoryLevel + "'";
                I2CheckResult.State = ServiceState.ServiceUnknown;
                return I2CheckResult;
            }

            if (Settings.bDoCheckServices == true)
            {
                returncode = Checks.Services(returncode);
            }

            I2CheckResult.State = (ServiceState)returncode;

            I2CheckResult = HandleIcinga2Text(I2CheckResult, returncode);

            return I2CheckResult;
        }

        public static CheckResult HandleIcinga2Text(CheckResult I2CheckResult, int returncode)
        {
            // ORDER the output
            string output = "";
            output = Checks.outputServices;

            // Handle returncode and exit with proper messages.
            if (returncode == (int)ServiceState.ServiceOK)
            {
                output = "OK: " + output;
            }
            else if (returncode == (int)ServiceState.ServiceWarning)
            {
                output = "WARNING: " + output;
            }
            else if (returncode == (int)ServiceState.ServiceCritical)
            {
                output = "CRITICAL: " + output;
            }
            else if (returncode == (int)ServiceState.ServiceUnknown)
            {
                output = "UNKNOWN: " + output;
            }
            else
            {
                output = "UNHANDLED: " + output;
            }

            string outputLong = "";
            if (PerfData.iNumberOfServices > 1 && Settings.bDoHideLongOutput == false)
            {
                int x = 1;
                foreach (string outputS in Checks.listServiceOutput)
                {
                    if (x < Checks.listServiceOutput.Count)
                    {
                        outputLong = outputLong + outputS + "\n";
                    }
                    else
                    {
                        outputLong = outputLong + outputS;
                    }
                    x++;
                }
            }

            string perfdata = "";
            foreach (string outputP in Checks.listPerfData)
            {
                perfdata = perfdata + outputP;
            }

            if (Settings.bDoHideLongOutput)
            {
                I2CheckResult.Output = output;
            }
            else
            {
                if (PerfData.iNumberOfServices > 1)
                    output = output + outputLong;
                I2CheckResult.Output = output;
            }

            I2CheckResult.PerformanceData = perfdata;

            return I2CheckResult;
        }
    }
}