Param(
	[ValidateSet("All", "Zip", "Installer")]$Target = "All"
)

# error to break
trap { break }

$product = 'NeeView'

$config = 'Release'


#---------------------
# get fileversion
function Get-FileVersion($fileName)
{
	throw "not supported."

	$major = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($fileName).FileMajorPart
	$minor = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($fileName).FileMinorPart

	"$major.$minor"
}


#---------------------
# get version from AssemblyInfo.cs
function Get-AssemblyVersion($assemblyInfo)
{
    $line = Get-Content $assemblyInfo | Select-String -Pattern "AssemblyFileVersion"
    if ($line -match "(\d+\.\d+)\.\d+\.\d+")
    {
        return $matches[1]
    }
    else
    {
        throw "Cannot get version"
    }
}


#---------------------
# get build count
function Get-BuildCount()
{
	# auto increment build version
	$xml = [xml](Get-Content "BuildCount.xml")
	return [int]$xml.build + 1
}

#---------------------
# set build count
function Set-BuildCount($buildCount)
{
	$xml = [xml](Get-Content "BuildCount.xml")
	$xml.build = [string]$buildCount
	$xml.Save("BuildCount.xml")
}


#--------------------
# replace keyword
function Replace-Content
{
	Param([string]$filepath, [string]$rep1, [string]$rep2)
	if ( $(Test-Path $filepath) -ne $True )
	{
		Write-Error "file not found"
		return
	}
	# input UTF8, output UTF8
	$file_contents = $(Get-Content -Encoding UTF8 $filepath) -replace $rep1, $rep2
	$file_contents | Out-File -Encoding UTF8 $filepath
}




#-----------------------
# variables
$solutionDir = "E:\Documents\Visual Studio 2015\Projects\$product"
$solution = "$solutionDir\$product.sln"
$projectDir = "$solutionDir\$product"
$productX86Dir = "$projectDir\bin\$config"
$productX64Dir = "$projectDir\bin\x64\$config"



#----------------------
# build
function Build-Project($arch)
{
	if ($arch -eq "x64")
	{
		$platform = "x64"
	}
	else
	{
		$platform = "Any CPU"
	}

	$msbuild = 'C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe'
	& $msbuild $solution /p:Configuration=$config /p:Platform=$platform /t:Clean,Build
	if ($? -ne $true)
	{
		throw "build error"
	}
}




#----------------------
# package section
function New-Package($productDir, $packageDir)
{
	$packageLibraryDir = $packageDir + "\Libraries"

	# make package folder
	$temp = New-Item $packageDir -ItemType Directory
	$temp = New-Item $packageLibraryDir -ItemType Directory

	# copy
	Copy-Item "$productDir\$product.exe" $packageDir
	Copy-Item "$productDir\$product.exe.config" $packageDir
	Copy-Item "$productDir\*.dll" $packageLibraryDir

	#Copy-Item "$productX64Dir\$product.exe" "$packageDir\${product}64.exe"
	#Copy-Item "$productX64Dir\$product.exe.config" "$packageDir\${product}64.exe.config"


	# copy language dll
	$langs = "de","en","es","fr","it","ja","ko","ru","zh-Hans","zh-Hant","x64","x86"
	foreach($lang in $langs)
	{
		Copy-Item "$productDir\$lang" $packageLibraryDir -Recurse
	}

	#------------------------
	# generate README.html

	$readmeDir = $packageDir + "\readme"
	$temp = New-Item $readmeDir -ItemType Directory 

	Copy-Item "$solutionDir\*.md" $readmeDir
	#Copy-Item "$solutionDir\Style.html" $readmeDir

	# edit README.md
	Replace-Content "$readmeDir\README.md" "<VERSION/>" "$version"

	# markdown to html by pandoc
	pandoc -s -t html5 -o "$packageDir\README.html" -H Style.html "$readmeDir\README.md" "$readmeDir\LICENSE.md" "$readmeDir\THIRDPARTY_LICENSES.md"

	Remove-Item $readmeDir -Recurse

	return $temp
}

#--------------------------
# archive to ZIP
function New-Zip
{
	Copy-Item $packageX86Dir $packageDir -Recurse
	Copy-Item "$packageX64Dir\$product.exe" "$packageDir\${product}64.exe"
	Copy-Item "$packageX64Dir\$product.exe.config" "$packageDir\${product}64.exe.config"

	Compress-Archive $packageDir -DestinationPath $packageZip
}


