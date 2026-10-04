namespace ServerManager.Plugin.Storage.S3;

public sealed record S3Settings(
    Uri? Endpoint,
    string Region,
    string Bucket,
    string Prefix,
    string AccessKey,
    string SecretKey,
    bool PathStyle)
{
    public string Key(string objectKey) => Prefix + objectKey;

    public override string ToString() =>
        $"S3Settings {{ Endpoint = {Endpoint}, Region = {Region}, Bucket = {Bucket}, Prefix = {Prefix}, PathStyle = {PathStyle} }}";
}
