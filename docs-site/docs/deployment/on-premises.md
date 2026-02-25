# On-Premises Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** On-premises servers (Windows/Linux)
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** runtime installed on target servers
- IIS (Windows) or systemd (Linux) configured for hosting
- Familiarity with [ASP.NET Core deployment](./aspnet-core.md) and [worker services](./worker-services.md)

## Overview

Deploy Excalibur applications to on-premises infrastructure using IIS, Windows Services, or systemd for full control over hosting environment.

**Use on-premises deployment when:**
- Regulatory requirements mandate data residency
- Existing infrastructure investment
- Air-gapped or offline environments
- Full control over hardware and network configuration

---

## IIS Deployment (Windows Server)

### Prerequisites

- Windows Server 2019 or later
- IIS 10+ with ASP.NET Core Hosting Bundle
- .NET 9 Runtime or SDK

### Install ASP.NET Core Hosting Bundle

```powershell
# Download and install
Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
.\dotnet-install.ps1 -Runtime aspnetcore -Version 9.0

# Verify installation
dotnet --list-runtimes
```

### Publish Application

```powershell
# Publish for IIS deployment
dotnet publish -c Release -o C:\inetpub\wwwroot\your-app

# Or with self-contained runtime
dotnet publish -c Release -r win-x64 --self-contained -o C:\inetpub\wwwroot\your-app
```

### Create IIS Application Pool

```powershell
# Import IIS module
Import-Module WebAdministration

# Create application pool
New-WebAppPool -Name "YourAppPool"

# Configure application pool
Set-ItemProperty -Path "IIS:\AppPools\YourAppPool" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty -Path "IIS:\AppPools\YourAppPool" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Set-ItemProperty -Path "IIS:\AppPools\YourAppPool" -Name "enable32BitAppOnWin64" -Value $false

# Set recycling schedule (daily at 2 AM)
Clear-ItemProperty -Path "IIS:\AppPools\YourAppPool" -Name "recycling.periodicRestart.schedule"
New-ItemProperty -Path "IIS:\AppPools\YourAppPool" -Name "recycling.periodicRestart.schedule" -Value @{value="02:00:00"}
```

### Create IIS Website

```powershell
# Create website
New-Website -Name "YourApp" `
  -PhysicalPath "C:\inetpub\wwwroot\your-app" `
  -ApplicationPool "YourAppPool" `
  -Port 80

# Configure HTTPS (recommended)
$cert = New-SelfSignedCertificate -DnsName "yourdomain.com" -CertStoreLocation "Cert:\LocalMachine\My"
New-WebBinding -Name "YourApp" -Protocol "https" -Port 443
$binding = Get-WebBinding -Name "YourApp" -Protocol "https"
$binding.AddSslCertificate($cert.Thumbprint, "My")

# Configure authentication
Set-WebConfigurationProperty -Filter "/system.webServer/security/authentication/anonymousAuthentication" `
  -Name "enabled" -Value $true -PSPath "IIS:\Sites\YourApp"

# Start website
Start-Website -Name "YourApp"
```

### web.config (Automatic)

ASP.NET Core generates web.config automatically during publish:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\YourApp.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

### Application Configuration

```json
// appsettings.Production.json
{
  "ConnectionStrings": {
    "Default": "Server=.\\SQLEXPRESS;Database=AppDb;Integrated Security=true;TrustServerCertificate=true;"
  },
  "Dispatch": {
    "Outbox": {
      "ProcessorInterval": "00:00:30",
      "BatchSize": 100
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Troubleshooting IIS

```powershell
# Check IIS logs
Get-Content "C:\inetpub\wwwroot\your-app\logs\stdout*.log" -Tail 50

# Check Event Viewer
Get-EventLog -LogName Application -Source "IIS AspNetCore Module V2" -Newest 20

# Restart application pool
Restart-WebAppPool -Name "YourAppPool"

# Test website
Test-NetConnection -ComputerName localhost -Port 80
```

---

## Windows Service Deployment

### Create Windows Service Project

```csharp
// Program.cs
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

// Configure as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Excalibur Dispatch Service";
});

// Add Dispatch
builder.Services.AddDispatch();
builder.Services.AddSqlServerOutboxStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Default");
});

// Add background service
builder.Services.AddOutboxHostedService();

var host = builder.Build();
await host.RunAsync();
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="9.0.0" />
    <PackageReference Include="Excalibur.Dispatch" Version="1.0.0" />
    <PackageReference Include="Excalibur.Outbox.SqlServer" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### Install Windows Service

```powershell
# Publish as self-contained
dotnet publish -c Release -r win-x64 --self-contained -o C:\Services\YourApp

# Create service using sc.exe
sc.exe create "YourAppService" `
  binPath= "C:\Services\YourApp\YourApp.exe" `
  start= auto `
  DisplayName= "Your Application Service"

