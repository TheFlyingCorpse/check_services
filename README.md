# check_services
Windows Service check plugin for Icinga2, Icinga, Centreon, Shinken, Naemon and other nagios like systems.

## General Requirements

* Windows OS with .NET Framework 3.5 SP1 or newer installed, this plugin comes in flavours for both 3.5 and 4.0+
* Basic understanding of Windows Services and what services you would like to monitor.
* This executable only uses the ServiceName, not the DisplayName. DisplayName is used only for output purposes. All arguments must use the ServiceName, example: "gpsvc"" and not ""Group Policy Client""
* This plugin can only check services locally on the same machine it runs from. Expects Icinga2 agent, NSCP or similar to execute it.

## Assumptions
This executable assumes the following:
* Services in the Supporting category will return a WARNING unless some other category has a higher severity Status. This can be changed with --warn-on-category to be a bogus category or for other categories if you want to.
* If you do not like the default ServiceDefinitions, the default CSV file which is similar to what is included in the plugin can be supplied with your alternative lists for the existing categories.
* Services which are set to Automatic that are not in the ServiceDefinitions already should be Running, unless otherwise specified using --excluded-services or --stopped-services (when checking ThirdParty). This can easily be overriden with flags for single- or multiple services with parameters shown below, or a new ServiceDefinition file.
* Services that are not set to Automatic start are ignored by default in all categories, use --check-all-starttypes to check these, or --inv-all-running to inventory them so only the ones running when the inventory is performed will be checked later (if the configuration is copied to Icinga2)
* If you run this plugin via Icinga2 and you have issues escaping, try to supply --split-by and separate multiple Services or Categories with a comma, ex "System,Role,Supporting".
* Some monitoring solutions really dont like long verbose output, so if your monitoring solution is one of these, try to use the switch --hide-long-output so it only prints the summary of the --check-service.

## Usage:

	Usage: check_services.exe [OPTIONS]
	Version: 0.11.6172.41529
	This plugin checks the State of one or more services on the local machine.
	You can filter for your services by using category or a combination of category and include/exclude filters.
	Use the same switch multiple times if you want to combine or exclude multiple categories or services

	Options:
	  -i, --inventory            Provide the inventory
	  -c, --check-service        Check the health status of the local services
		  --category=VALUE       Category to check or provide inventory from,
								   default is ThirdParty
		  --excluded-svc=VALUE   Exclude this service
		  --included-svc=VALUE   Explicitly include this service
		  --stopped-svc=VALUE    This service should be stopped
		  --running-svc=VALUE    Override CSV, this service should be running
		  --warn-on-category=VALUE
								 Warn on the specified category. Default is
								   Supporting
		  --allow-empty-result   Allow an empty result set. Useful when ThirdParty
								   category is specified.
		  --inv-format=VALUE     Inventory output format, default is readable,
								   available are csv,readable,i2conf
		  --inv-level=VALUE      Inventory level, normal or full
		  --inv-all-running      Inventory only the running services
		  --inv-hide-empty       Hide empty vars from inventory output.
		  --single-service       Specifies that only one Service is to be checked,
								   simplifies output of perfdata and optional
								   perfcounters
		  --expected-state=VALUE Set the expected state for the service, used
								   primarily with --single-service option
		  --split-by=VALUE       Alternative character to split input options VALUE
								   with. Default is ','
		  --check-all-starttypes Check all StartTypes against specified Category,
								   not only Automatic
		  --perfcounter          Extra performance counters, use with caution
		  --delayed-grace=VALUE  Set grace time for Automatic (Delayed) services
								   after boot-up before they must be started
		  --hide-long-output     Hide verbose output from the --check-service
								   command, simple output
		  --hide-category        Hide category from the normal output from the --
								   check-service command
		  --svc-in-sys-category=VALUE
								 Set category of specified service to System
		  --svc-in-man-category=VALUE
								 Set category of specified service to Managed
		  --svc-in-role-category=VALUE
								 Set category of specified service to Role
		  --svc-in-3rd-category=VALUE
								 Set category of specified service to ThirdParty
		  --svc-in-sup-category=VALUE
								 Set category of specified service to Supporting
		  --svc-in-ign-category=VALUE
								 Set category of specified service to Ignored
		  --category-file=VALUE  Path to a file which contains an alternative list
								   of Service to Category definitions
		  --file-format=VALUE    Specify format of the file path given in category-
								   file, default CSV
	  -v, --verbose              Verbose output
	  -d, --debug                Debug output
	  -h, --help                 Show this help

