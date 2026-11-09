using System.Diagnostics;
using System.IO;
using System.Text;

namespace Raahro.Services;

public class HotspotService : IHotspotService
{
    public async Task<bool> StartAsync(Action<string>? log = null)
    {
        try
        {
            var result = await RunPowerShellScriptAsync(StartScript);
            log?.Invoke("Hotspot: " + result);

            return result.Contains("ALREADY_ON", StringComparison.OrdinalIgnoreCase)
                   || result.Contains("START_STATUS=Success", StringComparison.OrdinalIgnoreCase)
                   || result.Contains("Success", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            log?.Invoke("Hotspot error: " + ex.Message);
            return false;
        }
    }

    public async Task StopAsync(Action<string>? log = null)
    {
        try
        {
            var result = await RunPowerShellScriptAsync(StopScript);
            log?.Invoke("Hotspot stop: " + result);
        }
        catch (Exception ex)
        {
            log?.Invoke("Stop hotspot error: " + ex.Message);
        }
    }

    private static async Task<string> RunPowerShellScriptAsync(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), $"Raahro-hotspot-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(path, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)!;
            var output = await p.StandardOutput.ReadToEndAsync();
            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            var text = (output + Environment.NewLine + err).Trim();
            return string.IsNullOrWhiteSpace(text) ? $"EXIT:{p.ExitCode}" : text;
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    private const string StartScript = """
$ErrorActionPreference = 'Stop'
try {
  Add-Type -AssemblyName System.Runtime.WindowsRuntime | Out-Null

  $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
      $_.Name -eq 'AsTask' -and
      $_.IsGenericMethod -and
      $_.GetParameters().Count -eq 1
    } | Select-Object -First 1)

  function Await($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    return $netTask.Result
  }

  $connType = [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]
  $profile = $connType::GetInternetConnectionProfile()
  if ($null -eq $profile) {
    Write-Output 'NO_PROFILE'
    exit 1
  }

  $tmType = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]
  $tm = $tmType::CreateFromConnectionProfile($profile)
  if ($null -eq $tm) {
    Write-Output 'NO_TETHERING_MANAGER'
    exit 1
  }

  $state = $tm.TetheringOperationalState.ToString()
  Write-Output ("STATE_BEFORE=" + $state)

  if ($state -eq 'On') {
    Write-Output 'ALREADY_ON'
    exit 0
  }

  $async = $tm.StartTetheringAsync()
  $resultType = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]
  $result = Await $async $resultType

  if ($null -eq $result) {
    Write-Output 'START_RESULT_NULL'
    exit 1
  }

  Write-Output ("START_STATUS=" + $result.Status.ToString())
  exit 0
}
catch {
  Write-Output ("ERR=" + $_.Exception.Message)
  exit 1
}
""";

    private const string StopScript = """
$ErrorActionPreference = 'Stop'
try {
  Add-Type -AssemblyName System.Runtime.WindowsRuntime | Out-Null

  $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
      $_.Name -eq 'AsTask' -and
      $_.IsGenericMethod -and
      $_.GetParameters().Count -eq 1
    } | Select-Object -First 1)

  function Await($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    return $netTask.Result
  }

  $connType = [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]
  $profile = $connType::GetInternetConnectionProfile()
  if ($null -eq $profile) {
    Write-Output 'NO_PROFILE'
    exit 0
  }

  $tmType = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]
  $tm = $tmType::CreateFromConnectionProfile($profile)
  if ($null -eq $tm) {
    Write-Output 'NO_TETHERING_MANAGER'
    exit 0
  }

  $state = $tm.TetheringOperationalState.ToString()
  if ($state -ne 'On') {
    Write-Output 'ALREADY_OFF'
    exit 0
  }

  $async = $tm.StopTetheringAsync()
  $resultType = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]
  $result = Await $async $resultType

  if ($null -eq $result) {
    Write-Output 'STOP_RESULT_NULL'
    exit 0
  }

  Write-Output ("STOP_STATUS=" + $result.Status.ToString())
  exit 0
}
catch {
  Write-Output ("ERR=" + $_.Exception.Message)
  exit 0
}
""";
}