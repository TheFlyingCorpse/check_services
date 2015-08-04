using System;

namespace check_services
{ 
    class WinServiceDefined
    {
        public String ServiceName { get; private set; }
        public String DisplayNamea { get; private set; }
        public String StartType { get; private set; }
        public String ExpectedStatus { get; private set; }
        public String ServiceCategory { get; private set; }

        public WinServiceDefined(String serviceName, String displayName, String serviceCategory, String startType, String expectedStatus)
        {
            ServiceName = serviceName;
            DisplayNamea = displayName;
            StartType = startType;
            ExpectedStatus = expectedStatus;
            ServiceCategory = serviceCategory;
        }
    }
}