## QuickStart - Icinga2

### Recommended setup to start with

To get quickly started with this plugin, these are the steps needed to quickly get going:

#### Copy the neccesary files

To make available the plugin for icinga2:
* Copy the two files "check_services.exe" and "check_services.exe.config" to the sbin folder of icinga2
** Default path on a 32bit OS: C:\Program Files\ICINGA2\sbin
** Default path on a 64bit OS: C:\Program Files (x86)\ICINGA2\sbin

* Copy the configuration file "check_services.conf" into a place where it is easy for you to modify this using Notepad++ or your favourite text editor, like your Documents

** Note, this is only the command definition 
#### Inventory ThirdParty Services

Copy the output from this command to hosts.conf inside the brackets of the Host object (must be run from within icinga2's sbin folder, where you copied check_services.exe and check_services.exe.config)

	check_services.exe --inventory --category ThirdParty --inv-format i2conf

#### Monitoring inventoried Services

Uncomment the "Monitor Inventoried Services" example inside the "check_services.conf" you copied earlier

The file check_services.conf contains this example, just uncomment it in the file and save it.

#### Monitor System Category Services

Uncomment the "Monitor System Category Services" example inside the "check_services.conf" you copied earlier

#### Monitor Managed Category Services
Uncomment the "Monitor Managed Category Services" example inside the "check_services.conf" you copied earlier

#### Monitor Role Category Services
Uncomment the "Monitor Role Category Services" example inside the "check_services.conf" you copied earlier

#### Monitor Supporting Category Services
This is a "maybe", it might be a bit noisy, unless you also opt to exclude noisy services.

Uncomment the "Monitor Supporting Category Services" example inside the "check_services.conf" you copied earlier

#### Copying the configuration file to icinga

Copy the configuration file "check_services.conf" into your ICINGA2\etc\icinga2\conf.d folder from where you modified a copy of the default.

#### Applying the configuration change

Restart icinga2 agent on the local server/client to read the changed configuration.

Wait 1-2 minutes, then on the master, run "icinga2 node update-config" before restarting the icinga2 service to read the "discovered" services from the agent(s).

## Examples

### Monitoring
Monitor System Services

	check_services.exe --check-service --category System

Monitor System, Managed and Role Services

	check_services.exe --check-service --category System Role Managed

Monitor Managed Services, exclude service gpsvc (Group Policy Client)

	check_services.exe --check-service --category Managed --excluded-svc gpsvc

Monitor only specified services

	check_services.exe --check-service --included-svc SNMP --included-svc Spooler --included-svc "Apple Mobile Device Service"

Monitor all services in the category, even those not set to Automatic

	check_services.exe --check-service --category Supporting --check-all-starttypes

Monitor categories System, Managed and ThirdParty, hide categories from output when there are multiple categories specified as a parameter

	check_services.exe --check-service --category System --category Managed --category ThirdParty --hide-category
	
Monitor categories System, Managed and Supporting, warn if it is incorrect in Managed and Supporting

	check_services.exe --check-service --category System --category Managed --category Supporting --warn-on-category Managed --warn-on-category Supporting
	
Monitor with categories from CSV file:

	check_services.exe --check-service --category System Managed ThirdParty --category-file "C:\temp\ServiceDefinitions.csv" --file-format CSV

Monitor category ThirdParty with 180 second gracetime for Services that are of type Automatic (Delayed) to be started after bootup

	check_services.exe --check-service --category ThirdParty --delayed-grace 180

Monitor single service with expected state set

	check_services.exe --check-service --single-service --expected-state "Running" --included-svc "RpcSs"
	
Monitor categories System, Managed, Supporting and Role, set service CcmExec to category Supporting

	check_services.exe --check-service --category System --category Managed --category Supporting --category Role --svc-in-sup-category CcmExec
	


### Inventory
Inventory Services

	check_services.exe --inventory

Inventory Services, output as json, all details on services

	check_services.exe --inventory --inv-format json --inv-level full

Inventory Services, output as i2conf

	check_services.exe --inventory --inv-format i2conf

Inventory Services, output as CSV

	check_services.exe --inventory --inv-format csv

Inventory Services, only return Categories of type ThirdParty

	check_services.exe --inventory --category ThirdParty

Inventory Services, exclude service name of workfoldersvc, WwanSvc and Apple Mobile Device Service

	check_services.exe --inventory --excluded-svc workfoldersvc --excluded-svc WwanSvc --excluded-svc "Apple Mobile Device Service"

Inventory Services, show services all currently running.

	check_services.exe --inventory --inv-all-running

## Configuration

To make this plugin work with Icinga2, NSCP or others, it needs to be configured. 
Below are some configuration examples on how to configure this plugin with Icinga2 (agent) and NSCP/NSClient++.

### Icinga2 agent
ToDo

#### Command Definition

	object CheckCommand "check_services" {
		import "plugin-check-command"

		command = [ PluginDir + "/check_services.exe" ]

		arguments = {
			"--inventory" = {
				set_if = "$svc_inventory$"	
				description = "Provide the inventory"
			}
			"--check-service" = {
				set_if = "$svc_check$"
				description = "Check the health status of the local services"
			}
			"--category" = {
				value = "$svc_category$"
				description = "Category to check or provide inventory from, default is ThirdParty"
			}
			"--excluded-svc" = {
				value = "$svc_excluded_svc$"
				description = "Exclude this service"
			}
			"--included-svc" = {
				value = "$svc_included_svc$"
				description = "Explicitly include this service"
			}
			"--stopped-services" = {
				value = "$svc_stopped_svc$"
				description = "This service should be stopped"
			}
			"--running-services" = {
				value = "$svc_running_svc$"
				description = "Override CSV, this service should be running"
			}
			"--warn-on-category" = {
				value = "$svc_warn_on_category$"
				description = "Warn on the specified category. Default is Supporting."
			}
			"--inv-format" = {
				value = "$svc_inventory_format$"
				description = "Inventory output format, default is readable, available are csv,readable,i2conf"
			}
			"--inv-level" = {
				value = "$svc_inventory_level$"
				description = "Inventory level, normal or ful"
			}
			"--inv-all-running" = {
				set_if = "$svc_inv_all_running$"	
				description = "Inventory only the running services"
			}
			"--inv-hide-empty" = {
				set_if = "$svc_inv_hide_empty_vars$"	
				description = "Hide empty vars from inventory output."
			}
			"--single-service" = {
				set_if = "$svc_single_service$"	
				description = "Specifies that only one Service is to be checked, simplifies output of perfdata and optional perfcounters"
			}
			"--expected-state" = {
				set_if = "$svc_single_service$"	
				value = "$svc_expected_state$"
				description = "Set the expected state for the service, used primarily with --single-service option"
			}
			"--split-by" = {
				value = "$svc_split_by$"
				description = "Alternative character to split input options VALUE with. Default is ','."
			}
			"--check-all-starttypes" = {
				set_if = "$svc_check_all_starttypes$"	
				description = "Check all StartTypes against specified Category, not only 'Automatic'"
			}
			"--perfcounter" = {
				set_if = "$svc_perfcounter$"	
				description = "Extra performance counters, use with caution"
			}
			"--delayed-grace" = {
				value = "$svc_delayed_grace$"
				description = "Set grace time for Automatic (Delayed) services after boot-up before they must be started, default is 60 s"
			}
			"--hide-long-output" = {
				set_if = "$svc_hide_long_output$"	
				description = "Hide verbose output from the --check-service command, simple output"
			}
			"--hide-category" = {
				set_if = "$svc_hide_category$"	
				description = "Hide category from the normal output from the --check-service command"
			}
			"--svc-in-sys-category" = {
				value = "$svc_in_sys_category$"
				description = "Set category of specified service to System"
			}
			"--svc-in-man-category" = {
				value = "$svc_in_man_category$"
				description = "Set category of specified service to Managed"
			}
			"--svc-in-role-category" = {
				value = "$svc_in_role_category$"
				description = "Set category of specified service to Role"
			}
			"--svc-in-3rd-category" = {
				value = "$svc_in_3rd_category$"
				description = "Set category of specified service to ThirdParty"
			}
			"--svc-in-sup-category" = {
				value = "$svc_in_sup_category$"
				description = "Set category of specified service to Supporting"
			}
			"--svc-in-ign-category" = {
				value = "$svc_in_ign_category$"
				description = "Set category of specified service to Ignored"
			}
			"--file-format" = {
				set_if = "$svc_override_file$"
				value = "$svc_file_format$"
				description = "Argument to specify format of the file path given in category-file, assumes CSV if nothing else is specified"
			}
			"--category-file" = {
				set_if = "$svc_override_file$"
				value = "$svc_category_file$"
				description = "Path to a file which contains an alternative list of Service to Category definitions"
			}
			"--verbose" = {
				set_if = "$svc_verbose$"
				description = "Switch to use when trying to figure out why a service is not included, excluded or similarly when the returned output is not as expected"
			}
			"--allow-empty-result" = {
				set_if = "$svc_allow_empty_result$"	
				description = "Allow an empty result set. Useful when ThirdParty category is specified"
			}
			
		}
		//vars.svc_inventory = false
		//vars.svc_check = false
		//vars.svc_excluded_svc = ""
		//vars.svc_included_svc = ""
		//vars.svc_stopped_svc = ""
		//vars.svc_running_svc = ""
		//vars.svc_warn_on_category = "Supporting"
		//vars.svc_category = "ThirdParty"
		//vars.svc_inventory_level = "normal"
		//vars.svc_inventory_format = "i2conf"
		//vars.svc_inv_all_running = false
		//vars.svc_inv_hide_empty_vars = false
		//vars.svc_single_service = false
		//vars.svc_expected_state = "Running"
		//vars.svc_split_by = ","
		//vars.svc_check_all_starttypes = false
		//vars.svc_perfcounter = false
		//vars.svc_delayed_grace = "60"
		//vars.svc_hide_long_output = false
		//vars.svc_hide_category = false
		//vars.svc_in_sys_category = ""
		//vars.svc_in_man_category = ""
		//vars.svc_in_role_category = ""
		//vars.svc_in_sup_category = ""
		//vars.svc_in_3rd_category = ""
		//vars.svc_in_ign_category = ""
		//vars.svc_override_file = false
		//vars.svc_file_format = "CSV"
		//vars.svc_category_file = ""
		//vars.svc_verbose = false	
		//vars.svc_allow_empty_result = false
	}

#### Apply Rules

Monitor all services that are on the host in the inventory

	apply Service "WinSvc " for (ServiceName => config in host.vars.inv.windows.service) {
	  import "generic-service"

	  check_command = "check_services"

	  vars += config
	  vars.svc_check = true
	  vars.svc_included_svc = [ vars.ServiceName ]
	  vars.svc_expected_state = vars.CurrentStatus
	  vars.svc_single_service = true
	}

Monitor all services in the System category

	apply Service "WinSvcs - System" {
		import "generic-service"
		
		check_command = "check_services"
		
		vars.svc_check = true
		vars.svc_categories = [ "System" ]
		assign where host.name == NodeName
	}

Monitor all services in the ThirdParty category, excluding gupdate

	apply Service "WinSvcs - ThirdParty" {
		import "generic-service"
		
		check_command = "check_services"
		
		vars.svc_check = true
		vars.svc_categories = [ "ThirdParty" ]
		vars.svc_excluded_services = [ "gupdate" ]
		assign where host.name == NodeName
	}

Monitor all services in the System, Managed, Role and Supporting category by specifying the predefined group category "Basic" and hide long output

	apply Service "WinSvcs - Basic" {
		import "generic-service"
		
		check_command = "check_services"
		
		vars.svc_check = true
		vars.svc_categories = [ "Basic" ]
		vars.svc_hide_long_output = true
		assign where host.name == NodeName
	}

Monitor all services in the System, Managed and Role categories, hide long output. 

	apply Service "WinSvcs - Core" {
		import "generic-service"
		
		check_command = "check_services"
		
		vars.svc_check = true
		vars.svc_category = [ "System", "Managed" ,"Role" ]
		vars.svc_hide_long_output = true
		assign where host.name == NodeName
	}
	
### NSCP / NSClient++
ToDo

Add in nsclient.ini

Test via check_nrpe or similar to verify.

	
## ServiceDefinitions:

The Default ServiceDefinitions in the CSV format below are included in the plugin, so there is no need to specify the path for a category-file if you do not want to provide your own definitions.

### Default ServiceDefinitions, CSV format
ServiceName | DisplayName | ServiceCategory | StartType | ExpectedStatus
------------|-------------|-----------------|-----------|----------------
BITS | Background Intelligent Transfer Service | Managed | Automatic | Running
COMSysApp | COM+ System Application | Managed | Automatic | Running
Dnscache | DNS Client | Managed | Automatic | Running
DPS | Diagnostic Policy Service | Managed | Automatic | Running
EventLog | Windows Event Log | Managed | Automatic | Running
EventSystem | COM+ Event System | Managed | Automatic | Running
iphlpsvc | IP Helper | Managed | Automatic | Running
LanmanServer | Server | Managed | Automatic | Running
LanmanWorkstation | Workstation | Managed | Automatic | Running
MpsSvc | Windows Firewall | Managed | Automatic | Running
Netlogon | Netlogon | Managed | Automatic | Running
pla | Performance Logs & Alerts | Managed | Automatic | Running
ProfSvc | User Profile Service | Managed | Automatic | Running
RpcEptMapper | RPC Endpoint Mapper | Managed | Automatic | Running
RpcSs | Remote Procedure Call (RPC) | Managed | Automatic | Running
UALSVC | User Access Logging Service | Managed | Automatic | Running
vmicheartbeat | Hyper-V Heartbeat Service | Managed | Automatic | Running
vmicshutdown | Hyper-V Guest Shutdown Service | Managed | Automatic | Running
vmictimesync | Hyper-V Time Synchronization Service | Managed | Automatic | Running
VMTools | VMware Tools | Managed | Automatic | Running
Winmgmt | Windows Management Instrumentation | Managed | Automatic | Running
WinRM | Windows Remote Management (WS-Management) | Managed | Automatic | Running
ALG | Application Layer Gateway Service | Ignored | Automatic | Running
AppReadiness | App Readiness | Ignored | Automatic | Running
AppXSvc | AppX Deployment Service (AppXSVC) | Ignored | Automatic | Running
dot3svc | Wired AutoConfig | Ignored | Automatic | Running
DsmSvc | Device Setup Manager | Ignored | Automatic | Running
EFS | Encrypting File System (EFS) | Ignored | Automatic | Running
hidserv | Human Interface Device Service | Ignored | Automatic | Running
lltdsvc | Link-Layer Topology Discovery Mapper | Ignored | Automatic | Running
msiserver | Windows Installer | Ignored | Automatic | Running
SNMPTRAP | SNMP Trap | Ignored | Automatic | Running
SysMain | Superfetch | Ignored | Automatic | Running
TabletInputService | Touch Keyboard and Handwriting Panel Service | Ignored | Automatic | Running
TrustedInstaller | Windows Modules Installer | Ignored | Automatic | Running
upnphost | UPnP Device Host | Ignored | Automatic | Running
vmicvss | Hyper-V Volume Shadow Copy Requestor | Ignored | Automatic | Running
vmvss | VMware Snapshot Provider | Ignored | Automatic | Running
VSS | Volume Shadow Copy | Ignored | Automatic | Running
Wecsvc | Windows Event Collector | Ignored | Automatic | Running
adfssrv | Active Directory Federation Services | Role | Automatic | Running
ADWS | Active Directory Web Services | Role | Automatic | Running
BITSCompactServer | BITS Compact Server | Role | Automatic | Running
c2wts | Claims to Windows Token Service | Role | Automatic | Running
CertSvc | Active Directory Certificate Services | Role | Automatic | Running
ClusSvc | Cluster Service | Role | Automatic | Running
ddpsvc | Data Deduplication Service | Role | Automatic | Running
ddpvssvc | Data Deduplication Volume Shadow Copy Service | Role | Automatic | Running
Dfs | DFS Namespace | Role | Automatic | Running
DFSR | DFS Replication | Role | Automatic | Running
DNS | DNS Server | Role | Automatic | Running
drs | Device Registration Service | Role | Automatic | Running
Eaphost | Extensible Authentication Protocol | Role | Automatic | Running
Fax | Fax | Role | Automatic | Running
fdPHost | Function Discovery Provider Host | Role | Automatic | Running
FDResPub | Function Discovery Resource Publication | Role | Automatic | Running
fssagent | Microsoft File Server Shadow Copy Agent Service | Role | Automatic | Running
hkmsvc | Health Key and Certificate Management | Role | Automatic | Running
IAS | Network Policy Server | Role | Automatic | Running
IISADMIN | IIS Admin Service | Role | Automatic | Running
IKEEXT | IKE and AuthIP IPsec Keying Modules | Role | Automatic | Running
IsmServ | Intersite Messaging | Role | Automatic | Running
Kdc | Kerberos Key Distribution Center | Role | Automatic | Running
KdsSvc | Microsoft Key Distribution Service | Role | Automatic | Running
KeyIso | CNG Key Isolation | Role | Automatic | Running
KPSSVC | KDC Proxy Server service (KPS) | Role | Automatic | Running
KtmRm | KtmRm for Distributed Transaction Coordinator | Role | Automatic | Running
LPDSVC | LPD Service | Role | Automatic | Running
MMCSS | Multimedia Class Scheduler | Role | Automatic | Running
MSDTC | Distributed Transaction Coordinator | Role | Automatic | Running
MSiSCSI | Microsoft iSCSI Initiator Service | Role | Automatic | Running
MSiSNS | Microsoft iSNS Server | Role | Automatic | Running
MSMQ | Message Queuing | Role | Automatic | Running
MSMQTriggers | Message Queuing Triggers | Role | Automatic | Running
MSSQL$MICROSOFT##WID | Windows Internal Database | Role | Automatic | Running
MSStrgSvc | Windows Standards-Based Storage Management | Role | Automatic | Running
NetMsmqActivator | Net.Msmq Listener Adapter | Role | Automatic | Running
NetPipeActivator | Net.Pipe Listener Adapter | Role | Automatic | Running
NetTcpActivator | Net.Tcp Listener Adapter | Role | Automatic | Running
NetTcpPortSharing | Net.Tcp Port Sharing Service | Role | Automatic | Running
NfsService | Server for NFS | Role | Automatic | Running
NtFrs | File Replication | Role | Automatic | Running
OcspSvc | Online Responder Service | Role | Automatic | Running
PeerDistSvc | BranchCache | Role | Automatic | Running
PNRPAutoReg | PNRP Machine Name Publication Service | Role | Automatic | Running
PNRPsvc | Peer Name Resolution Protocol | Role | Automatic | Running
PrintNotify | Printer Extensions and Notifications | Role | Automatic | Running
RaMgmtSvc | Remote Access Management service | Role | Automatic | Running
RasAuto | Remote Access Auto Connection Manager | Role | Automatic | Running
RasMan | Remote Access Connection Manager | Role | Automatic | Running
RDMS | Remote Desktop Management | Role | Automatic | Running
RemoteAccess | Routing and Remote Access | Role | Automatic | Running
RPCHTTPLBS | RPC/HTTP Load Balancing Service | Role | Automatic | Running
RpcLocator | Remote Procedure Call (RPC) Locator | Role | Automatic | Running
rqs | Remote Access Quarantine Agent | Role | Automatic | Running
SessionEnv | Remote Desktop Configuration | Role | Automatic | Running
simptcp | Simple TCP/IP Services | Role | Automatic | Running
SmbHash | SMB Hash Generation Service | Role | Automatic | Running
SmbWitness | SMB Witness | Role | Automatic | Running
smphost | Microsoft Storage Spaces SMP | Role | Automatic | Running
SMTPSVC | Simple Mail Transfer Protocol (SMTP) | Role | Automatic | Running
SNMP | SNMP Service | Role | Automatic | Running
SrmReports | File Server Storage Reports Manager | Role | Automatic | Running
SrmSvc | File Server Resource Manager | Role | Automatic | Running
SstpSvc | Secure Socket Tunneling Protocol Service | Role | Automatic | Running
stisvc | Windows Image Acquisition (WIA) | Role | Automatic | Running
StorSvc | Storage Service | Role | Automatic | Running
svsvc | Spot Verifier | Role | Automatic | Running
SyncShareSvc | Windows Sync Share | Role | Automatic | Running
SyncShareTTSvc | Sync Share Token Translation Service | Role | Automatic | Running
TapiSrv | Telephony | Role | Automatic | Running
TermService | Remote Desktop Services | Role | Automatic | Running
TermServLicensing | Remote Desktop Licensing | Role | Automatic | Running
THREADORDER | Thread Ordering Server | Role | Automatic | Running
TieringEngineService | Storage Tiers Management | Role | Automatic | Running
TimeBroker | Time Broker | Role | Automatic | Running
TlntSvr | Telnet | Role | Automatic | Running
TPAutoConnSvc | TP AutoConnect Service | Role | Automatic | Running
TPVCGateway | TP VC Gateway Service | Role | Automatic | Running
TrkWks | Distributed Link Tracking Client | Role | Automatic | Running
TScPubRPC | RemoteApp and Desktop Connection Management | Role | Automatic | Running
TSGateway | Remote Desktop Gateway | Role | Automatic | Running
Tssdis | Remote Desktop Connection Broker | Role | Automatic | Running
UI0Detect | Interactive Services Detection | Role | Automatic | Running
UmRdpService | Remote Desktop Services UserMode Port Redirector | Role | Automatic | Running
w3logsvc | W3C Logging Service | Role | Automatic | Running
W3SVC | World Wide Web Publishing Service | Role | Automatic | Running
WAS | Windows Process Activation Service | Role | Automatic | Running
wbengine | Block Level Backup Engine Service | Role | Automatic | Running
WbioSrvc | Windows Biometric Service | Role | Automatic | Running
WDSServer | Windows Deployment Services Server | Role | Automatic | Running
WEPHOSTSVC | Windows Encryption Provider Host Service | Role | Automatic | Running
wercplsupport | Problem Reports and Solutions Control Panel Support | Role | Automatic | Running
WerSvc | Windows Error Reporting Service | Role | Automatic | Running
WFFSvc | Windows Feedback Forwarder Service | Role | Automatic | Running
WiaRpc | Still Image Acquisition Events | Role | Automatic | Running
WIDWriter | Windows Internal Database VSS Writer | Role | Automatic | Running
WinTarget | Microsoft iSCSI Software Target | Role | Automatic | Running
WSusCertServer | WSUS Certificate Server | Role | Automatic | Running
WsusService | WSUS Service | Role | Automatic | Running
AeLookupSvc | Application Experience | Supporting | Automatic | Running
AppHostSvc | Application Host Helper Service | Supporting | Automatic | Running
AppIDSvc | Application Identity | Supporting | Automatic | Running
Appinfo | Application Information | Supporting | Automatic | Running
AppMgmt | Application Management | Supporting | Automatic | Running
aspnet_state | ASP.NET State Service | Supporting | Automatic | Running
AudioEndpointBuilder | Windows Audio Endpoint Builder | Supporting | Automatic | Running
Audiosrv | Windows Audio | Supporting | Automatic | Running
AxInstSV | ActiveX Installer (AxInstSV) | Supporting | Automatic | Running
BDESVC | BitLocker Drive Encryption Service | Supporting | Automatic | Running
Browser | Computer Browser | Supporting | Automatic | Running
CertPropSvc | Certificate Propagation | Supporting | Automatic | Running
CryptSvc | Cryptographic Services | Supporting | Automatic | Running
CscService | Offline Files | Supporting | Automatic | Running
defragsvc | Optimize drives | Supporting | Automatic | Running
DeviceAssociationService | Device Association Service | Supporting | Automatic | Running
DeviceInstall | Device Install Service | Supporting | Automatic | Running
Dhcp | DHCP Client | Supporting | Automatic | Running
DiagTrack | Diagnostics Tracking Service | Supporting | Automatic | Running
FontCache | Windows Font Cache Service | Supporting | Automatic | Running
gpsvc | Group Policy Client | Supporting | Automatic | Running
IEEtwCollectorService | Internet Explorer ETW Collector Service | Supporting | Automatic | Running
lmhosts | TCP/IP NetBIOS Helper | Supporting | Automatic | Running
napagent | Network Access Protection Agent | Supporting | Automatic | Running
NcaSvc | Network Connectivity Assistant | Supporting | Automatic | Running
NcbService | Network Connection Broker | Supporting | Automatic | Running
Netman | Network Connections | Supporting | Automatic | Running
netprofm | Network List Service | Supporting | Automatic | Running
NlaSvc | Network Location Awareness | Supporting | Automatic | Running
nsi | Network Store Interface Service | Supporting | Automatic | Running
p2pimsvc | Peer Networking Identity Manager | Supporting | Automatic | Running
PerfHost | Performance Counter DLL Host | Supporting | Automatic | Running
PlugPlay | Plug and Play | Supporting | Automatic | Running
PolicyAgent | IPsec Policy Agent | Supporting | Automatic | Running
QWAVE | Quality Windows Audio Video Experience | Supporting | Automatic | Running
RemoteRegistry | Remote Registry | Supporting | Automatic | Running
RSoPProv | Resultant Set of Policy Provider | Supporting | Automatic | Running
sacsvr | Special Administration Console Helper | Supporting | Automatic | Running
SCardSvr | Smart Card | Supporting | Automatic | Running
ScDeviceEnum | Smart Card Device Enumeration Service | Supporting | Automatic | Running
SCPolicySvc | Smart Card Removal Policy | Supporting | Automatic | Running
seclogon | Secondary Logon | Supporting | Automatic | Running
SENS | System Event Notification Service | Supporting | Automatic | Running
SharedAccess | Internet Connection Sharing (ICS) | Supporting | Automatic | Running
ShellHWDetection | Shell Hardware Detection | Supporting | Automatic | Running
Spooler | Print Spooler | Supporting | Automatic | Running
sppsvc | Software Protection | Supporting | Automatic | Running
SSDPSRV | SSDP Discovery | Supporting | Automatic | Running
swprv | Microsoft Software Shadow Copy Provider | Supporting | Automatic | Running
SystemEventsBroker | System Events Broker | Supporting | Automatic | Running
Themes | Themes | Supporting | Automatic | Running
VaultSvc | Credential Manager | Supporting | Automatic | Running
vds | Virtual Disk | Supporting | Automatic | Running
vmicguestinterface | Hyper-V Guest Service Interface | Supporting | Automatic | Running
vmickvpexchange | Hyper-V Data Exchange Service | Supporting | Automatic | Running
vmicrdv | Hyper-V Remote Desktop Virtualization Service | Supporting | Automatic | Running
W32Time | Windows Time | Supporting | Automatic | Running
Wcmsvc | Windows Connection Manager | Supporting | Automatic | Running
WcsPlugInService | Windows Color System | Supporting | Automatic | Running
WdiServiceHost | Diagnostic Service Host | Supporting | Automatic | Running
WdiSystemHost | Diagnostic System Host | Supporting | Automatic | Running
WebClient | WebClient | Supporting | Automatic | Running
WinHttpAutoProxySvc | WinHTTP Web Proxy Auto-Discovery Service | Supporting | Automatic | Running
wlidsvc | Microsoft Account Sign-in Assistant | Supporting | Automatic | Running
wmiApSrv | WMI Performance Adapter | Supporting | Automatic | Running
WPDBusEnum | Portable Device Enumerator Service | Supporting | Automatic | Running
WSearch | Windows Search | Supporting | Automatic | Running
WSService | Windows Store Service (WSService) | Supporting | Automatic | Running
wuauserv | Windows Update | Supporting | Automatic | Running
wudfsvc | Windows Driver Foundation - User-mode Driver Framework | Supporting | Automatic | Running
BFE | Base Filtering Engine | System | Automatic | Running
BrokerInfrastructure | Background Tasks Infrastructure Service | System | Automatic | Running
DcomLaunch | DCOM Server Process Launcher | System | Automatic | Running
LSM | Local Session Manager | System | Automatic | Running
Power | Power | System | Automatic | Running
SamSs | Security Accounts Manager | System | Automatic | Running
Schedule | Task Scheduler | System | Automatic | Running
WinDefend | Windows Defender Service | System | Automatic | Running