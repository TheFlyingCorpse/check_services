using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace check_services
{
    class WinServiceActual
    {
        public String ServiceName { get; private set; }
        public String DisplayName { get; private set; }
        public String StartType { get; private set; }
        public Boolean DelayedAutostart { get; private set; }
        public Boolean WOW64 { get; private set; }
        public String ObjectName { get; private set; }
        public String CurrentStatus { get; private set; }
        public String ServiceCategory { get; private set; }
        public String FileOwner { get; private set; }
        public String ImagePath { get; private set; }
        public String ResolvedImagePath { get; private set; }
        public Array DependentServices { get; private set; }
        public Array ServicesDependingOn { get; private set; }

        public WinServiceActual(String serviceName, String displayName, String startType, Boolean delayedAutostart, Boolean WOW64, String objectName, String currentStatus, string serviceCategory, string fileOwner, string imagePath, string resolvedImagePath, Array dependentServices, Array servicesDependingOn)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            StartType = startType;
            DelayedAutostart = delayedAutostart;
            ObjectName = objectName;
            CurrentStatus = currentStatus;
            ServiceCategory = serviceCategory;
            FileOwner = fileOwner;
            ImagePath = imagePath;
            ResolvedImagePath = resolvedImagePath;
            DependentServices = dependentServices;
            ServicesDependingOn = servicesDependingOn;
        }
    }
}
