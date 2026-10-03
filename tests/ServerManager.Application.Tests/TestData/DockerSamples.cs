namespace ServerManager.Application.Tests.TestData;

/// <summary>Docker 28.5 (docker:28-dind) üzerinde alınmış gerçek çıktılar; yalnızca test container'larına aittir.</summary>
public static class DockerSamples
{
    public const string WebId = "7d0dac637d87c33da2c836cefd241381397da60990807d8bef2c1733a2f61abb";
    public const string CacheId = "482dca7a41ec8a084cb63ce725ef98b55387884a7a768295b467f20b7d0f4121";
    public const string FailedJobId = "b94bc5d6b3957ddc430ae893f9991f9c7eb74b90c843000d751263f776fac514";

    public const string Ps = """
        {"Command":"\"sh -c 'echo hata \u003e\u00262; echo bitti; exit 3'\"","CreatedAt":"2026-10-03 18:31:49 +0000 UTC","ID":"b94bc5d6b3957ddc430ae893f9991f9c7eb74b90c843000d751263f776fac514","Image":"alpine:3","Labels":"","LocalVolumes":"0","Mounts":"","Names":"failed-job","Networks":"bridge","Platform":null,"Ports":"","RunningFor":"20 minutes ago","Size":"0B (virtual 8.66MB)","State":"exited","Status":"Exited (3) 20 minutes ago"}
        {"Command":"\"docker-entrypoint.sh redis-server\"","CreatedAt":"2026-10-03 18:31:49 +0000 UTC","ID":"482dca7a41ec8a084cb63ce725ef98b55387884a7a768295b467f20b7d0f4121","Image":"redis:alpine","Labels":"com.docker.compose.project=shop,com.docker.compose.service=cache","LocalVolumes":"0","Mounts":"","Names":"cache","Networks":"bridge","Platform":null,"Ports":"","RunningFor":"20 minutes ago","Size":"89B (virtual 119MB)","State":"exited","Status":"Exited (0) 4 minutes ago"}
        {"Command":"\"/docker-entrypoint.sh nginx -g 'daemon off;'\"","CreatedAt":"2026-10-03 18:31:49 +0000 UTC","ID":"7d0dac637d87c33da2c836cefd241381397da60990807d8bef2c1733a2f61abb","Image":"nginx:alpine","Labels":"maintainer=NGINX Docker Maintainers \u003cdocker-maint@nginx.com\u003e","LocalVolumes":"1","Mounts":"webdata","Names":"web","Networks":"appnet,bridge","Platform":null,"Ports":"0.0.0.0:8080-\u003e80/tcp, [::]:8080-\u003e80/tcp","RunningFor":"20 minutes ago","Size":"1.19kB (virtual 62.4MB)","State":"running","Status":"Up 6 minutes (unhealthy)"}
        """;

    public const string Info = """
        WARNING: bridge-nf-call-iptables is disabled
        {"Containers":3,"ContainersRunning":1,"ContainersPaused":0,"ContainersStopped":2,"Images":3,"Driver":"overlay2","DockerRootDir":"/var/lib/docker","NCPU":8,"MemTotal":8217473024,"ServerVersion":"28.5.2","OperatingSystem":"Alpine Linux v3.22 (containerized)","KernelVersion":"6.10.14-linuxkit"}
        """;

    public const string Stats = """
        {"BlockIO":"0B / 12.3kB","CPUPerc":"0.00%","Container":"7d0dac637d87c33da2c836cefd241381397da60990807d8bef2c1733a2f61abb","ID":"7d0dac637d87c33da2c836cefd241381397da60990807d8bef2c1733a2f61abb","MemPerc":"0.12%","MemUsage":"9.207MiB / 7.653GiB","Name":"web","NetIO":"3.1kB / 252B","PIDs":"13"}
        """;

    public const string Images = """
        {"Containers":"1","CreatedAt":"2026-09-22 22:09:50 +0000 UTC","CreatedSince":"10 days ago","Digest":"sha256:df221db836e1754089190208cee7eeda94f233197056426eda74a43ab1abeac2","ID":"sha256:a2b80c421aaa02ba1ef4ef2d3c991112674e676f9db05fd42d456f2529b83a86","Repository":"nginx","SharedSize":"N/A","Size":"62.4MB","Tag":"alpine","UniqueSize":"N/A","VirtualSize":"62.45MB"}
        {"Containers":"N/A","CreatedAt":"2026-09-01 10:00:00 +0000 UTC","CreatedSince":"4 weeks ago","Digest":"\u003cnone\u003e","ID":"sha256:1111111111111111111111111111111111111111111111111111111111111111","Repository":"\u003cnone\u003e","SharedSize":"N/A","Size":"5.2MB","Tag":"\u003cnone\u003e","UniqueSize":"N/A","VirtualSize":"5.2MB"}
        {"Containers":"1","CreatedAt":"2026-09-17 20:37:05 +0000 UTC","CreatedSince":"2 weeks ago","Digest":"sha256:294b683cb724975bec92580e1e685676bd4b50bda910ddb8c51d4cabeaec77e6","ID":"sha256:33bee74c45f307e3268adc2010c0f55c48e7a6041e12cd12432bb1a46e498e43","Repository":"alpine","SharedSize":"N/A","Size":"8.66MB","Tag":"3","UniqueSize":"N/A","VirtualSize":"8.66MB"}
        """;

    public const string Volumes = """
        {"Availability":"N/A","Driver":"local","Group":"N/A","Labels":"","Links":"N/A","Mountpoint":"/var/lib/docker/volumes/orphan-vol/_data","Name":"orphan-vol","Scope":"local","Size":"N/A","Status":"N/A"}
        {"Availability":"N/A","Driver":"local","Group":"N/A","Labels":"com.docker.compose.project=shop","Links":"N/A","Mountpoint":"/var/lib/docker/volumes/webdata/_data","Name":"webdata","Scope":"local","Size":"N/A","Status":"N/A"}
        """;

