##############################
# This is a PowerShell Script.
# Supplied with a DLL, it will output all fields (and the classes they
# belong to) that do not begin with an "m_" prefix.
# Optionally, you may specify another prefix as the second argument.
##############################

function Invoke-Ternary ([bool] $decider, [object] $iftrue, [object] $iffalse)
{
    if ($decider) { $iftrue } else { $iffalse }
}

if ($args.Length -lt 1) {
    ""
    "This script finds all fields in all classes in a supplied dll that"
    "do not have an 'm_' prefix, and displays them"
    ""
    "Please supply a DLL to perform reflection on."
}
else
{
    set-Alias ?: Invoke-Ternary
    
    $dllPath = $args[0]
    if ($args[1] -eq $null) { 
        $prefix = "m_" 
    } else { 
        $prefix = $args[1] 
    } 
        
    Copy-Item $dllPath ($dllPath + ".test")
    
    [Reflection.Assembly]::LoadFrom($dllPath + ".test")
    ""
    
    $assemblies = [AppDomain]::CurrentDomain.GetAssemblies()
    $assembly = $assemblies | where {$_.fullname -match "RabbitMQ"}
    
    foreach ($type in $assembly.GetTypes())
    {
        foreach ($fieldInfo in $type.GetFields())
        {
            if ($fieldInfo.IsStatic -eq $false) {
                if ($fieldInfo.Name.SubString(0,2) -ne $prefix) {
                    $type.ToString() + ": " + $fieldInfo.Name
                }
            }
        }
    }
}


