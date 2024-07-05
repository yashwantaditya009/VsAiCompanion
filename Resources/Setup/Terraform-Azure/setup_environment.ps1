# Ask for the environment abbreviation
$env = Read-Host -Prompt "Enter the name of the environment (dev, test, prod, ...)"

function GetConfig {
    param (
        [Parameter(Mandatory=$true)]
        [string]$FilePath
    )
    $config = @{}
    $fileContent = Get-Content $FilePath
    $fileContent | ForEach-Object {
        if ($_ -match '(\w+)\s*=\s*"(.+?)"') {
            $config[$matches[1]] = $matches[2]
            Write-Host "$($matches[1]) = $($matches[2])"
        }
    }
    return $config
}

# Read and parse the backend file
$backendConfig = GetConfig "backend.${env}.tfvars"
$resourceGroupName = $backendConfig["resource_group_name"]
$storageAccountName = $backendConfig["storage_account_name"]

# Logout from Azure to ensure a clean login
az logout

# Login to Azure with normal user to retrieve service principal information.
az login

# Execute the service principal setup script for the new environment
.\setup_service_principal.ps1

$armClientId = $env:TF_VAR_ARM_CLIENT_ID
$armClientSecret = $env:TF_VAR_ARM_CLIENT_SECRET
$armTenantId = $env:TF_VAR_ARM_TENANT_ID
$armSubscriptionId = $env:TF_VAR_ARM_SUBSCRIPTION_ID
   
# Login to Azure with principal that will have owner permissions on azure resoure where everything will be created.
az login --service-principal -u $armClientId -p $armClientSecret --tenant $armTenantId
az account set --subscription $armSubscriptionId

# Get access key.
$env:ARM_ACCESS_KEY = (az storage account keys list --resource-group $resourceGroupName --account-name $storageAccountName --query '[0].value' -o tsv)

$tokenResponse = az account get-access-token --resource https://database.windows.net/ --output json
$env:ARM_DATABASE_ACCESS_TOKEN = ($tokenResponse | ConvertFrom-Json).accessToken

Write-Host "ARM_DATABASE_ACCESS_TOKEN = $env:ARM_DATABASE_ACCESS_TOKEN"

# Logout from Azure to ensure a clean login
az logout

# Run terraform init with the correct backend config and reconfigure
terraform init -reconfigure -backend-config="backend.${env}.tfvars"

# Refresh the state without making changes
terraform apply -refresh-only -var-file="variables.${env}.tfvars"

# Plan the changes for the new environment
terraform plan -var-file="variables.${env}.tfvars"
