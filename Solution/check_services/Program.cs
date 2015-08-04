using Icinga;
using System.Collections.Generic;

namespace check_services
{
    public class Program
    {
        public static List<string> listServicePerfCounters = new List<string>();

        private static int Main(string[] args)
        {
            int returncode = 0;

            returncode = ConsoleHandler.ParseArgs(returncode, args);

            return (int)returncode;
        }

        public static CheckResult Check(string[] args)
        {
            CheckResult I2CheckResult = new CheckResult();

            I2CheckResult = Icinga2Handler.ParseArgs(I2CheckResult, args);

            return I2CheckResult;
        }
    }
}