    public const string DiskUsageVerbose = """
        {"Images":[],"Containers":[],"Volumes":[{"Name":"webdata","Size":"1.393kB","Links":"1"},{"Name":"orphan-vol","Size":"0B","Links":"0"}],"BuildCache":[]}
        """;

    public const string Networks = """
        {"CreatedAt":"2026-10-03 18:31:49.693094553 +0000 UTC","Driver":"bridge","ID":"ea60976ce88cc07db45bb74d40114157427c2a1806b96a96ccf6e888ce4d48c6","IPv4":"true","IPv6":"false","Internal":"false","Labels":"","Name":"appnet","Scope":"local"}
        {"CreatedAt":"2026-10-03 18:45:13.069397092 +0000 UTC","Driver":"bridge","ID":"b5d6494643a3d575d5f073d08f41cc881c6569ff3f9e5e1a47ec2a0bc3e92e95","IPv4":"true","IPv6":"false","Internal":"false","Labels":"","Name":"bridge","Scope":"local"}
        {"CreatedAt":"2026-10-03 18:26:33.771441171 +0000 UTC","Driver":"host","ID":"dc9ad126cfc5a7fed1591b78c8833ae97e168e51c3bdec4d8dd61b6207cb07d1","IPv4":"true","IPv6":"false","Internal":"false","Labels":"","Name":"host","Scope":"local"}
        {"CreatedAt":"2026-10-03 18:26:33.770101796 +0000 UTC","Driver":"null","ID":"5c33586615f7c75fe2d1e72d1a61037770943666052127764063c0ef1bea299a","IPv4":"true","IPv6":"false","Internal":"true","Labels":"","Name":"none","Scope":"local"}
        """;

    public const string NetworkInspect = """
        [{"Name":"appnet","Id":"ea60976ce88cc07db45bb74d40114157427c2a1806b96a96ccf6e888ce4d48c6","Driver":"bridge","IPAM":{"Driver":"default","Config":[{"Subnet":"172.30.0.0/16","Gateway":"172.30.0.1"}]},"Containers":{"7d0dac637d87c33da2c836cefd241381397da60990807d8bef2c1733a2f61abb":{"Name":"web","IPv4Address":"172.30.0.2/16","IPv6Address":""}}}]
        """;

    public const string DiskUsage = """
        {"Active":"3","Reclaimable":"8.66MB (4%)","Size":"181MB","TotalCount":"3","Type":"Images"}
        {"Active":"1","Reclaimable":"89B (6%)","Size":"1.275kB","TotalCount":"3","Type":"Containers"}
        {"Active":"1","Reclaimable":"0B (0%)","Size":"1.393kB","TotalCount":"2","Type":"Local Volumes"}
        {"Active":"0","Reclaimable":"0B","Size":"0B","TotalCount":"0","Type":"Build Cache"}
        """;

    public const string WebInspect = """
        [{
          "Id": "7d0dac637d87c33da2c836cefd241381397da60990807d8bef2c1733a2f61abb",
          "Created": "2026-10-03T18:31:49.451926177Z",
          "Name": "/web",
          "RestartCount": 2,
          "Platform": "linux",
          "State": {
            "Status": "running",
            "Running": true,
            "OOMKilled": false,
            "ExitCode": 0,
            "Error": "",
            "StartedAt": "2026-10-03T18:45:13.224248009Z",
            "FinishedAt": "2026-10-03T18:44:58.105523390Z",
            "Health": { "Status": "healthy" }
          },
          "HostConfig": { "RestartPolicy": { "Name": "on-failure", "MaximumRetryCount": 5 } },
          "Mounts": [
            { "Type": "volume", "Name": "webdata", "Source": "/var/lib/docker/volumes/webdata/_data", "Destination": "/usr/share/nginx/html", "Driver": "local", "Mode": "z", "RW": true }
          ],
          "Config": {
            "Hostname": "7d0dac637d87",
            "User": "",
            "Env": [ "API_SECRET=supersecret", "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin", "EMPTY=" ],
            "Cmd": [ "nginx", "-g", "daemon off;" ],
            "Image": "nginx:alpine",
            "WorkingDir": "",
            "Entrypoint": [ "/docker-entrypoint.sh" ],
            "Labels": { "com.docker.compose.project": "shop", "maintainer": "NGINX Docker Maintainers <docker-maint@nginx.com>" }
          },
          "NetworkSettings": {
            "Ports": {
              "80/tcp": [ { "HostIp": "0.0.0.0", "HostPort": "8080" }, { "HostIp": "::", "HostPort": "8080" } ],
              "443/tcp": null
            },
            "Networks": {
              "appnet": { "Aliases": [ "web", "frontend" ], "MacAddress": "02:42:ac:1e:00:02", "IPAddress": "172.30.0.2", "Gateway": "172.30.0.1" },
              "bridge": { "Aliases": null, "MacAddress": "", "IPAddress": "172.17.0.2", "Gateway": "172.17.0.1" }
            }
          }
        }]
        """;

    public const string LogsStdout =
        "2026-10-03T18:45:13.224248009Z /docker-entrypoint.sh: Configuration complete\n" +
        "2026-10-03T18:48:14.123407217Z follow-test-stdout-1\n" +
        "2026-10-03T18:48:19.219917720Z \u001b[32mrenkli\u001b[0m satır\n";

    public const string LogsStderr =
        "2026-10-03T18:48:14.123500000Z follow-test-stderr-2\n";
}
