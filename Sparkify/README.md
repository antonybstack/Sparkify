# Windows Service Deployment
## New Service
``` shell
$serviceName = "Sparkify"
$deploymentPath = "C:\dev\Release"
$serviceUser = "DESKTOP-4IQSTOS\antbly"

$acl = Get-Acl $deploymentPath
$aclRuleArgs = $serviceUser, "Read,Write,ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule($aclRuleArgs)
$acl.SetAccessRule($accessRule)
$acl | Set-Acl $deploymentPath

New-Service -Name $serviceName -BinaryPathName $deploymentPath"\Sparkify.exe" -Credential $serviceUser -Description "Web API" -DisplayName $serviceName -StartupType Automatic
Start-Service -Name $serviceName

# Sets the first and second failure to restart the service
sc failure $serviceName reset= 60 actions= restart/5000/restart/5000/""/0

# Set Priority
$process = Get-Process -Name $serviceName
$process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High

# Check the service status to ensure it's running
Get-Service -Name $serviceName | Select-Object DisplayName, Status

```

## Publish
``` shell
# Define service name and paths
$serviceName = "Sparkify"
$deploymentPath = "C:\dev\Release"
$backupPath = "C:\dev\Backups"
$newDeploymentFilesPath = "C:\dev\Publish"

# Backup existing files
$newBackupPath = "$backupPath\_backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -Path $newBackupPath -ItemType Directory
Copy-Item -Path "$deploymentPath\*" -Destination $backupPath -Recurse

dotnet publish -r win-x64 --self-contained true -c Release -o $newDeploymentFilesPath

Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue

# Copy new files to deployment directory
Copy-Item -Path "$newDeploymentFilesPath\*" -Destination $deploymentPath -Recurse

Start-Service -Name $serviceName

# Sets the first and second failure to restart the service
sc failure $serviceName reset= 60 actions= restart/5000/restart/5000/""/0

# Set Priority
$process = Get-Process -Name $serviceName
$process.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High

# Check the service status to ensure it's running
Get-Service -Name $serviceName | Select-Object DisplayName, Status

```

