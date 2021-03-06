# Adapted from: https://github.com/NLog/NLog.Extensions.Logging/blob/master/run-sonar.ps1

$projectFile = "src\MMALSharp\MMALSharp.csproj"
$sonarQubeId = "MMALSharp"
$github = "techyian/MMALSharp"
$baseBranch = "dev"
$framework = "netstandard2.0"


if ($env:APPVEYOR_REPO_NAME -eq $github) {

	if (-not $env:SONAR_TOKEN) {
        Write-warning "Sonar: not running SonarQube, no SONAR_KEY"
        return;
    }
	
	$prMode = $false;
    $branchMode = $false;
	
	if ($env:APPVEYOR_PULL_REQUEST_NUMBER) { 
        # first check PR as that is on the base branch
        $prMode = $true;
        Write-Output "Sonar: on PR $env:APPVEYOR_PULL_REQUEST_NUMBER"
    }
    elseif ($env:APPVEYOR_REPO_BRANCH -eq $baseBranch) {
        Write-Output "Sonar: on base branch ($baseBranch)"
    }
    else {
        $branchMode = $true;
        Write-Output "Sonar: on branch $env:APPVEYOR_REPO_BRANCH"
    }
	
	choco install "msbuild-sonarqube-runner" -y
	
	$sonarUrl = "https://sonarcloud.io"
	$sonarKey = $env:SONAR_KEY
    $sonarToken = $env:SONAR_TOKEN
    $buildVersion = $env:APPVEYOR_BUILD_VERSION
	
	
	if ($prMode) {
        $pr = $env:APPVEYOR_PULL_REQUEST_NUMBER
        Write-Output "Sonar: Running Sonar for PR $pr"
        SonarScanner.MSBuild.exe begin /o:"$sonarKey" /k:"$sonarQubeId" /d:"sonar.host.url=$sonarUrl" /d:"sonar.login=$sonarToken" /v:"$buildVersion" /d:"sonar.analysis.mode=preview" /d:"sonar.github.pullRequest=$pr" /d:"sonar.github.repository=$github" /d:"sonar.github.oauth=$env:GITHUB_AUTH"
    }
    elseif ($branchMode) {
        $branch = $env:APPVEYOR_REPO_BRANCH;
        Write-Output "Sonar: Running Sonar in branch mode for branch $branch"
        SonarScanner.MSBuild.exe begin /o:"$sonarKey" /k:"$sonarQubeId" /d:"sonar.host.url=$sonarUrl" /d:"sonar.login=$sonarToken" /v:"$buildVersion" /d:"sonar.branch.name=$branch"  
    }
    else {
        Write-Output "Sonar: Running Sonar in non-preview mode, on branch $env:APPVEYOR_REPO_BRANCH"
        SonarScanner.MSBuild.exe begin /o:"$sonarKey" /k:"$sonarQubeId" /d:"sonar.host.url=$sonarUrl" /d:"sonar.login=$sonarToken" /v:"$buildVersion"
    }
	
	msbuild /t:Rebuild $projectFile /p:targetFrameworks=$framework /verbosity:minimal

    SonarScanner.MSBuild.exe end /d:"sonar.login=$env:sonar_token"
}
else {
    Write-Output "Sonar: not running as we're on '$env:APPVEYOR_REPO_NAME'"
}