# Or use New-Service (PowerShell 6+)
New-Service -Name "YourAppService" `
  -BinaryPathName "C:\Services\YourApp\YourApp.exe" `
  -DisplayName "Your Application Service" `
  -StartupType Automatic `
  -Description "Excalibur background service"

# Configure service recovery
sc.exe failure "YourAppService" reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Start service
Start-Service -Name "YourAppService"

# Check status
Get-Service -Name "YourAppService"
```

### Service Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "Default": "Server=.\\SQLEXPRESS;Database=AppDb;Integrated Security=true;TrustServerCertificate=true;"
  },
  "Dispatch": {
    "Outbox": {
      "ProcessorInterval": "00:00:30",
      "BatchSize": 100
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "EventLog": {
      "SourceName": "YourApp",
      "LogName": "Application"
    }
  }
}
```

### Troubleshooting Windows Service

```powershell
# Check service status
Get-Service -Name "YourAppService" | Format-List

# View Event Viewer logs
Get-EventLog -LogName Application -Source "YourApp" -Newest 20

# Stop and restart service
Stop-Service -Name "YourAppService"
Start-Service -Name "YourAppService"

# Uninstall service
Stop-Service -Name "YourAppService"
sc.exe delete "YourAppService"
```

---

## systemd Deployment (Linux)

### Publish for Linux

```bash
# Publish for Linux x64
dotnet publish -c Release -r linux-x64 --self-contained -o /opt/your-app

# Set permissions
sudo chown -R www-data:www-data /opt/your-app
sudo chmod +x /opt/your-app/YourApp
```

### Create systemd Service Unit

```bash
# Create service file
sudo nano /etc/systemd/system/your-app.service
```

**Service file content:**

```ini
[Unit]
Description=Your Application Service
After=network.target

[Service]
Type=notify
User=www-data
Group=www-data
WorkingDirectory=/opt/your-app
ExecStart=/opt/your-app/YourApp
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=your-app
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/opt/your-app/logs

[Install]
WantedBy=multi-user.target
```

### Manage Service

```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service (start on boot)
sudo systemctl enable your-app.service

# Start service
sudo systemctl start your-app.service

# Check status
sudo systemctl status your-app.service

# View logs
sudo journalctl -u your-app.service -f

# Restart service
sudo systemctl restart your-app.service

# Stop service
sudo systemctl stop your-app.service
```

### Reverse Proxy with Nginx

```bash
# Install Nginx
sudo apt update
sudo apt install nginx

# Configure Nginx
sudo nano /etc/nginx/sites-available/your-app
```

**Nginx configuration:**

```nginx
server {
    listen 80;
    server_name yourdomain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Enable site:**

```bash
# Enable site
sudo ln -s /etc/nginx/sites-available/your-app /etc/nginx/sites-enabled/

# Test configuration
sudo nginx -t

# Reload Nginx
sudo systemctl reload nginx
```

### SSL/TLS with Let's Encrypt

```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx

# Obtain certificate
sudo certbot --nginx -d yourdomain.com

# Auto-renewal (already configured by Certbot)
sudo certbot renew --dry-run
```

---

## SQL Server Configuration

### Windows Integrated Authentication

```json
{
  "ConnectionStrings": {
    "Default": "Server=.\\SQLEXPRESS;Database=AppDb;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

**Grant permissions:**

```sql
-- Create login for IIS app pool
CREATE LOGIN [IIS APPPOOL\YourAppPool] FROM WINDOWS;

-- Create user in database
USE AppDb;
CREATE USER [IIS APPPOOL\YourAppPool] FROM LOGIN [IIS APPPOOL\YourAppPool];

-- Grant permissions
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\YourAppPool];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\YourAppPool];
ALTER ROLE db_ddladmin ADD MEMBER [IIS APPPOOL\YourAppPool];  -- For migrations
```

### SQL Server Authentication (Linux)

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=AppDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;"
  }
}
```

**Secure password storage:**

```bash
# Use environment variable
export ConnectionStrings__Default="Server=localhost;Database=AppDb;User Id=sa;Password=...;TrustServerCertificate=true;"

# Or use User Secrets (development)
dotnet user-secrets set "ConnectionStrings:Default" "Server=..."
```

---

## Load Balancing and High Availability

### Windows Network Load Balancing (NLB)

```powershell
# Install NLB feature
Install-WindowsFeature -Name NLB -IncludeManagementTools

