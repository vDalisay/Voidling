param(
    [Parameter(Mandatory = $false)]
    [ValidateRange(1024, 65535)]
    [int]$StartPort = 27181,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 1000)]
    [int]$MaxAttempts = 21
)

$ErrorActionPreference = 'Stop'
$lastPort = [Math]::Min(65535, $StartPort + $MaxAttempts - 1)

for ($port = $StartPort; $port -le $lastPort; $port++) {
    $udp = $null
    try {
        # Match Voidling's LAN host requirements: IPv4 UDP bound on all local interfaces.
        # ExclusiveAddressUse makes this fail when another process already owns the port,
        # instead of producing a false-positive preflight result through socket reuse.
        $udp = [System.Net.Sockets.UdpClient]::new([System.Net.Sockets.AddressFamily]::InterNetwork)
        $udp.Client.ExclusiveAddressUse = $true
        $endpoint = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, $port)
        $udp.Client.Bind($endpoint)

        Write-Output $port
        exit 0
    }
    catch [System.Net.Sockets.SocketException] {
        # Occupied/excluded/unavailable on this machine. Try the next port.
    }
    finally {
        if ($null -ne $udp) {
            $udp.Dispose()
        }
    }
}

Write-Error "No bindable IPv4 UDP port was found from $StartPort through $lastPort. Close stale Godot/Voidling processes or choose another starting port."
exit 1