#--------------------------
# WiX
function New-Msi($arch, $packageDir, $packageMsi)
{
	$candle = 'C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe'
	$light = 'C:\Program Files (x86)\WiX Toolset v3.11\bin\light.exe'
	$heat = 'C:\Program Files (x86)\WiX Toolset v3.11\bin\heat.exe'



	$config = "$product.exe.config"
	$packageAppendDir = $packageDir + ".append"

	# remove append folder
	if (Test-Path $packageAppendDir)
	{
		Remove-Item $packageAppendDir -Recurse
		Start-Sleep -m 100
	}

	# make append folder
	$temp = New-Item $packageAppendDir -ItemType Directory

	# make config for installer
	[xml]$xml = Get-Content "$packageDir\$config"

	$add = $xml.configuration.appSettings.add | Where { $_.key -eq 'PackageType' } | Select -First 1
	$add.value = '.msi'

	$add = $xml.configuration.appSettings.add | Where { $_.key -eq 'UseLocalApplicationData' } | Select -First 1
	$add.value = 'True'

	$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
	$sw = New-Object System.IO.StreamWriter("$packageAppendDir\$config", $false, $utf8WithoutBom)
	$xml.Save( $sw )
	$sw.Close()


	# make DllComponents.wxs
	#& $heat dir "$packageDir\Libraries" -cg DllComponents -ag -pog:Binaries -sfrag -var var.LibrariesDir -dr INSTALLFOLDER -out WixSource\DllComponents.wxs
	#if ($? -ne $true)
	#{
	#	throw "heat error"
	#}

	#-------------------------
	# WiX
	#-------------------------

	$ErrorActionPreference = "stop"

	& $candle -arch $arch -d"Platform=$arch" -d"BuildVersion=$buildVersion" -d"ProductVersion=$version" -d"ContentDir=$packageDir\\" -d"AppendDir=$packageDir.append\\" -d"LibrariesDir=$packageDir\\Libraries" -ext WixNetFxExtension -out "$packageDir.append\\"  WixSource\*.wxs
	if ($? -ne $true)
	{
		throw "candle error"
	}

	& $light -out "$packageMsi" -ext WixUIExtension -ext WixNetFxExtension -cultures:ja-JP "$packageDir.append\*.wixobj"
	if ($? -ne $true)
	{
		throw "light error" 
	}
}

#--------------------------
# remove build objects
function Remove-BuildObjects
{
	if (Test-Path $packageDir)
	{
		Remove-Item $packageDir -Recurse -Force
	}
	if (Test-Path $packageX86Dir)
	{
		Remove-Item $packageX86Dir -Recurse -Force
	}
	if (Test-Path $packageX64Dir)
	{
		Remove-Item $packageX64Dir -Recurse -Force
	}
	if (Test-Path $packageZip)
	{
		Remove-Item $packageZip
	}
	if (Test-Path $packageX86Msi)
	{
		Remove-Item $packageX86Msi
	}
	if (Test-Path $packageX64Msi)
	{
		Remove-Item $packageX64Msi
	}

	Start-Sleep -m 100
}



#======================
# main
#======================

# versions
$version = Get-AssemblyVersion "$projectDir\Properties\AssemblyInfo.cs"
$buildCount = Get-BuildCount
$buildVersion = "$version.$buildCount"


$packageDir = "$product$version"
$packageX86Dir = "$product$version-32bit"
$packageX64Dir = "$product$version-64bit"
$packageZip = "$product$version.zip"
$packageX86Msi = "$product$version.msi"
$packageX64Msi = "$product$version-64bit.msi"


# clear
Write-Host "`n[Clear] ...`n" -fore Cyan
Remove-BuildObjects
	
# build
Write-Host "`n[Build] ...`n" -fore Cyan
Build-Project "x86"
Build-Project "x64"

#
Write-Host "`n[Package] ...`n" -fore Cyan
New-Package $productX86Dir $packageX86Dir
New-Package $productX64Dir $packageX64Dir

#
if (($Target -eq "All") -or ($Target -eq "Zip"))
{
	Write-Host "`[Zip] ...`n" -fore Cyan
	New-Zip
	Write-Host "`nExport $packageZip successed.`n" -fore Green
}

if (($Target -eq "All") -or ($Target -eq "Installer"))
{
	Write-Host "`[Installer] ...`n" -fore Cyan
	New-Msi "x86" $packageX86Dir $packageX86Msi
	Write-Host "`nExport $packageX86Msi successed.`n" -fore Green
	New-Msi "x64" $packageX64Dir $packageX64Msi
	Write-Host "`nExport $packageX64Msi successed.`n" -fore Green
}

# current
Write-Host "`[Current] ...`n" -fore Cyan
if (-not (Test-Path $product))
{
	New-Item $product -ItemType Directory
}
Copy-Item "$packageDir\*" "$product\" -Recurse -Force

#--------------------------
# saev buid version
Set-BuildCount $buildCount

#-------------------------
# Finish.
Write-Host "`nBuild $buildVersion All done.`n" -fore Green





