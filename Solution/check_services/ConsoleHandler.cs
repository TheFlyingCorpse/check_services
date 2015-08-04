using Icinga;
using System;
using System.Collections.Generic;

namespace check_services
{
    internal class ConsoleHandler
    {
        public static int Parse(int returncode, string[] args)
        {
            returncode = Handler.ParseArgs(returncode, args);

            // Return if something is not ok.
            if (returncode > (int)ServiceState.ServiceOK)
                return returncode;

            // Inventory is blocked from running at the same time as other checks, thus it is run first if specified.
            if (Settings.bDoInventory == true && Settings.strInventoryFormat == "readable")
            {
                returncode = Inventory.OutputReadable();
                return (int)ServiceState.ServiceUnknown;
            }
            else if (Settings.bDoInventory == true && Settings.strInventoryFormat == "csv")
            {
                returncode = Inventory.OutputCSV();
                return (int)ServiceState.ServiceUnknown;
            }
            else if (Settings.bDoInventory == true && Settings.strInventoryFormat == "i2conf")
            {
                returncode = Inventory.OutputI2Conf();
                return (int)ServiceState.ServiceUnknown;
            }
            else if (Settings.bDoInventory == true && Settings.strInventoryFormat == "json")
            {
                returncode = Inventory.OutputJSON();
                return (int)ServiceState.ServiceUnknown;
            }
            else if (Settings.bDoInventory == true)
            {
                Console.WriteLine("Unknown inventory, format: '" + Settings.strInventoryFormat + "', level: '" + Settings.strInventoryLevel + "'");
                return (int)ServiceState.ServiceUnknown;
            }

            if (Settings.bDoCheckServices == true)
            {
                returncode = Checks.Services(returncode);
            }

            returncode = HandleConsoleText(returncode);
            return returncode;
        }

        public static int HandleConsoleText(int returncode)
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

            Console.Write(output);
            if (PerfData.iNumberOfServices > 1 && Settings.bDoHideLongOutput == false)
                Console.Write("\n" + outputLong);

            Console.Write(" | " + perfdata);

            return returncode;
        }
    }
}