# Create NLB cluster
New-NlbCluster -InterfaceName "Ethernet" `
  -OperationMode Multicast `
  -ClusterPrimaryIP 192.168.1.100 `
  -ClusterName "AppCluster"

# Add cluster port rule (HTTP)
Add-NlbClusterPortRule -HostName "AppCluster" `
  -StartPort 80 `
  -EndPort 80 `
  -Protocol Tcp `
  -Affinity Single

# Add cluster nodes
Add-NlbClusterNode -InterfaceName "Ethernet" `
  -HostName "Server2" `
  -NewNodeName "Server2" `
  -NewNodeInterface "Ethernet"
```

### Linux HAProxy

```bash
# Install HAProxy
sudo apt install haproxy

# Configure HAProxy
sudo nano /etc/haproxy/haproxy.cfg
```

**HAProxy configuration:**

```cfg
global
    log /dev/log local0
    maxconn 4096

defaults
    log global
    mode http
    option httplog
    timeout connect 5000ms
    timeout client 50000ms
    timeout server 50000ms

frontend http_front
    bind *:80
    default_backend http_back

backend http_back
    balance roundrobin
    option httpchk GET /health
    server server1 192.168.1.101:5000 check
    server server2 192.168.1.102:5000 check
    server server3 192.168.1.103:5000 check
```

**Start HAProxy:**

```bash
sudo systemctl enable haproxy
sudo systemctl start haproxy
sudo systemctl status haproxy
```

---

## Monitoring and Logging

### Windows Event Log

```csharp
// Program.cs
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "YourApp";
    settings.LogName = "Application";
});
```

### Linux syslog

```csharp
// Add Serilog
using Serilog;

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("/var/log/your-app/app-.log", rollingInterval: RollingInterval.Day);
});
```

### Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database")
    .AddCheck<OutboxHealthCheck>("outbox");

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
```

**Monitor health:**

```bash
# Windows
Invoke-WebRequest -Uri http://localhost/health

# Linux
curl http://localhost/health
```

---

## Security Hardening

### 1. Run with Least Privilege

**Windows:**
- Use ApplicationPoolIdentity (IIS)
- Use dedicated service account (Windows Service)

**Linux:**
- Use www-data or dedicated user
- Never run as root

### 2. File System Permissions

**Windows:**

```powershell
# Grant read/execute to app pool
$acl = Get-Acl "C:\inetpub\wwwroot\your-app"
$permission = "IIS APPPOOL\YourAppPool", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl "C:\inetpub\wwwroot\your-app" $acl
```

**Linux:**

```bash
# Set ownership
sudo chown -R www-data:www-data /opt/your-app

# Set permissions (read/execute for files, write for logs)
sudo chmod -R 755 /opt/your-app
sudo chmod -R 775 /opt/your-app/logs
```

### 3. Firewall Configuration

**Windows:**

```powershell
# Allow HTTP
New-NetFirewallRule -DisplayName "Allow HTTP" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 80 `
  -Action Allow

# Allow HTTPS
New-NetFirewallRule -DisplayName "Allow HTTPS" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 443 `
  -Action Allow
```

**Linux (ufw):**

```bash
# Enable firewall
sudo ufw enable

# Allow HTTP/HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Allow SSH (important!)
sudo ufw allow 22/tcp

# Check status
sudo ufw status
```

---

## Troubleshooting

### Application Won't Start (Windows)

```powershell
# Check IIS logs
Get-Content "C:\inetpub\wwwroot\your-app\logs\stdout*.log" -Tail 50

# Check Event Viewer
Get-EventLog -LogName Application -Newest 20

# Verify .NET runtime
dotnet --list-runtimes

# Test application manually
cd C:\inetpub\wwwroot\your-app
dotnet YourApp.dll
```

### Application Won't Start (Linux)

```bash
# Check systemd status
sudo systemctl status your-app.service

# View logs
sudo journalctl -u your-app.service -f

# Check permissions
ls -la /opt/your-app

# Test application manually
cd /opt/your-app
./YourApp
```

### Database Connection Issues

```powershell
# Test SQL Server connection (Windows)
sqlcmd -S .\SQLEXPRESS -E -Q "SELECT @@VERSION"

# Linux
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'YourPassword' -Q "SELECT @@VERSION"
```

---

## Next Steps

- **Docker:** [Containerize for easier deployment](docker.md)
- **Kubernetes:** [Orchestrate on-prem with K8s](kubernetes.md)
- **Monitoring:** [Health checks and observability](../observability/health-checks.md)
- **Security:** [Security best practices](security-best-practices.md)

---

## See Also

- [Docker Deployment](docker.md) - Containerize on-premises applications for consistent environments
- [Kubernetes Deployment](kubernetes.md) - Orchestrate on-premises containers with Kubernetes
- [Performance Tuning](../operations/performance-tuning.md) - Optimize throughput and latency for on-premises workloads

---

**Last Updated:** 2026-01-01
**Framework:** Excalibur 1.0.0
**Platforms:** Windows Server 2019+, Linux (Ubuntu 20.04+, RHEL 8+)
